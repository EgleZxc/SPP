using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TestEngine;

namespace TestRunnerApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Собственный запускатор тестов (параллельная версия) ===\n");

            string testsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTests.dll");
            Assembly testsAssembly = Assembly.LoadFrom(testsPath);

            int maxDegree = 4;
            Console.WriteLine($"Максимальная степень параллелизма: {maxDegree}");

            Console.WriteLine("\n--- Запуск последовательно ---");
            var sequentialRunner = new TestRunner(testsAssembly, 1);
            var sw = Stopwatch.StartNew();
            var resultsSeq = await sequentialRunner.RunAllAsync();
            sw.Stop();
            Console.WriteLine($"Время последовательного выполнения: {sw.ElapsedMilliseconds} мс");
            sequentialRunner.PrintSummary();

            Console.WriteLine("\n" + new string('-', 50));

            Console.WriteLine("\n--- Запуск параллельно ---");
            var parallelRunner = new TestRunner(testsAssembly, maxDegree);
            sw.Restart();
            var resultsPar = await parallelRunner.RunAllAsync();
            sw.Stop();
            Console.WriteLine($"Время параллельного выполнения: {sw.ElapsedMilliseconds} мс");
            parallelRunner.PrintSummary();

            File.WriteAllLines("test_results.txt", resultsPar.Select(r =>
                $"{r.TestName}{r.Parameters}: {(r.Skipped ? "ПРОПУЩЕН" : r.Passed ? "ОК" : "ПРОВАЛЕН")} {r.Message}"));
            Console.WriteLine("\nРезультаты также сохранены в файл test_results.txt");
        }
    }
}