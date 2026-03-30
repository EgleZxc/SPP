using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DynamicThreadPool;
using TestEngine;
using ApplicationUnderTest;

namespace LoadSimulator
{
    class Program
    {
        static int totalTasks = 0;
        static int completedTasks = 0;
        static readonly object statsLock = new();

        static void Main(string[] args)
        {
            Console.WriteLine("=== Симулятор нагрузки для динамического пула потоков ===\n");

            using IDynamicThreadPool pool = new DynamicPool(minThreads: 2, maxThreads: 8, idleTimeoutSeconds: 3);
            var monitorTask = Task.Run(() => MonitorState(pool));

            string testsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTests.dll");
            Assembly testsAssembly = Assembly.LoadFrom(testsPath);

            var testMethods = GetAllTestMethods(testsAssembly);
            Console.WriteLine($"Найдено тестовых методов: {testMethods.Count}");

            GenerateLoad(pool, testMethods);

            while (completedTasks < totalTasks)
            {
                Thread.Sleep(500);
            }

            Console.WriteLine("\nВсе задачи выполнены.");
            monitorTask.Wait(1000);
            Console.WriteLine(pool.GetState());
        }

        static List<(object instance, MethodInfo method, object[]? parameters)> GetAllTestMethods(Assembly assembly)
        {
            var result = new List<(object instance, MethodInfo method, object[]? parameters)>();
            var testClasses = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestSuiteAttribute>() != null);

            foreach (var testClass in testClasses)
            {
                if (testClass.GetCustomAttribute<SharedContextTypeAttribute>() != null)
                    continue;

                object? instance = Activator.CreateInstance(testClass);
                if (instance == null) continue;

                var testMethods = testClass.GetMethods()
                    .Where(m => m.GetCustomAttribute<FactAttribute>() != null)
                    .OrderBy(m => m.GetCustomAttribute<FactAttribute>()!.Priority);

                foreach (var method in testMethods)
                {
                    if (method.GetCustomAttribute<SkipAttribute>() != null)
                        continue;

                    var dataAttrs = method.GetCustomAttributes<TestDataAttribute>().ToList();
                    if (dataAttrs.Count == 0)
                    {
                        result.Add((instance, method, null));
                    }
                    else
                    {
                        foreach (var data in dataAttrs)
                        {
                            result.Add((instance, method, data.Parameters));
                        }
                    }
                }
            }
            return result;
        }

        static void GenerateLoad(IDynamicThreadPool pool, List<(object instance, MethodInfo method, object[]? parameters)> testMethods)
        {
            Console.WriteLine(">>> ПИК 1: добавление 20 тестов...");
            for (int i = 0; i < 20 && totalTasks < 50; i++)
            {
                var test = testMethods[i % testMethods.Count];
                EnqueueTest(pool, test.instance, test.method, test.parameters);
                Thread.Sleep(20);
            }

            Console.WriteLine(">>> ПАУЗА 5 секунд (без новых задач)");
            Thread.Sleep(5000);

            Console.WriteLine(">>> ПИК 2: добавление 30 тестов...");
            for (int i = 0; i < 30 && totalTasks < 50; i++)
            {
                var test = testMethods[(i + 20) % testMethods.Count];
                EnqueueTest(pool, test.instance, test.method, test.parameters);
                Thread.Sleep(10);
            }

            int remaining = 50 - totalTasks;
            Console.WriteLine($">>> ЕДИНИЧНЫЕ ЗАДАЧИ: {remaining} задач с интервалом 2 сек");
            for (int i = 0; i < remaining; i++)
            {
                var test = testMethods[(i + 50) % testMethods.Count];
                EnqueueTest(pool, test.instance, test.method, test.parameters);
                Thread.Sleep(2000);
            }

            Console.WriteLine($"Всего отправлено задач: {totalTasks}");
        }

        static void EnqueueTest(IDynamicThreadPool pool, object instance, MethodInfo method, object[]? parameters)
        {
            totalTasks++;
            pool.Enqueue(() =>
            {
                var start = Stopwatch.GetTimestamp();
                try
                {
                    object? result = method.Invoke(instance, parameters);
                    if (result is Task task)
                    {
                        task.GetAwaiter().GetResult();
                    }
                    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    lock (statsLock)
                    {
                        completedTasks++;
                        Console.WriteLine($"[Тест {completedTasks}/{totalTasks}] {method.Name} пройден за {elapsed:F0} мс (поток: {Thread.CurrentThread.Name})");
                    }
                }
                catch (Exception ex)
                {
                    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    lock (statsLock)
                    {
                        completedTasks++;
                        Console.WriteLine($"[Тест {completedTasks}/{totalTasks}] {method.Name} провален: {ex.InnerException?.Message ?? ex.Message} (поток: {Thread.CurrentThread.Name})");
                    }
                }
            });
        }

        static void MonitorState(IDynamicThreadPool pool)
        {
            while (true)
            {
                Console.WriteLine($"[Монитор] {pool.GetState()}");
                Thread.Sleep(1000);
            }
        }
    }
}