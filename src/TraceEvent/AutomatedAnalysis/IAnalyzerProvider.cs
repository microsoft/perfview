using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The assembly-level attribute used by Analyzer developers to identify the class that implements IAnalyzerProvider for the assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AnalyzerProviderAttribute : Attribute
    {
        /// <summary>
        /// Create an new instance of AnalyzerProviderAttribute which stores the Type of the IAnalyzerProvider for the assembly.
        /// </summary>
        /// <param name="providerType">The type contained in this assembly that implements IAnalyzerProvider.</param>
        public AnalyzerProviderAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type providerType)
        {
            ProviderType = providerType;
        }

        /// <summary>
        /// The type that implements IAnalyzerProvider.
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type ProviderType { get; }
    }

    /// <summary>
    /// The interface used within Analyzer assemblies to publish the set of Analyzers that are consumable from the assembly.
    /// </summary>
    public interface IAnalyzerProvider
    {
        /// <summary>
        /// Called to provide the list of Analyzers published by the assembly.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Analyzer> GetAnalyzers();
    }
}
