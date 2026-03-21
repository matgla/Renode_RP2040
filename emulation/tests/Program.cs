/**
 * Program.cs
 *
 * Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
 *
 * Distributed under the terms of the MIT License.
 */

using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Antmicro.Renode.Tests
{
    /// <summary>
    /// Simple console test runner for .NET Standard libraries.
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

            // Run all test classes in this assembly
            var assembly = Assembly.GetExecutingAssembly();
            var testTypes = assembly.GetTypes();

            foreach (var type in testTypes)
            {
                if (type.GetCustomAttribute(typeof(FactAttribute)) != null ||
                    type.GetMethods().Any(m => m.GetCustomAttribute(typeof(FactAttribute)) != null))
                {
                    var result = RunTests(type);
                    passed += result.Passed;
                    failed += result.Failed;
                }
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"Results: {passed} passed, {failed} failed");
            Console.WriteLine("========================================");

            return failed > 0 ? 1 : 0;
        }

        private static (int Passed, int Failed) RunTests(Type testClass)
        {
            int passed = 0;
            int failed = 0;

            Console.WriteLine($"Running tests from {testClass.Name}...");

            // Create instance
            object instance;
            try
            {
                instance = Activator.CreateInstance(testClass);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERROR] Failed to create instance: {ex.Message}");
                return (0, 1);
            }

            // Run all test methods
            var methods = testClass.GetMethods()
                .Where(m => m.GetCustomAttribute(typeof(FactAttribute)) != null);

            foreach (var method in methods)
            {
                try
                {
                    method.Invoke(instance, null);
                    Console.WriteLine($"  [PASS] {method.Name}");
                    passed++;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is AssertActualExpectedException)
                {
                    var assertEx = (AssertActualExpectedException)ex.InnerException;
                    Console.WriteLine($"  [FAIL] {method.Name}: {assertEx.Message}");
                    failed++;
                }
                catch (TargetInvocationException ex)
                {
                    Console.WriteLine($"  [FAIL] {method.Name}: {ex.InnerException?.Message ?? ex.Message}");
                    failed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [ERROR] {method.Name}: {ex.Message}");
                    failed++;
                }
            }

            return (passed, failed);
        }
    }
}
