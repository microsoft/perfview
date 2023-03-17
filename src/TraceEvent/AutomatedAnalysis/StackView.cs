using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public delegate FilterStackSource FilterDelegate(StackSource stackSource, ProcessIndex processIndex);

    /// <summary>
    /// A view into a set of aggregated stacks.
    /// </summary>
    public class StackView
    {
        private static readonly char[] SymbolSeparator = new char[] { '!' };

        private TraceLog _traceLog;
        private StackSource _rawStackSource;
        private ProcessIndex _processIndex;
        private SymbolReader _symbolReader;
        private FilterDelegate _filterDelegate;
        private CallTree _callTree;
        private List<CallTreeNodeBase> _byName;
        private HashSet<string> _resolvedSymbolModules = new HashSet<string>();


        /// <summary>
        /// Create a new instance of StackView for the specified source.
        /// </summary>
        /// <param name="traceLog">Optional: The TraceLog associated with the StackSource.</param>
        /// <param name="stackSource">The source of the stack data.</param>
        /// <param name="processIndex">Optional: The index of the process that the stacks belong to.</param>
        /// <param name="symbolReader">Optional: A symbol reader that can be used to lookup symbols.</param>
        public StackView(TraceLog traceLog, StackSource stackSource, ProcessIndex processIndex, SymbolReader symbolReader, FilterDelegate filterDelegate = null)
        {
            _traceLog = traceLog;
            _rawStackSource = stackSource;
            _processIndex = processIndex;
            _symbolReader = symbolReader;
            _filterDelegate = filterDelegate;
            if (_filterDelegate == null)
            {
                _filterDelegate = (source, index) =>
                {
                    return new FilterStackSource(new FilterParams(), source, ScalingPolicyKind.ScaleToData);
                };
            }
        }

        /// <summary>
        /// The call tree representing all stacks in the view.
        /// </summary>
        public CallTree CallTree
        {
            get
            {
                if (_callTree == null)
                {
                    _callTree = new CallTree(ScalingPolicyKind.ScaleToData)
                    {
                        StackSource = _filterDelegate(_rawStackSource, _processIndex)
                    };
                }
                return _callTree;
            }
        }

        /// <summary>
        /// All nodes in the view ordered by exclusive metric.
        /// </summary>
        private IEnumerable<CallTreeNodeBase> ByName
        {
            get
            {
                if (_byName == null)
                {
                    _byName = CallTree.ByIDSortedExclusiveMetric();
                }

                return _byName;
            }
        }

        /// <summary>
        /// Find a node.
        /// </summary>
        /// <param name="nodeNamePat">The regex pattern for the node name.</param>
        /// <returns>The requested node, or the root node if requested not found.</returns>
        public CallTreeNodeBase FindNodeByName(string nodeNamePat)
        {
            var regEx = new Regex(nodeNamePat, RegexOptions.IgnoreCase);
            foreach (var node in ByName)
            {
                if (regEx.IsMatch(node.Name))
                {
                    return node;
                }
            }
            return CallTree.Root;
        }
        /// <summary>
        /// Get the set of caller nodes for a specified symbol.
        /// </summary>
        /// <param name="symbolName">The symbol.</param>
        public CallTreeNode GetCallers(string symbolName)
        {
            var focusNode = FindNodeByName(symbolName);
            return AggregateCallTreeNode.CallerTree(focusNode);
        }

        /// <summary>
        /// Get the set of callee nodes for a specified symbol.
        /// </summary>
        /// <param name="symbolName">The symbol.</param>
        public CallTreeNode GetCallees(string symbolName)
        {
            var focusNode = FindNodeByName(symbolName);
            return AggregateCallTreeNode.CalleeTree(focusNode);
        }

        /// <summary>
        /// Get the call tree node for the specified symbol.
        /// </summary>
        /// <param name="symbolName">The symbol.</param>
        /// <returns>The call tree node representing the symbol, or null if the symbol is not found.</returns>
        public CallTreeNodeBase GetCallTreeNode(string symbolName)
        {
            string[] symbolParts = symbolName.Split(SymbolSeparator);
            if (symbolParts.Length != 2)
            {
                return null;
            }

            return GetCallTreeNodeImpl(symbolName, symbolParts[0]);
        }

        public CallTreeNodeBase GetCallTreeNode(string moduleName, string methodName)
        {
            return GetCallTreeNodeImpl($"{moduleName}!{methodName}", moduleName);
        }

        private CallTreeNodeBase GetCallTreeNodeImpl(string symbolName, string moduleName)
        {
            // Try to get the call tree node.
            CallTreeNodeBase node = FindNodeByName(Regex.Escape(symbolName));

            // Check to see if the node matches.
            if (SymbolNamesMatch(symbolName, node.Name))
            {
                return node;
            }

            // Check to see if we should attempt to load symbols.
            if (_traceLog != null && _symbolReader != null && !_resolvedSymbolModules.Contains(moduleName))
            {
                // Look for an unresolved symbols node for the module.
                string unresolvedSymbolsNodeName = moduleName + "!?";
                node = FindNodeByName(unresolvedSymbolsNodeName);
                if (node.Name.Equals(unresolvedSymbolsNodeName, StringComparison.OrdinalIgnoreCase))
                {
                    // Symbols haven't been resolved yet.  Try to resolve them now.
                    IEnumerable<TraceModuleFile> moduleFiles;
                    if (_processIndex != ProcessIndex.Invalid)
                    {
                        moduleFiles = _traceLog.Processes[_processIndex].LoadedModules
                            .Where(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.ModuleFile);
                    }
                    else
                    {
                        moduleFiles = _traceLog.ModuleFiles.Where(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                    }
                    foreach(TraceModuleFile moduleFile in moduleFiles)
                    {
                        // Special handling for NGEN images.
                        if(moduleName.EndsWith(".ni", StringComparison.OrdinalIgnoreCase))
                        {
                            SymbolReaderOptions options = _symbolReader.Options;
                            try
                            {
                                _symbolReader.Options = SymbolReaderOptions.CacheOnly;
                                _traceLog.CallStacks.CodeAddresses.LookupSymbolsForModule(_symbolReader, moduleFile);
                            }
                            finally
                            {
                                _symbolReader.Options = options;
                            }
                        }
                        else
                        {
                            _traceLog.CallStacks.CodeAddresses.LookupSymbolsForModule(_symbolReader, moduleFile);
                        }
                    }
                    InvalidateCachedStructures();
                }

                // Mark the module as resolved so that we don't try again.
                _resolvedSymbolModules.Add(moduleName);

                // Try to get the call tree node one more time.
                node = FindNodeByName(Regex.Escape(symbolName));

                // Check to see if the node matches.
                if (SymbolNamesMatch(symbolName, node.Name))
                {
                    return node;
                }
            }

            return null;
        }

        private static bool SymbolNamesMatch(string requestedName, string foundName)
        {
            // Symbol names don't match if either or both are null.
            if (requestedName == null || foundName == null)
            {
                return false;
            }

            // Allow the found name to be of equivalent or greater length than the requested length.
            // Example:
            //  Requested: System.Private.CoreLib.il!System.String.Concat
            //  Found:     System.Private.CoreLib.il!System.String.Concat(class System.String,class System.String)
            if (foundName.Length < requestedName.Length)
            {
                return false;
            }

            // Check for module!methodName equivalence.
            for (int i = 0; i < requestedName.Length; i++)
            {
                if (requestedName[i] != foundName[i])
                {
                    return false;
                }
            }

            // After the module!methodName equivalence check, only allow a '(', which denotes the method signature.
            if (foundName.Length > requestedName.Length)
            {
                if (foundName[requestedName.Length] != '(')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Unwind the wrapped sources to get to a TraceEventStackSource if possible. 
        /// </summary>
        private static TraceEventStackSource GetTraceEventStackSource(StackSource source)
        {
            StackSourceStacks rawSource = source;
            TraceEventStackSource asTraceEventStackSource = null;
            for (; ; )
            {
                asTraceEventStackSource = rawSource as TraceEventStackSource;
                if (asTraceEventStackSource != null)
                {
                    return asTraceEventStackSource;
                }

                var asCopyStackSource = rawSource as CopyStackSource;
                if (asCopyStackSource != null)
                {
                    rawSource = asCopyStackSource.SourceStacks;
                    continue;
                }
                var asStackSource = rawSource as StackSource;
                if (asStackSource != null && asStackSource != asStackSource.BaseStackSource)
                {
                    rawSource = asStackSource.BaseStackSource;
                    continue;
                }
                return null;
            }
        }

        private void InvalidateCachedStructures()
        {
            _byName = null;
            _callTree = null;
        }
    }
}
