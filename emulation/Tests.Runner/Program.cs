/**
 * Program.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Antmicro.Renode.Tests.Runner
{
    /// <summary>
    /// Simple console test runner for I2C unit tests.
    /// </summary>
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("I2C Peripheral Unit Tests");
            Console.WriteLine("========================================");
            Console.WriteLine();

            int passed = 0;
            int failed = 0;

            // Get the test assembly - look in the same directory as the runner
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var testDllPath = Path.Combine(baseDir, "Peripherals.Tests.dll");
            
            if (!File.Exists(testDllPath))
            {
                // Try parent directory
                testDllPath = Path.Combine(baseDir, "..", "Peripherals.Tests.dll");
            }
            
            var testAssembly = Assembly.LoadFrom(testDllPath);

            foreach (var type in testAssembly.GetTypes())
            {
                // Check if type has any methods with [Test] attribute
                // The TestAttribute is defined inside the test classes
                var testMethods = type.GetMethods()
                    .Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TestAttribute"))
                    .ToList();

                if (testMethods.Count == 0)
                    continue;

                Console.WriteLine($"Running tests from {type.Name}...");
                Console.WriteLine();

                // Create instance
                object instance;
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [ERROR] Failed to create instance: {ex.Message}");
                    failed++;
                    continue;
                }

                // Run each test method
                foreach (var method in testMethods)
                {
                    try
                    {
                        method.Invoke(instance, null);
                        Console.WriteLine($"  [PASS] {method.Name}");
                        passed++;
                    }
                    catch (TargetInvocationException ex)
                    {
                        var inner = ex.InnerException;
                        if (inner != null)
                        {
                            Console.WriteLine($"  [FAIL] {method.Name}: {inner.Message}");
                        }
                        else
                        {
                            Console.WriteLine($"  [FAIL] {method.Name}: {ex.Message}");
                        }
                        failed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [FAIL] {method.Name}: {ex.GetType().Name}: {ex.Message}");
                        failed++;
                    }
                }

                Console.WriteLine();
            }

            Console.WriteLine("========================================");
            Console.WriteLine($"Results: {passed} passed, {failed} failed");
            Console.WriteLine("========================================");

            return failed > 0 ? 1 : 0;
        }
    }
}
