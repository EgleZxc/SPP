using System;
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
            Console.WriteLine("=== Запуск тестов ===\n");

            string testsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MyTests.dll");
            Assembly testsAssembly = Assembly.LoadFrom(testsPath);

            var runner = new TestRunner(testsAssembly);
            var results = await runner.RunAllAsync();

            foreach (var res in results)
            {
                if (res.Skipped)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[ПРОПУЩЕН] {res.TestName}{res.Parameters} – {res.Message}");
                }
                else if (res.Passed)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[ОК] {res.TestName}{res.Parameters} ({res.Duration.TotalMilliseconds} мс)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ПРОВАЛЕН] {res.TestName}{res.Parameters} – {res.Message}");
                }
                Console.ResetColor();
            }

            runner.PrintSummary();

            File.WriteAllLines("test_results.txt", results.Select(r =>
                $"{r.TestName}{r.Parameters}: {(r.Skipped ? "ПРОПУЩЕН" : r.Passed ? "ОК" : "ПРОВАЛЕН")} {r.Message}"));
            Console.WriteLine("\nРезультаты также сохранены в файл test_results.txt");
        }
    }
}