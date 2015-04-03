#define DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using Dia2Lib;

using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Immutable;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Symbols;

namespace Graphs
{
    /// <summary>
    /// Generates a graph from the "inference log" produced by the ProjectN toolchain, which describes the reason why various
    /// elements (code, data) were included in the final binary.
    /// </summary>
    public class ProjectNInferenceGraph : Graph
    {
        public static bool s_checkedEnabled;
        public static bool s_enabled;

        public static bool Enabled
        {
            get
            {
                //
                // We need Microsoft.Cci.dll and CciExtensions.dll, but since this is not commonly-used
                // functionality we don't want to carry these with every PerfView deployment.  So we disable this
                // feature if these assemblies are not present.
                //
                if (!s_checkedEnabled)
                {
                    if (Type.GetType("Microsoft.Cci.Dummy, Microsoft.Cci, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: false) == null)
                        DebugWriteLine("Microsoft.Cci not present.  Disabling .NET Native binary size analysis.");
                    else if (Type.GetType("Microsoft.Cci.Extensions.CciExtensions, CciExtensions, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", throwOnError: false) == null)
                        DebugWriteLine("CciExtensions not present.  Disabling .NET Native binary size analysis.");
                    else
                        s_enabled = true;

                    s_checkedEnabled = true;
                }
                return s_enabled;
            }
        }

        public ProjectNInferenceGraph(string filePath)
            : base(10000)
        {
            var rootType = CreateType("[root]");
            var rootNode = CreateNode();

            var importer = new InferenceImporter(this, filePath);

            SetNode(rootNode, rootType, 0, importer.GetRoots());

            importer = null;
            RootIndex = rootNode;
            AllowReading();
        }

        private static void DebugWriteLine(string message)
        {
            PerfView.App.CommandProcessor.LogFile.WriteLine(message);
        }

        private static void DebugWriteLine(string format, params object[] args)
        {
            PerfView.App.CommandProcessor.LogFile.WriteLine(format, args);
        }

        private static string GetDisplayName(string name)
        {
            if (name == null)
                return null;

            // For some reason, anything between curly braces does not display in the stack viewer.
            return name.Replace("{", "[").Replace("}", "]");
        }

        class InferenceImporter
        {
            // The maximum number of parents a node may have.  Limiting this greatly reduces the time/memory needed to
            // generate the graph.
            const int MaxParents = 10;

            readonly ProjectNInferenceGraph m_graph;
            PDBImporter m_pdbImporter;

            GrowableArray<NodeIndex> m_rootNodes;
            Dictionary<int, IEnumerable<NodeIndex>> m_pdbChildren = new Dictionary<int, IEnumerable<NodeIndex>>();

            struct Entry
            {
                public object Value;
                public int Parent;
            }
            GrowableArray<Entry> m_entries;
            Dictionary<int, int> m_mergedEntries = new Dictionary<int, int>();
            BitArray m_necessary = new BitArray(1024);

            Dictionary<string, string> m_strings = new Dictionary<string, string>();

            private string Intern(string s)
            {
                string interned;
                if (!m_strings.TryGetValue(s, out interned))
                    m_strings[s] = interned = s;
                return interned;
            }

            public InferenceImporter(ProjectNInferenceGraph graph, string filePath)
            {
                m_graph = graph;

                using (var host = new Host())
                {
                    using (var archive = ZipFile.OpenRead(filePath))
                    {
                        m_pdbImporter = new PDBImporter(graph, archive, host);

                        GrowableArray<Dictionary<string, ZipArchiveEntry>> sections = new GrowableArray<Dictionary<string, ZipArchiveEntry>>();
                        foreach (var entry in archive.Entries)
                        {
                            // Inference log entries are stored as "InferenceLog\NN\filename", where NN is the log segment number.

                            if (entry.FullName.StartsWith("InferenceLog\\", StringComparison.OrdinalIgnoreCase))
                            {
                                string fileName = entry.Name;
                                int sectionNumber = int.Parse(Path.GetFileName(Path.GetDirectoryName(entry.FullName)));

                                var section = sections.Get(sectionNumber);
                                if (section == null)
                                {
                                    section = new Dictionary<string, ZipArchiveEntry>();
                                    sections.Set(sectionNumber, section);
                                }
                                section[fileName] = entry;
                            }
                        }

                        foreach (var section in sections)
                        {
                            ImportInferences(host, section);
                        }
                    }

                    CreateGraphNodes();
                }
            }

            private void ImportInferences(Host host, Dictionary<string, ZipArchiveEntry> section)
            {
                var references = ImportReferences(host, section);

                ValueBuffer values = new ValueBuffer(section["values"], true);
                ValueBuffer parents = new ValueBuffer(section["parents"], true);
                ValueBuffer merged = new ValueBuffer(section["merged"], false);

                BitArray necessary = ImportNecessary(section);

                m_entries.Set(0, new Entry());

                int firstId = m_entries.Count;
                for (int i = 0; values.More; i++)
                {
                    int id = firstId + i;
                    int parent = id - parents.NextInt();
                    object value = references[values.NextInt()];

                    m_entries.Set(id, new Entry()
                    {
                        Parent = parent,
                        Value = value, 
                    });

                    if (id < necessary.Count && necessary[id])
                    {
                        m_necessary.Length = Math.Max(m_necessary.Length, m_entries.UnderlyingArray.Length);
                        m_necessary[id] = true;

                        var pdbNodes = m_pdbImporter.GetNodes(value);
                        if (pdbNodes != null)
                            m_pdbChildren[id] = pdbNodes;
                    }

                    int mergedOffset = merged.NextInt();
                    if (mergedOffset != 0)
                        m_mergedEntries[id] = id + mergedOffset;
                }
            }

            private BitArray ImportNecessary(Dictionary<string, ZipArchiveEntry> section)
            {
                var necessaryEntry = section["necessary"];
                byte[] necessaryBytes = new byte[necessaryEntry.Length];
                using (var s = necessaryEntry.Open())
                    s.Read(necessaryBytes, 0, necessaryBytes.Length);
                return new BitArray(necessaryBytes);
            }

            private List<object> ImportReferences(Host host, Dictionary<string, ZipArchiveEntry> section)
            {
                List<string> strings = new List<string>();
                ValueBuffer stringBuffer = new ValueBuffer(section["strings"], false);

                while (stringBuffer.More)
                    strings.Add(stringBuffer.NextString());

                var entry = section["references"];
                using (var s = new StreamWithLength(entry.Open(), entry.Length))
                {
                    var assembly = (IAssembly)host.LoadUnitFrom(s);

                    var moduleClass = assembly.GetAllTypes().Where(type => type.Name.Value == "<Module>").First();

                    var method = moduleClass.Methods.Where(m => m.Name.Value == "Values").First();
                    var body = host.Copy(method).Body; // Make a copy to avoid caching the operations in the PE reader

                    List<object> values = new List<object>(body.Operations.Count());

                    foreach (var operation in body.Operations)
                    {
                        if (operation.Value is int)
                            values.Add(strings[(int)operation.Value]);
                        else
                            values.Add(operation.Value);
                    }

                    return values;
                }
            }

            internal GrowableArray<NodeIndex> GetRoots()
            {
                return m_rootNodes;
            }

            private void CreateGraphNodes()
            {
                m_strings = null;
                m_pdbImporter = null;

                GrowableArray<NodeIndex> nodes = new GrowableArray<NodeIndex>(m_entries.Count);
                GrowableArray<GrowableArray<NodeIndex>> childNodes = new GrowableArray<GrowableArray<NodeIndex>>(m_entries.Count);
                PopulateNodes(ref nodes, ref childNodes);

                m_mergedEntries = null;

                GrowableArray<NodeTypeIndex> nodeTypes;
                PopulateNodeTypes(nodes, out nodeTypes);

                m_entries = default(GrowableArray<Entry>);

                var notFoundType = m_graph.CreateType("[not found in binary]");
                var notFoundNode = m_graph.CreateNode();
                m_graph.SetNode(notFoundNode, notFoundType, 0, new GrowableArray<NodeIndex>());

                for (int id = 0; id < nodes.Count; id++)
                {
                    if (nodes[id] != (NodeIndex)0)
                    {
                        var children = childNodes.Get(id);

                        var pdbChildren = m_pdbChildren.GetOrDefault(id);
                        if (pdbChildren != null)
                            children.AddRange(pdbChildren);
                        else if (m_necessary[id])
                            children.Add(notFoundNode);

                        m_graph.SetNode(nodes[id], nodeTypes[id], 0, children);

                        m_pdbChildren.Remove(id);
                        childNodes.Set(id, default(GrowableArray<NodeIndex>));
                    }
                }
            }

            public void PopulateNodes(ref GrowableArray<NodeIndex> nodes, ref GrowableArray<GrowableArray<NodeIndex>> childNodes)
            {
                nodes = new GrowableArray<NodeIndex>(m_entries.Count);
                childNodes = new GrowableArray<GrowableArray<NodeIndex>>(m_entries.Count);

                Stack<int> stack = new Stack<int>();
                for (int id = 0; id < m_entries.Count; id++)
                    if (m_necessary[id])
                        stack.Push(id);

                while (stack.Count > 0)
                {
                    int id = stack.Pop();

                    if (nodes.Get(id) == (NodeIndex)0)
                    {
                        var node = m_graph.CreateNode();
                        nodes.Set(id, node);

                        Entry entry = m_entries[id];
                        if (entry.Parent == 0)
                        {
                            m_rootNodes.Add(node);
                        }
                        else
                        {
                            var parent = entry.Parent;
                            var children = childNodes.Get(parent);
                            children.Add(node);

                            int parentCount = 0;
                            do
                            {
                                stack.Push(parent);
                                childNodes.Set(parent, children);
                                parentCount++;
                            }
                            while (
                                parentCount < MaxParents && 
                                m_mergedEntries.TryGetValue(parent, out parent) && 
                                parent != entry.Parent && parent != 0);
                        }
                    }
                }
            }

            void PopulateNodeTypes(GrowableArray<NodeIndex> nodes, out GrowableArray<NodeTypeIndex> nodeTypes)
            {
                Dictionary<string, NodeTypeIndex> nodeTypesByName = new Dictionary<string, NodeTypeIndex>();

                nodeTypes = new GrowableArray<NodeTypeIndex>(nodes.Count);

                for (int id = 0; id < nodes.Count; id++)
                {
                    if (nodes[id] != (NodeIndex)0)
                    {
                        string value = GetCciDisplayName(m_entries[id].Value);

                        NodeTypeIndex nodeType = nodeTypesByName.GetOrDefault(value, NodeTypeIndex.Invalid);
                        if (nodeType == NodeTypeIndex.Invalid)
                            nodeType = nodeTypesByName[value] = m_graph.CreateType(value);

                        nodeTypes.Set(id, nodeType);
                    }
                }
            }

            private static string GetCciDisplayName(object value)
            {
                return GetDisplayName(GetCciNodeName(value));
            }

            private static string GetCciNodeName(object value)
            {
                if (value == null)
                    return "[NULL]";

                if (value is string)
                    return (string)value;

                if (value is ITypeReference)
                    return TypeHelper.GetTypeName((ITypeReference)value);

                if (value is ITypeMemberReference)
                    return MemberHelper.GetMemberSignature((ITypeMemberReference)value);

                if (value is ICustomAttribute)
                    return "attribute:" + GetCciNodeName(((ICustomAttribute)value).Type);

                if (Debugger.IsAttached)
                    Debugger.Break();

                return "[" + value.ToString() + "]";
            }
        }

        class PDBImporter
        {
            readonly ProjectNInferenceGraph m_graph;
            readonly Host m_host;

            readonly Dictionary<uint, HashSet<IMethodReference>> m_rvaToMethodMap;
            readonly Dictionary<IMethodReference, HashSet<uint>> m_genericMethodToRvaMap;
            readonly Dictionary<string, uint> m_udtToTypeIndexMap;
            readonly Dictionary<uint, ITypeReference> m_typeIndexToTypeMap;

            readonly Dictionary<ITypeReference, List<NodeIndex>> m_typeToNodeMap = new Dictionary<ITypeReference, List<NodeIndex>>(TypeReferenceOnlyComparer.Instance);
            readonly Dictionary<IFieldReference, List<NodeIndex>> m_fieldToNodeMap = new Dictionary<IFieldReference, List<NodeIndex>>(FieldReferenceOnlyComparer.Instance);
            readonly Dictionary<IMethodReference, List<NodeIndex>> m_methodToNodeMap = new Dictionary<IMethodReference, List<NodeIndex>>(MethodReferenceOnlyComparer.Instance);

            readonly ITokenDecoder m_tokenDecoder;
            readonly ITypeDefinition m_systemCanonType;

            readonly SymbolModule m_symbolModule;

            public PDBImporter(ProjectNInferenceGraph graph, ZipArchive archive, Host host)
            {
                m_graph = graph;
                m_host = host;

                using (var symReader = new SymbolReader(PerfView.App.CommandProcessor.LogFile))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.FullName.StartsWith("Output\\", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!Path.GetExtension(entry.Name).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
                            continue;
                        MemoryStream pdbStream = new MemoryStream();
                        using (var compressed = entry.Open())
                            compressed.CopyTo(pdbStream);

                        m_symbolModule = symReader.OpenSymbolFile(entry.Name, pdbStream);

                        byte[] ilImage = m_symbolModule.GetEmbeddedILImage();
                        if (ilImage != null)
                        {
                            m_rvaToMethodMap = new Dictionary<uint, HashSet<IMethodReference>>();
                            m_genericMethodToRvaMap = new Dictionary<IMethodReference, HashSet<uint>>(MethodReferenceOnlyComparer.Instance);
                            m_udtToTypeIndexMap = new Dictionary<string, uint>();
                            m_typeIndexToTypeMap = new Dictionary<uint, ITypeReference>();


                            var assembly = m_host.LoadUnitFrom(new MemoryStream(ilImage, writable: false));
                            m_tokenDecoder = (ITokenDecoder)assembly;

                            m_systemCanonType = new PlatformType(m_host).CreateReference((IAssemblyReference)assembly, "System", "__Canon").ResolvedType;

                            PopulateTypeIndexMap();
                            PopulateFuncTokenMap();
                            PouplateTypeTokenMap();

                            PopulateSharedGenerics();

                            BuildGraphNodes(m_symbolModule);
                        }

                        m_symbolModule = null;
                    }
                }
            }

            private void PopulateTypeIndexMap()
            {
                foreach (var sym in m_symbolModule.GlobalSymbol.GetChildren(SymTagEnum.SymTagUDT))
                    m_udtToTypeIndexMap[sym.Name] = sym.Id;
            }

            public IEnumerable<NodeIndex> GetNodes(object cciValue)
            {
                if (cciValue == null)
                    return null;

                IEnumerable<NodeIndex> result = null;

                if (cciValue is ITypeReference)
                    result = m_typeToNodeMap.GetOrDefault((ITypeReference)cciValue)
                        ?? m_typeToNodeMap.GetOrDefault(Canonize((ITypeReference)cciValue));
                else if (cciValue is IMethodReference)
                    result = m_methodToNodeMap.GetOrDefault((IMethodReference)cciValue)
                        ?? m_methodToNodeMap.GetOrDefault(Canonize((IMethodReference)cciValue));
                else if (cciValue is IFieldReference)
                    result = m_fieldToNodeMap.GetOrDefault((IFieldReference)cciValue);

                return result;
            }

            void BuildGraphNodes(SymbolModule symbolModule)
            {
                var symbols =
                    (from sym in m_symbolModule.GlobalSymbol.GetChildren(SymTagEnum.SymTagPublicSymbol)
                     let rva = sym.RVA
                     where rva != 0 && sym.Length != 0
                     orderby rva
                     select sym
                     ).ToArray();

                for (int i = 0; i < symbols.Length; i++)
                {
                    var length = symbols[i].Length;
                    if (i + 1 < symbols.Length)
                    {
                        if (Symbol.InSameSection(symbols[i], symbols[i + 1]))
                            length = symbols[i + 1].RVA - symbols[i].RVA;
                    }

                    string name = symbols[i].UndecoratedName;
                    NodeIndex node = m_graph.CreateNode();
                    NodeTypeIndex nodeType = m_graph.CreateType(GetDisplayName(name), m_symbolModule.SymbolFilePath);
                    m_graph.SetNode(node, nodeType, (int)length, new GrowableArray<NodeIndex>());

                    var found = FindMethods(symbols[i].RVA, node) || FindFieldForSymbol(name, node) || FindTypeForVTableSymbol(name, node);
                }
            }

            private bool FindFieldForSymbol(string symName, NodeIndex node)
            {
                bool found = false;
                int dot = symName.LastIndexOf('.');
                if (dot > 0)
                {
                    string typeSymName = symName.Substring(0, dot);
                    string fieldName = symName.Substring(dot + 1);

                    uint typeSymId;
                    m_udtToTypeIndexMap.TryGetValue(typeSymName, out typeSymId);
                    if (typeSymId != 0)
                    {
                        var type = m_typeIndexToTypeMap.GetOrDefault(typeSymId);
                        if (type != null)
                        {
                            var field = type.ResolvedType.Fields.FirstOrDefault(f => f.Name.Value == fieldName);
                            if (field != null)
                            {
                                m_fieldToNodeMap.GetOrNew(field).Add(node);
                                found = true;
                            }
                        }
                    }
                }
                return found;
            }

            private bool FindTypeForVTableSymbol(string undecoratedName, NodeIndex node)
            {
                bool found = false;

                // Example VTable symbol names:
                // StringBuilder = L"const System::Text::StringBuilder::`vftable'"
                // Action<String> = L"const System::Action$1<System::String>::`vftable'"
                // int[] = L"EEType__Int32[]"
                const string vtable = "::`vftable'";
                const string @const = "const ";
                const string EEType = "EEType__";
                string typeSymName;

                if (undecoratedName.StartsWith(@const) && undecoratedName.EndsWith(vtable))
                    typeSymName = undecoratedName.Substring(@const.Length, undecoratedName.Length - @const.Length - vtable.Length);
                else if (undecoratedName.StartsWith(EEType))
                    typeSymName = undecoratedName.Substring(EEType.Length);
                else
                    typeSymName = undecoratedName;

                typeSymName = typeSymName.Replace("> ", ">").Replace("Boxed_", "").Replace("class ", "");

                uint typeSymId;
                m_udtToTypeIndexMap.TryGetValue(typeSymName, out typeSymId);
                if (typeSymId != 0)
                {
                    var type = m_typeIndexToTypeMap.GetOrDefault(typeSymId);
                    if (type != null)
                    {
                        m_typeToNodeMap.GetOrNew(type).Add(node);
                        found = true;
                    }
                }

                return found;
            }

            private bool FindMethods(uint rva, NodeIndex node)
            {
                bool found = false;
                var methods = m_rvaToMethodMap.GetOrDefault(rva) ?? Enumerable.Empty<IMethodReference>();
                foreach (var method in methods)
                {
                    found = true;
                    m_methodToNodeMap.GetOrNew(method).Add(node);
                }
                return found;
            }

            internal enum CorTokenType
            {
                mdtModule = 0x00000000,
                mdtTypeRef = 0x01000000,
                mdtTypeDef = 0x02000000,
                mdtFieldDef = 0x04000000,
                mdtMethodDef = 0x06000000,
                mdtParamDef = 0x08000000,
                mdtInterfaceImpl = 0x09000000,
                mdtMemberRef = 0x0a000000,
                mdtCustomAttribute = 0x0c000000,
                mdtPermission = 0x0e000000,
                mdtSignature = 0x11000000,
                mdtEvent = 0x14000000,
                mdtProperty = 0x17000000,
                mdtModuleRef = 0x1a000000,
                mdtTypeSpec = 0x1b000000,
                mdtAssembly = 0x20000000,
                mdtAssemblyRef = 0x23000000,
                mdtFile = 0x26000000,
                mdtExportedType = 0x27000000,
                mdtManifestResource = 0x28000000,
                mdtGenericParam = 0x2a000000,
                mdtMethodSpec = 0x2b000000,
                mdtGenericParamConstraint = 0x2c000000,

                mdtString = 0x70000000,
                mdtName = 0x71000000,
                mdtBaseType = 0x72000000,
            }


            private unsafe void PouplateTypeTokenMap()
            {
                byte[] buf = m_symbolModule.GetTypeMDTokenMap();
                fixed (byte* pBuf = buf)
                {
                    uint entryCount = *(uint*)pBuf;
                    byte* pEntries = pBuf + 4;
                    byte* pTypeData = pEntries + (entryCount * 8);

                    for (uint i = 0; i < entryCount; i++)
                    {
                        uint* pEntry = (uint*)(pEntries + (i * 8));
                        uint typeIndex = pEntry[0];
                        uint offset = pEntry[1];

                        SigParser sig;
                        if ((offset & 0x80000000) != 0)
                        {
                            byte[] compactSig = new byte[4];
                            compactSig[0] = (byte)((offset >> 24) & 0x7f);
                            compactSig[1] = (byte)(offset >> 16);
                            compactSig[2] = (byte)(offset >> 8);
                            compactSig[3] = (byte)(offset);
                            sig = new SigParser(compactSig, compactSig.Length);
                        }
                        else
                        {
                            int adjustedOffset = (int)(offset + (entryCount * 8 + 4));
                            sig = new SigParser(buf, buf.Length - adjustedOffset, adjustedOffset);
                        }

                        var typeRef = GetType(ref sig, m_tokenDecoder);
                        m_typeIndexToTypeMap[typeIndex] = typeRef;
                    }
                }
            }

            private unsafe void PopulateFuncTokenMap()
            {
                byte[] buf = m_symbolModule.GetFuncMDTokenMap();
                fixed (byte* pBuf = buf)
                {
                    uint entryCount = *(uint*)pBuf;
                    byte* pEntries = pBuf + 4;
                    byte* pMethodData = pEntries + (entryCount * 8);

                    for (uint i = 0; i < entryCount; i++)
                    {
                        uint* pEntry = (uint*)(pEntries + (i * 8));
                        uint rva = pEntry[0];
                        uint offset = pEntry[1];

                        if ((offset & 0x80000000) != 0)
                        {
                            uint token = (offset & 0x00ffffff) | (uint)CorTokenType.mdtMethodDef;
                            var methodRef = (IMethodReference)m_tokenDecoder.GetObjectForToken(token);
                            if (methodRef != null)
                                AddMethod(methodRef, rva);
                        }
                        else
                        {
                            byte* pMethod = pMethodData + pEntry[1];
                            int genericArgCount = (*(int*)pMethod) >> 24;
                            uint token = (*(uint*)pMethod & 0x00ffffff) | (uint)CorTokenType.mdtMethodDef;

                            var methodDef = (IMethodDefinition)m_tokenDecoder.GetObjectForToken(token);
                            if (methodDef != null)
                            {
                                if (genericArgCount == 0)
                                {
                                    AddMethod(methodDef, rva);
                                }
                                else
                                {
                                    byte* pSig = pMethod + 4;
                                    int sigOffset = (int)(pSig - pBuf);
                                    AddMethod(
                                        GetMethodWithTypeArgs(
                                            methodDef,
                                            GetTypeArguments(genericArgCount, new SigParser(buf, buf.Length - sigOffset, sigOffset), m_tokenDecoder)),
                                        rva);
                                }
                            }
                        }
                    }
                }
            }

            void AddMethod(IMethodReference methodRef, uint rva)
            {
                m_rvaToMethodMap.GetOrNew(rva).Add(methodRef);
                if (methodRef is IGenericMethodInstanceReference || methodRef.ContainingType.IsConstructedGenericType())
                    m_genericMethodToRvaMap.GetOrNew(methodRef).Add(rva);
            }


            HashSet<ITypeReference> m_nonSharedArgTypes = new HashSet<ITypeReference>(TypeReferenceOnlyComparer.Instance);


            void PopulateSharedGenerics()
            {
                foreach (var methods in m_rvaToMethodMap.Where(p => p.Key != 0).Select(p => p.Value))
                {
                    foreach (var method in methods)
                    {
                        var methodInstance = method as IGenericMethodInstanceReference;
                        if (methodInstance != null)
                            FindNonSharedGenericArgumentTypes(methodInstance.GenericArguments);

                        if (method.ContainingType.IsConstructedGenericType())
                            FindNonSharedGenericArgumentTypes(method.ContainingType.GenericTypeArguments());
                    }
                }

                foreach (var type in m_typeIndexToTypeMap.Values)
                {
                    var canonizedType = Canonize(type);
                    var canonizedMethods = canonizedType.ResolvedType.Methods.Select(m => Canonize(m));

                    var methods = type.ResolvedType.Methods.Zip(canonizedMethods, 
                        (uncanonizedMethod, canonizedMethod) => new { uncanonizedMethod, canonizedMethod });

                    foreach (var method in methods)
                        if (method.canonizedMethod != method.uncanonizedMethod)
                            foreach (var rva in m_genericMethodToRvaMap.GetOrNew(method.canonizedMethod))
                                AddMethod(method.uncanonizedMethod, rva);
                }

                foreach (var methods in m_rvaToMethodMap.Values)
                {
                    foreach (var method in methods)
                    {
                        var canonizedMethod = Canonize(method);
                        if (canonizedMethod != method)
                            foreach (var rva in m_genericMethodToRvaMap.GetOrNew(canonizedMethod))
                                AddMethod(method, rva);
                    }
                }
            }

            private ITypeReference Canonize(ITypeReference typeRef)
            {
                if (!typeRef.IsConstructedGenericType())
                    return typeRef;

                var genericType = typeRef.GetGenericTypeDefinition();
                if (genericType.ResolvedType.IsDummy())
                    return typeRef;

                var args = typeRef.GenericTypeArguments().Select(arg => CanonizeArgument(arg));
                return genericType.CreateConstructedGenericType(m_host, args.ToArray());
            }

            private IMethodReference Canonize(IMethodReference methodRef)
            {
                var methodInst = methodRef as IGenericMethodInstanceReference;
                if (methodInst != null)
                {
                    var genericMethod = methodInst.GenericMethod;
                    if (!(genericMethod.ResolvedMethod is Dummy))
                    {
                        var args = methodInst.GenericArguments.Select(arg => CanonizeArgument(arg));
                        try
                        {
                            return genericMethod.CreateConstructedGenericMethod(m_host, args.ToArray());
                        }
                        catch (Exception e)
                        {
                            DebugWriteLine("Error creating instantiation of " + genericMethod);
                            DebugWriteLine(e.ToString());
                            return Dummy.MethodReference;
                        }
                    }
                }

                return methodRef;
            }

            private ITypeReference CanonizeArgument(ITypeReference typeRef)
            {
                var openType = typeRef.IsConstructedGenericType() ? typeRef.GetGenericTypeDefinition() : typeRef;

                if (!m_nonSharedArgTypes.Contains(openType))
                    return m_systemCanonType;

                return Canonize(typeRef);
            }

            private void FindNonSharedGenericArgumentTypes(IEnumerable<ITypeReference> args)
            {
                foreach (var type in args)
                {
                    if (!type.IsConstructedGenericType())
                    {
                        if (!TypeReferenceOnlyComparer.Instance.Equals(type, m_systemCanonType))
                            m_nonSharedArgTypes.Add(type);
                    }
                    else
                    {
                        m_nonSharedArgTypes.Add(type.GetGenericTypeDefinition());
                        FindNonSharedGenericArgumentTypes(type.GenericTypeArguments());
                    }
                }
            }

            static uint GetToken(ref SigParser sig)
            {
                int typeDefOrRefEncoded;
                sig.GetData(out typeDefOrRefEncoded);

                int token;
                int table = typeDefOrRefEncoded & 3;
                if (table == 0)
                    token = (int)CorTokenType.mdtTypeDef | (typeDefOrRefEncoded >> 2);
                else if (table == 1)
                    token = (int)CorTokenType.mdtTypeRef | (typeDefOrRefEncoded >> 2);
                else if (table == 2)
                    token = (int)CorTokenType.mdtTypeSpec | (typeDefOrRefEncoded >> 2);
                else
                    throw new InvalidOperationException("Bad typeDefOrRefEncoded " + typeDefOrRefEncoded);

                return (uint)token;
            }

            ITypeReference GetType(ref SigParser sig, ITokenDecoder tokenDecoder)
            {
                int etype;
                sig.GetElemType(out etype);

                switch ((ClrElementType)etype)
                {
                    case ClrElementType.Boolean: return m_host.PlatformType.SystemBoolean;
                    case ClrElementType.Char: return m_host.PlatformType.SystemChar;
                    case ClrElementType.Int8: return m_host.PlatformType.SystemInt8;
                    case ClrElementType.UInt8: return m_host.PlatformType.SystemUInt8;
                    case ClrElementType.Int16: return m_host.PlatformType.SystemInt16;
                    case ClrElementType.UInt16: return m_host.PlatformType.SystemUInt16;
                    case ClrElementType.Int32: return m_host.PlatformType.SystemInt32;
                    case ClrElementType.UInt32: return m_host.PlatformType.SystemUInt32;
                    case ClrElementType.Int64: return m_host.PlatformType.SystemInt64;
                    case ClrElementType.UInt64: return m_host.PlatformType.SystemUInt64;
                    case ClrElementType.Float: return m_host.PlatformType.SystemFloat32;
                    case ClrElementType.Double: return m_host.PlatformType.SystemFloat64;
                    case ClrElementType.String: return m_host.PlatformType.SystemString;
                    case ClrElementType.NativeInt: return m_host.PlatformType.SystemIntPtr;
                    case ClrElementType.NativeUInt: return m_host.PlatformType.SystemUIntPtr;
                    case ClrElementType.Object: return m_host.PlatformType.SystemObject;

                    case ClrElementType.Struct:
                        return (ITypeReference)tokenDecoder.GetObjectForToken(GetToken(ref sig)) ?? Dummy.TypeReference;
                    case ClrElementType.Class:
                        return (ITypeReference)tokenDecoder.GetObjectForToken(GetToken(ref sig)) ?? Dummy.TypeReference;

                    case ClrElementType.Pointer:
                        return GetType(ref sig, tokenDecoder).CreatePointerType(m_host);

                    case ClrElementType.SZArray:
                        return GetType(ref sig, tokenDecoder).CreateArrayType(m_host);

                    case ClrElementType.Array:
                        ITypeReference elementType = GetType(ref sig, tokenDecoder);

                        int rank;
                        sig.GetData(out rank);

                        int numSizes;
                        sig.GetData(out numSizes);
                        List<ulong> sizes = new List<ulong>(numSizes);
                        for (int i = 0; i < numSizes; i++)
                        {
                            int size;
                            sig.GetData(out size);
                            sizes.Add((ulong)size);
                        }

                        int numLoBounds;
                        sig.GetData(out numLoBounds);
                        List<int> loBounds = new List<int>(numLoBounds);
                        for (int i = 0; i < numLoBounds; i++)
                        {
                            int loBound;
                            sig.GetData(out loBound);
                            loBounds.Add(loBound);
                        }

                        return new MatrixTypeReference()
                        {
                            InternFactory = m_host.InternFactory,
                            ElementType = GetType(ref sig, tokenDecoder),
                            Rank = (uint)rank,
                            Sizes = sizes,
                            LowerBounds = loBounds,
                            IsFrozen = true
                        };

                    case (ClrElementType)0x15: //GENERICINST
                        int classOrValueType;
                        sig.GetElemType(out classOrValueType);

                        var genericType = (tokenDecoder.GetObjectForToken(GetToken(ref sig)) as INamedTypeReference) ?? Dummy.NamedTypeReference;

                        int argCount;
                        sig.GetData(out argCount);
                        List<ITypeReference> args = new List<ITypeReference>(argCount);
                        for (int i = 0; i < argCount; i++)
                            args.Add(GetType(ref sig, tokenDecoder));

                        if (!genericType.ResolvedType.IsDummy() && genericType.IsGenericTypeDefinition())
                            return genericType.CreateConstructedGenericType(m_host, args.ToArray());
                        else
                            return Dummy.TypeReference;

                    case ClrElementType.FunctionPointer:
                        Debug.Assert(false, "Function pointers not yet supported");
                        break;
                }

                throw new InvalidOperationException("Unknknown element type " + etype);
            }



            ITypeReference[] GetTypeArguments(int count, SigParser sig, ITokenDecoder tokenDecoder)
            {
                var result = new ITypeReference[count];
                for (int i = 0; i < count; i++)
                    result[i] = GetType(ref sig, tokenDecoder);
                return result;
            }

            IMethodReference GetMethodWithTypeArgs(IMethodDefinition methodDef, ITypeReference[] typeArguments)
            {
                try
                {
                    if (methodDef.ContainingType.IsGenericTypeDefinition())
                    {
                        var typeParamCount = methodDef.ContainingType.GenericTypeParameters().Count();
                        var typeInstance = ((INamedTypeReference)methodDef.ContainingType).CreateConstructedGenericType(m_host, typeArguments.Take(typeParamCount).ToArray());
                        methodDef = typeInstance.ResolvedType.Methods.OfType<ISpecializedMethodDefinition>().First(method => method.UnspecializedVersion.Equals(methodDef));
                        typeArguments = typeArguments.Skip(typeParamCount).ToArray();
                    }

                    if (typeArguments.Length > 0)
                    {
                        methodDef = methodDef.CreateConstructedGenericMethod(m_host, typeArguments).ResolvedMethod;
                    }
                }
                catch (Exception e)
                {
                    DebugWriteLine("Exception while resolving instantiation of " + methodDef);
                    DebugWriteLine(e.ToString());
                    return Dummy.MethodReference;
                }

                return methodDef;
            }
        }

        /// <summary>
        /// A CCI host implementation with some customizations to make life easier for us.
        /// </summary>
        class Host : MetadataReaderHost, IDisposable
        {
            readonly PeReader m_reader;
            readonly Dictionary<string, AssemblyIdentity> m_loadedAssemblyIdentities = new Dictionary<string, AssemblyIdentity>(StringComparer.InvariantCultureIgnoreCase);

            //
            // CCI "document" type from a Stream.  This allows an assembly to be loaded directly from a Stream, without round-tripping through
            // a file on disk.
            //
            class StreamDocument : IBinaryDocument
            {
                readonly Stream m_stream;
                readonly string m_location;
                readonly IName m_name;

                public StreamDocument(Stream stream, string location, IMetadataHost host)
                {
                    m_stream = stream;
                    m_location = location;
                    m_name = host.NameTable.GetNameFor(Path.GetFileName(location));
                }

                public uint Length
                {
                    get
                    {
                        return (uint)m_stream.Length;
                    }
                }

                public string Location
                {
                    get
                    {
                        return m_location;
                    }
                }

                public IName Name
                {
                    get
                    {
                        return m_name;
                    }
                }

                public Stream Stream
                {
                    get
                    {
                        return m_stream;
                    }
                }
            }

            public Host() : base(new Microsoft.Cci.NameTable(), new InternFactory(), 0, null, false)
            {
                m_reader = new PeReader(this);
            }

            public IMethodDefinition Copy(IMethodDefinition method)
            {
                return new MetadataDeepCopier(this).Copy(method);
            }

            public override IUnit LoadUnitFrom(string location)
            {
                // We don't want to accidentally load from "ordinary" on-disk assemblies.  All of our assemblies should
                // come from the .PDB files, or the inference log, both of which are loaded via a Stream, below.
                throw new NotSupportedException();
            }

            public IUnit LoadUnitFrom(Stream stream)
            {
                IUnit unit = m_reader.OpenModule(new StreamDocument(stream, "dummy.location", this));
                IAssembly assembly = unit as IAssembly;
                if (assembly != null)
                {
                    RegisterAsLatest(assembly);
                    m_loadedAssemblyIdentities[assembly.AssemblyIdentity.Name.Value] = assembly.AssemblyIdentity;
                    return assembly;
                }
                return unit;
            }

            // Disable probing the GAC for assembly references.
            protected override AssemblyIdentity Probe(string probeDir, AssemblyIdentity referencedAssembly)
            {
                return referencedAssembly;
            }

            // Disable probing the GAC for assembly references.
            public override AssemblyIdentity ProbeAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly)
            {
                return referencedAssembly;
            }

            public override void ResolvingAssemblyReference(IUnit referringUnit, AssemblyIdentity referencedAssembly)
            {
            }

            public override void ResolvingModuleReference(IUnit referringUnit, ModuleIdentity referencedModule)
            {
            }

            public override AssemblyIdentity UnifyAssembly(AssemblyIdentity assemblyIdentity)
            {
                AssemblyIdentity result;
                if (!m_loadedAssemblyIdentities.TryGetValue(assemblyIdentity.Name.Value, out result))
                    result = assemblyIdentity;
                return result;
            }

#if false  // Unused and probably broken because of renaming
            private AssemblyIdentity m_coreAssemblyIdentity;
            protected override AssemblyIdentity GetCoreAssemblySymbolicIdentity()
            {
                if (m_coreAssemblyIdentity == null)
                    m_coreAssemblyIdentity = new AssemblyIdentity(
                       NameTable.GetNameFor("corefx"), string.Empty, new Version(0, 0, 0, 0), Enumerable.Empty<byte>(), string.Empty);
                return m_coreAssemblyIdentity;
            }
#endif

            public override IBinaryDocumentMemoryBlock OpenBinaryDocument(IBinaryDocument sourceDocument)
            {
                try
                {
                    UnmanagedBinaryMemoryBlock memoryBlock;
                    StreamDocument streamDoc = sourceDocument as StreamDocument;
                    if (streamDoc != null)
                        memoryBlock = UnmanagedBinaryMemoryBlock.CreateUnmanagedBinaryMemoryBlock(streamDoc.Stream, sourceDocument);
                    else
                        memoryBlock = UnmanagedBinaryMemoryBlock.CreateUnmanagedBinaryMemoryBlock(sourceDocument.Location, sourceDocument);
                    disposableObjectAllocatedByThisHost.Add(memoryBlock);
                    return memoryBlock;
                }
                catch (IOException)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Wraps a Stream, but with a specified Length.  This is needed because the streams produced by ZipArchive don't
        /// know their own lengths.
        /// </summary>
        class StreamWithLength : Stream
        {
            readonly Stream m_stream;
            readonly long m_length;

            public StreamWithLength(Stream stream, long length) { m_stream = stream; m_length = length; }

            public override long Length { get { return m_length; } }

            public override bool CanRead { get { return m_stream.CanRead; } }
            public override bool CanWrite { get { return m_stream.CanWrite; } }
            public override bool CanSeek { get { return m_stream.CanSeek; } }
            public override bool CanTimeout { get { return m_stream.CanTimeout; } }
            public override long Position { get { return m_stream.Position; } set { m_stream.Position = value; } }
            public override void Flush() { m_stream.Flush(); }
            public override long Seek(long offset, SeekOrigin origin) { return m_stream.Seek(offset, origin); }
            public override void SetLength(long value) { m_stream.SetLength(value); }
            public override int Read(byte[] buffer, int offset, int count) { return m_stream.Read(buffer, offset, count); }
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { return m_stream.BeginRead(buffer, offset, count, callback, state); }
            public override int EndRead(IAsyncResult asyncResult) { return m_stream.EndRead(asyncResult); }
            public override int ReadByte() { return m_stream.ReadByte(); }
            public override void Write(byte[] buffer, int offset, int count) { m_stream.Write(buffer, offset, count); }
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) { return m_stream.BeginWrite(buffer, offset, count, callback, state); }
            public override void EndWrite(IAsyncResult asyncResult) { m_stream.EndWrite(asyncResult); }
            public override void WriteByte(byte value) { m_stream.WriteByte(value); }
            public override int ReadTimeout { get { return m_stream.ReadTimeout; } set { m_stream.ReadTimeout = value; } }
            public override int WriteTimeout { get { return m_stream.WriteTimeout; } set { m_stream.WriteTimeout = value; } }
            public override void Close() { m_stream.Close(); }
            protected override void Dispose(bool disposing) { if (disposing) m_stream.Dispose(); }
        }


        /// <summary>
        /// Reads from a buffer full of compressed integers.
        /// </summary>
        class ValueBuffer
        {
            byte[] m_buffer;
            int m_position;

            readonly bool m_deltas;
            int m_previousInt;

            public ValueBuffer(ZipArchiveEntry entry, bool deltas)
            {
                m_deltas = deltas;
                m_buffer = new byte[entry.Length];
                using (Stream stream = entry.Open())
                    stream.Read(m_buffer, 0, m_buffer.Length);
            }

            public bool More { get { return m_position < m_buffer.Length; } }

            public int NextInt()
            {
                int ret = 0;
                byte b = m_buffer[m_position++];
                ret = b << 25 >> 25;
                for (; ;)
                {
                    if ((b & 0x80) == 0)
                        break;
                    ret <<= 7;
                    b = m_buffer[m_position++];
                    ret += (b & 0x7f);
                }
                if (m_deltas)
                {
                    if ((ret & 1) == 1)
                        ret = -(ret >> 1);
                    else
                        ret = ret >> 1;

                    ret += m_previousInt;
                    m_previousInt = ret;
                }
                return ret;
            }

            public string NextString()
            {
                int byteCount = NextInt();
                string ret = Encoding.UTF8.GetString(m_buffer, m_position, byteCount);
                m_position += byteCount;
                return ret;
            }
        }
    }

    static class ProjectNInferenceGraphExtensions
    {
        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey k, TValue defaultValue = default(TValue))
        {
            TValue v;
            if (!d.TryGetValue(k, out v))
                return defaultValue;
            return v;
        }

        public static TValue GetOrNew<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey k)
            where TValue : new()
        {
            TValue v;
            if (!d.TryGetValue(k, out v))
                d[k] = v = new TValue();
            return v;
        }
    }
}