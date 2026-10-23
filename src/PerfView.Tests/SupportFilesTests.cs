using Azure.Identity;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace PerfViewTests
{
    public class SupportFilesTests
    {
        [Fact]
        public void ResolvingOlderReferenceReusesAlreadyLoadedAssembly()
        {
            string applicationDirectory = Path.GetDirectoryName(typeof(SupportFilesTests).Assembly.Location);
            string assemblyPath = Path.Combine(applicationDirectory, "System.Threading.Tasks.Extensions.dll");
            string supportDirectory = Path.Combine(Path.GetTempPath(), "PerfView-SupportFilesTests-" + Guid.NewGuid());
            Directory.CreateDirectory(supportDirectory);
            AppDomain domain = null;
            try
            {
                File.Copy(assemblyPath, Path.Combine(supportDirectory, Path.GetFileName(assemblyPath)));
                domain = AppDomain.CreateDomain("SupportFilesTests", null, new AppDomainSetup
                {
                    ApplicationBase = applicationDirectory,
                    ConfigurationFile = Path.Combine(supportDirectory, "NoBindingRedirects.config")
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
        /// Exercises PerfView's resolver without the test host's automatic binding redirects.
        /// </summary>
        public sealed class ResolutionTestHost : MarshalByRefObject
        {
            public string[] LoadCredential(string supportDirectory)
            {
                global::Utilities.SupportFiles.SetSupportFilesDir(supportDirectory);
                global::Utilities.SupportFiles.UnpackResourcesIfNeeded();
                Assembly.Load("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

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
