using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private readonly List<TestResult> _results = new();
        private int _total, _passed, _failed, _skipped;

        public TestRunner(Assembly testAssembly)
        {
            _testAssembly = testAssembly;
        }

        public async Task<IReadOnlyList<TestResult>> RunAllAsync()
        {
            var testClasses = _testAssembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestSuiteAttribute>() != null);

            foreach (var testClass in testClasses)
            {
                await RunClassAsync(testClass);
            }

            return _results;
        }

        private async Task RunClassAsync(Type testClass)
        {
            Console.WriteLine($"\n--- Тестовый класс: {testClass.Name} ---");

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
                .OrderBy(m => m.GetCustomAttribute<FactAttribute>()!.Priority);

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
                    await RunTestMethod(testInstance, method, initializeMethod, cleanupMethod, null);
                }
                else
                {
                    foreach (var data in dataAttrs)
                    {
                        await RunTestMethod(testInstance, method, initializeMethod, cleanupMethod, data.Parameters);
                    }
                }
            }

            sharedContext?.Dispose();
        }

        private async Task RunTestMethod(object? instance, MethodInfo method, MethodInfo? init, MethodInfo? cleanup, object[]? parameters)
        {
            _total++;
            var start = DateTime.UtcNow;
            string paramString = parameters != null ? $"({string.Join(", ", parameters)})" : "()";
            bool passed = false;
            string? message = null;

            try
            {
                init?.Invoke(instance, null);
                object? result = method.Invoke(instance, parameters);
                if (result is Task task)
                    await task;
                passed = true;
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
            if (skipped)
            {
                _skipped++;
                _results.Add(new TestResult
                {
                    TestName = methodName,
                    Parameters = parameters,
                    Passed = false,
                    Skipped = true,
                    Message = message,
                    Duration = duration ?? TimeSpan.Zero
                });
            }
            else if (passed)
            {
                _passed++;
                _results.Add(new TestResult
                {
                    TestName = methodName,
                    Parameters = parameters,
                    Passed = true,
                    Skipped = false,
                    Duration = duration ?? TimeSpan.Zero
                });
            }
            else
            {
                _failed++;
                _results.Add(new TestResult
                {
                    TestName = methodName,
                    Parameters = parameters,
                    Passed = false,
                    Skipped = false,
                    Message = message,
                    Duration = duration ?? TimeSpan.Zero
                });
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