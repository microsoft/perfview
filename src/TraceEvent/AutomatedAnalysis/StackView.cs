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
    public class StackView
    {
        private static readonly char[] SymbolSeparator = new char[] { '!' };

        private TraceLog _traceLog;
        private StackSource _rawStackSource;
        private SymbolReader _symbolReader;
        private CallTree _callTree;
        private List<CallTreeNodeBase> _byName;
        private HashSet<string> _resolvedSymbolModules = new HashSet<string>();

        public StackView(TraceLog traceLog, StackSource stackSource, SymbolReader symbolReader)
        {
            _traceLog = traceLog;
            _rawStackSource = stackSource;
            _symbolReader = symbolReader;
            LookupWarmNGENSymbols();
        }

        public CallTree CallTree
        {
            get
            {
                if (_callTree == null)
                {
                    FilterStackSource filterStackSource = new FilterStackSource(new FilterParams(), _rawStackSource, ScalingPolicyKind.ScaleToData);
                    _callTree = new CallTree(ScalingPolicyKind.ScaleToData)
                    {
                        StackSource = filterStackSource
                    };
                }
                return _callTree;
            }
        }

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
        public CallTreeNode GetCallers(string focusNodeName)
        {
            var focusNode = FindNodeByName(focusNodeName);
            return AggregateCallTreeNode.CallerTree(focusNode);
        }
        public CallTreeNode GetCallees(string focusNodeName)
        {
            var focusNode = FindNodeByName(focusNodeName);
            return AggregateCallTreeNode.CalleeTree(focusNode);
        }

        public CallTreeNodeBase GetCallTreeNode(string symbolName)
        {
            string[] symbolParts = symbolName.Split(SymbolSeparator);
            if (symbolParts.Length != 2)
            {
                return null;
            }

            // Try to get the call tree node.
            CallTreeNodeBase node = FindNodeByName(Regex.Escape(symbolName));

            // Check to see if the node matches.
            if (node.Name.StartsWith(symbolName, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            // Check to see if we should attempt to load symbols.
            if (!_resolvedSymbolModules.Contains(symbolParts[0]))
            {
                // Look for an unresolved symbols node for the module.
                string unresolvedSymbolsNodeName = symbolParts[0] + "!?";
                node = FindNodeByName(unresolvedSymbolsNodeName);
                if (node.Name.Equals(unresolvedSymbolsNodeName, StringComparison.OrdinalIgnoreCase))
                {
                    // Symbols haven't been resolved yet.  Try to resolve them now.
                    TraceModuleFile moduleFile = _traceLog.ModuleFiles.Where(m => m.Name.Equals(symbolParts[0], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (moduleFile != null)
                    {
                        // Special handling for NGEN images.
                        if(symbolParts[0].EndsWith(".ni", StringComparison.OrdinalIgnoreCase))
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
                        InvalidateCachedStructures();
                    }
                }

                // Mark the module as resolved so that we don't try again.
                _resolvedSymbolModules.Add(symbolParts[0]);

                // Try to get the call tree node one more time.
                node = FindNodeByName(Regex.Escape(symbolName));

                // Check to see if the node matches.
                if (node.Name.StartsWith(symbolName, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            return null;
        }

        private void LookupWarmNGENSymbols()
        {
            TraceEventStackSource asTraceEventStackSource = GetTraceEventStackSource(_rawStackSource);
            if (asTraceEventStackSource == null)
            {
                return;
            }

            SymbolReaderOptions savedOptions = _symbolReader.Options;
            try
            {
                // NGEN PDBs (even those not yet produced) are considered to be in the cache.
                _symbolReader.Options = SymbolReaderOptions.CacheOnly;

                // Resolve all NGEN images.
                asTraceEventStackSource.LookupWarmSymbols(1, _symbolReader, _rawStackSource, s => s.Name.EndsWith(".ni", StringComparison.OrdinalIgnoreCase));

                // Invalidate cached data structures to finish resolving symbols.
                InvalidateCachedStructures();
            }
            finally
            {
                _symbolReader.Options = savedOptions;
            }
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
