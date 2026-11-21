using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace TraceEventTests.Regression
{
    internal static class IsolatedTest
    {
        public static void Run(Action test, Type testClass, [CallerMemberName] string method = null)
        {
            string name = testClass.FullName + "." + method;
            const string Variable = "TRACEEVENT_ISOLATED_TEST";
            if (Environment.GetEnvironmentVariable(Variable) == name)
            {
                test();
                return;
            }

            string assembly = Assembly.GetExecutingAssembly().Location;
            var start = new ProcessStartInfo("dotnet",
                "vstest \"" + assembly + "\" /TestAdapterPath:\"" +
                Path.Combine(Path.GetDirectoryName(assembly), "xunit.runner.visualstudio.testadapter.dll") +
                "\" /TestCaseFilter:\"FullyQualifiedName=" + name + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.EnvironmentVariables[Variable] = name;
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    process.WaitForExit();
                    Assert.Fail("Isolated test timed out: " + name);
                }
                Assert.True(process.ExitCode == 0,
                    output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
            }
        }
    }
}
