using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DynamicThreadPool;
using TestEngine;

namespace LoadSimulator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Лабораторная работа №4: Демонстрация новых возможностей ===\n");

            using var pool = new DynamicPool(minThreads: 2, maxThreads: 4, idleTimeoutSeconds: 3);
            SubscribeToEvents(pool);

            string testsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTests.dll");
            Assembly testsAssembly = Assembly.LoadFrom(testsPath);
            var runner = new TestRunner(testsAssembly, maxDegreeOfParallelism: 2);

            Console.WriteLine("\n--- Запуск тестов с фильтром (Category = Demo или Smoke) ---");
            var filteredResults = runner.RunAllAsync(method =>
            {
                var fact = method.GetCustomAttribute<FactAttribute>();
                return fact != null && (fact.Category == "Demo" || fact.Category == "Smoke");
            }).GetAwaiter().GetResult();

            Console.WriteLine($"\nВсего отфильтровано: {filteredResults.Count}");
            foreach (var r in filteredResults)
            {
                string status = r.Passed ? "ПРОЙДЕН" : (r.Skipped ? "ПРОПУЩЕН" : "ПРОВАЛЕН");
                Console.WriteLine($"[{status}] {r.TestName} – {r.Message}");
            }

            Console.WriteLine("\n--- Демонстрация событий пула потоков (имитация нагрузки) ---");
            for (int i = 0; i < 10; i++)
            {
                int taskId = i;
                pool.Enqueue(() =>
                {
                    Console.WriteLine($"  Выполняется задача {taskId} в потоке {Thread.CurrentThread.Name}");
                    Thread.Sleep(200);
                });
            }

            while (pool.TaskQueueCount > 0 || pool.ActiveThreadCount > 0)
                Thread.Sleep(500);

            Console.WriteLine("\nВсе задачи выполнены. Нажмите Enter для выхода.");
            Console.ReadLine();
        }

        static void SubscribeToEvents(DynamicPool pool)
        {
            pool.ThreadCreated += (s, e) => Console.WriteLine($"[Событие] Поток создан: {e.ThreadName}");
            pool.ThreadDestroyed += (s, e) => Console.WriteLine($"[Событие] Поток завершён: {e.ThreadName}");
            pool.TaskStarted += (s, e) => Console.WriteLine($"[Событие] Задача начата: {e.TaskDescription}");
            pool.TaskCompleted += (s, e) => Console.WriteLine($"[Событие] Задача завершена: {e.TaskDescription}");
            pool.TaskFailed += (s, e) => Console.WriteLine($"[Событие] Задача провалена: {e.TaskDescription} – {e.Exception?.Message}");
            pool.PoolDisposed += (s, e) => Console.WriteLine("[Событие] Пул потоков уничтожен");
        }
    }
}