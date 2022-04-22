#if NET462 || NETSTANDARD2_0
using System.IO;
using System.Reflection;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// Analyzer resolver that searches the specified directory.
    /// </summary>
    public sealed class DirectoryAnalyzerResolver : AnalyzerResolver
    {
        private static string _baseDirectory;

        /// <summary>
        /// Create an instance of the DirectoryAnalyzerResolver pointing at the specified directory.
        /// </summary>
        /// <param name="baseDirectory">The directory to search for Analyzers.</param>
        public DirectoryAnalyzerResolver(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
        }

        protected internal override void Resolve()
        {
            string[] candidateAssemblies = Directory.GetFiles(_baseDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            foreach (string candidateAssembly in candidateAssemblies)
            {
                Assembly assembly = Assembly.LoadFrom(candidateAssembly);
                if (assembly != null)
                {
                    ConsumeAssembly(assembly);
                }
            }

            Configuration configuration = new Configuration();

            string[] filePaths = Directory.GetFiles(_baseDirectory, "*.config.xml");
            foreach (string filePath in filePaths)
            {
                configuration.AddConfigurationFile(filePath);
            }

            ConsumeConfiguration(configuration);
        }
    }
}
#endif