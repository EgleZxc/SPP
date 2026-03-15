using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TestEngine
{
    public class TestResult
    {
        public string TestName { get; set; }
        public string? Parameters { get; set; }
        public bool Passed { get; set; }
        public bool Skipped { get; set; }
        public string? Message { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TestRunner
    {
        private readonly Assembly _testAssembly;
        private readonly int _maxDegreeOfParallelism;
        private readonly ConcurrentBag<TestResult> _results = new();
        private int _total, _passed, _failed, _skipped;
        private readonly object _consoleLock = new(); 

        public TestRunner(Assembly testAssembly, int maxDegreeOfParallelism = 0)
        {
            _testAssembly = testAssembly;
            _maxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount;
        }

        public async Task<IReadOnlyList<TestResult>> RunAllAsync()
        {
            var testTasks = new List<Func<Task>>(); 
            var testClasses = _testAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestSuiteAttribute>() != null);

            foreach (var testClass in testClasses)
            {
                var (testInstance, initializeMethod, cleanupMethod, testMethods) = PrepareClass(testClass);

                foreach (var method in testMethods)
                {
                    var skipAttr = method.GetCustomAttribute<SkipAttribute>();
                    if (skipAttr != null)
                    {
                        lock (_consoleLock)
                        {
                            LogResult(method.Name, null, false, true, $"Пропущен: {skipAttr.Reason}");
                        }
                        continue;
                    }

                    var dataAttrs = method.GetCustomAttributes<TestDataAttribute>().ToList();
                    if (dataAttrs.Count == 0)
                    {
                        testTasks.Add(() => RunTestMethodAsync(testInstance, method, initializeMethod, cleanupMethod, null));
                    }
                    else
                    {
                        foreach (var data in dataAttrs)
                        {
                            testTasks.Add(() => RunTestMethodAsync(testInstance, method, initializeMethod, cleanupMethod, data.Parameters));
                        }
                    }
                }
            }

            using var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism);
            var tasks = testTasks.Select(async taskFunc =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await taskFunc();
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
            return _results.ToList();
        }

        public async Task<IReadOnlyList<TestResult>> RunAllSequentialAsync()
        {
            var testClasses = _testAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestSuiteAttribute>() != null);

            foreach (var testClass in testClasses)
            {
                var (testInstance, initializeMethod, cleanupMethod, testMethods) = PrepareClass(testClass);

                foreach (var method in testMethods)
                {
                    var skipAttr = method.GetCustomAttribute<SkipAttribute>();
                    if (skipAttr != null)
                    {
                        LogResult(method.Name, null, false, true, $"Пропущен: {skipAttr.Reason}");
                        continue;
                    }

                    var dataAttrs = method.GetCustomAttributes<TestDataAttribute>().ToList();
                    if (dataAttrs.Count == 0)
                    {
                        await RunTestMethodAsync(testInstance, method, initializeMethod, cleanupMethod, null);
                    }
                    else
                    {
                        foreach (var data in dataAttrs)
                        {
                            await RunTestMethodAsync(testInstance, method, initializeMethod, cleanupMethod, data.Parameters);
                        }
                    }
                }
            }

            return _results.ToList();
        }

        private (object? instance, MethodInfo? init, MethodInfo? cleanup, List<MethodInfo> testMethods) PrepareClass(Type testClass)
        {
            lock (_consoleLock)
            {
                Console.WriteLine($"\n--- Тестовый класс: {testClass.Name} ---");
            }

            ISharedContext? sharedContext = null;
            var sharedAttr = testClass.GetCustomAttribute<SharedContextTypeAttribute>();
            if (sharedAttr != null)
            {
                sharedContext = Activator.CreateInstance(sharedAttr.ContextType) as ISharedContext;
                sharedContext?.Initialize();
            }

            object? testInstance;
            if (sharedContext != null && testClass.GetConstructor(new[] { sharedAttr!.ContextType }) != null)
                testInstance = Activator.CreateInstance(testClass, sharedContext);
            else
                testInstance = Activator.CreateInstance(testClass);

            var initializeMethod = testClass.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<InitializeAttribute>() != null);
            var cleanupMethod = testClass.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<CleanupAttribute>() != null);

            var testMethods = testClass.GetMethods()
                .Where(m => m.GetCustomAttribute<FactAttribute>() != null)
                .OrderBy(m => m.GetCustomAttribute<FactAttribute>()!.Priority)
                .ToList();

            return (testInstance, initializeMethod, cleanupMethod, testMethods);
        }

        private async Task RunTestMethodAsync(object? instance, MethodInfo method, MethodInfo? init, MethodInfo? cleanup, object[]? parameters)
        {
            var factAttr = method.GetCustomAttribute<FactAttribute>();
            int timeout = factAttr?.TimeoutMilliseconds ?? 0;

            Interlocked.Increment(ref _total);
            var start = DateTime.UtcNow;
            string paramString = parameters != null ? $"({string.Join(", ", parameters)})" : "()";
            bool passed = false;
            string? message = null;

            try
            {
                Task testTask = Task.Run(async () =>
                {
                    init?.Invoke(instance, null);
                    object? result = method.Invoke(instance, parameters);
                    if (result is Task task)
                        await task;
                });

                Task timeoutTask = timeout > 0 ? Task.Delay(timeout) : Task.CompletedTask;
                var completedTask = await Task.WhenAny(testTask, timeoutTask);

                if (timeout > 0 && completedTask == timeoutTask)
                {
                    message = $"Тест превысил время ожидания ({timeout} мс)";
                }
                else
                {
                    await testTask; 
                    passed = true;
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException is AssertionException ass)
            {
                message = ass.Message;
            }
            catch (TargetInvocationException ex)
            {
                message = $"Неожиданная ошибка: {ex.InnerException?.Message}";
            }
            catch (Exception ex)
            {
                message = $"Ошибка теста: {ex.Message}";
            }
            finally
            {
                cleanup?.Invoke(instance, null);
                var duration = DateTime.UtcNow - start;
                LogResult(method.Name, paramString, passed, false, message, duration);
            }
        }

        private void LogResult(string methodName, string? parameters, bool passed, bool skipped, string? message, TimeSpan? duration = null)
        {
            Interlocked.Increment(ref _total); 

            TestResult result;
            if (skipped)
            {
                Interlocked.Increment(ref _skipped);
                result = new TestResult
                {
                    TestName = methodName,
                    Parameters = parameters,
                    Passed = false,
                    Skipped = true,
                    Message = message,
                    Duration = duration ?? TimeSpan.Zero
                };
            }
            else if (passed)
            {
                Interlocked.Increment(ref _passed);
                result = new TestResult
                {
                    TestName = methodName,
                    Parameters = parameters,
                    Passed = true,
                    Skipped = false,
                    Duration = duration ?? TimeSpan.Zero
                };
            }
            else
            {
                Interlocked.Increment(ref _failed);
                result = new TestResult
                {
                    TestName = methodName,
                    Parameters = parameters,
                    Passed = false,
                    Skipped = false,
                    Message = message,
                    Duration = duration ?? TimeSpan.Zero
                };
            }

            _results.Add(result);

            lock (_consoleLock)
            {
                if (skipped)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[ПРОПУЩЕН] {methodName}{parameters} – {message}");
                }
                else if (passed)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[ОК] {methodName}{parameters} ({duration?.TotalMilliseconds} мс)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ПРОВАЛЕН] {methodName}{parameters} – {message}");
                }
                Console.ResetColor();
            }
        }

        public void PrintSummary()
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine($"Всего тестов : {_total}");
            Console.WriteLine($"Пройдено     : {_passed}");
            Console.WriteLine($"Провалено    : {_failed}");
            Console.WriteLine($"Пропущено    : {_skipped}");
            Console.WriteLine(new string('=', 50));
        }
    }
}