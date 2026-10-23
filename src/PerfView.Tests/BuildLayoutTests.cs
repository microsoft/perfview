using Azure.Identity;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace PerfViewTests
{
    public class BuildLayoutTests
    {
        [Fact]
        public void ConfigurationRedirectsPackagesButNotFrameworkAssemblies()
        {
            string applicationDirectory = Path.GetDirectoryName(typeof(PerfView.Startup).Assembly.Location);
            var configuration = XDocument.Load(typeof(PerfView.Startup).Assembly.Location + ".config");
            XNamespace assemblyNamespace = "urn:schemas-microsoft-com:asm.v1";
            var redirects = configuration.Descendants(assemblyNamespace + "dependentAssembly")
                .ToDictionary(
                    assembly => (string)assembly.Element(assemblyNamespace + "assemblyIdentity").Attribute("name"),
                    assembly => (string)assembly.Element(assemblyNamespace + "bindingRedirect").Attribute("newVersion"));

            foreach (string dependency in new[]
            {
                "System.Threading.Tasks.Extensions",
                "Microsoft.Bcl.AsyncInterfaces",
                "System.Memory",
                "System.Text.Json",
                "Microsoft.Extensions.Hosting.Abstractions"
            })
            {
                string assemblyPath = Path.Combine(applicationDirectory, dependency + ".dll");
                Assert.Equal(AssemblyName.GetAssemblyName(assemblyPath).Version.ToString(), redirects[dependency]);
            }

            foreach (string dependency in new[] { "System.Net.Http", "System.Runtime", "System.IO" })
            {
                Assert.DoesNotContain(dependency, redirects.Keys);
            }
        }

        [Fact]
        public void LocalDependenciesUsePerfViewBindingRedirects()
        {
            string applicationDirectory = Path.GetDirectoryName(typeof(BuildLayoutTests).Assembly.Location);
            string assemblyPath = Path.Combine(applicationDirectory, "System.Threading.Tasks.Extensions.dll");
            string supportDirectory = Path.Combine(Path.GetTempPath(), "PerfView-BuildLayoutTests-" + Guid.NewGuid());
            Directory.CreateDirectory(supportDirectory);
            AppDomain domain = null;
            try
            {
                File.Copy(assemblyPath, Path.Combine(supportDirectory, Path.GetFileName(assemblyPath)));
                domain = AppDomain.CreateDomain("BuildLayoutTests", null, new AppDomainSetup
                {
                    ApplicationBase = applicationDirectory,
                    ConfigurationFile = typeof(PerfView.Startup).Assembly.Location + ".config"
                });
                var host = (ResolutionTestHost)domain.CreateInstanceAndUnwrap(
                    typeof(ResolutionTestHost).Assembly.FullName, typeof(ResolutionTestHost).FullName);

                Assert.Equal(new[] { assemblyPath }, host.LoadCredential(supportDirectory));
            }
            finally
            {
                if (domain != null)
                {
                    AppDomain.Unload(domain);
                }
                Directory.Delete(supportDirectory, true);
            }
        }

        /// <summary>
        /// Exercises the build's PerfView.exe.config instead of the test host's binding redirects.
        /// </summary>
        public sealed class ResolutionTestHost : MarshalByRefObject
        {
            public string[] LoadCredential(string supportDirectory)
            {
                global::Utilities.SupportFiles.SetSupportFilesDir(supportDirectory);
                global::Utilities.SupportFiles.UnpackResourcesIfNeeded();
                Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System.Threading.Tasks.Extensions.dll"));

                CreateCredential();

                return AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == "System.Threading.Tasks.Extensions")
                    .Select(assembly => assembly.Location)
                    .ToArray();
            }

            #region private
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void CreateCredential()
            {
                new ChainedTokenCredential(new AzureCliCredential());
            }
            #endregion
        }
    }
}
