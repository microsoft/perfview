using Graphs;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Address = System.UInt64;

public class PdbScopeMemoryGraph : MemoryGraph
{
    public PdbScopeMemoryGraph(string pdbScopeFile)
        : base(10000)
    {
        var children = new GrowableArray<NodeIndex>(1000);
        Dictionary<string, NodeTypeIndex> knownTypes = new Dictionary<string, NodeTypeIndex>(1000);

        XmlReaderSettings settings = new XmlReaderSettings() { IgnoreWhitespace = true, IgnoreComments = true };
        using (XmlReader reader = XmlReader.Create(pdbScopeFile, settings))
        {
            int foundBestRoot = int.MaxValue;       // it is zero when when we find the best root. 
            Address imageBase = 0;
            uint sizeOfImageHeader = 0;
            Address lastAddress = 0;
            int badValues = 0;

            Queue<Section> sections = new Queue<Section>();
            Address prevAddr = 0;
            Address expectedAddr = 0;
            RootIndex = NodeIndex.Invalid;
            NodeIndex firstNodeIndex = NodeIndex.Invalid;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "Section":
                            {
                                Section section = new Section();
                                Address.TryParse(reader.GetAttribute("Start"), out section.Start);
                                section.Size = uint.Parse(reader.GetAttribute("Size"));
                                section.Name = reader.GetAttribute("Name");
                                sections.Enqueue(section);
                                lastAddress = Math.Max(lastAddress, section.EndRoundedUpToPage);
                            }
                            break;
                        case "Module":
                            if (imageBase == 0)
                            {
                                Address.TryParse(reader.GetAttribute("Base"), out imageBase);
                                sizeOfImageHeader = 1024;        // We are using the file size number 
                                NodeIndex nodeIndex = GetNodeIndex(imageBase);
                                NodeTypeIndex typeIndex = CreateType("Image Header");
                                children.Clear();
                                SetNode(nodeIndex, typeIndex, (int)sizeOfImageHeader, children);
                                expectedAddr = imageBase + 0x1000;

                                DebugWriteLine("Loading Module Map table used to decode $N symbol prefixes.");
                                string dllFilePath = reader.GetAttribute("FilePath");
                                if (dllFilePath != null)
                                {
                                    LoadModuleMap(dllFilePath, pdbScopeFile);
                                }
                                else
                                {
                                    DebugWriteLine("Could not find path to original DLL being analyzed.");
                                }

                                if (m_moduleMap != null)
                                {
                                    DebugWriteLine("Loaded Module Map of " + m_moduleMap.Count + " Project N style IL modules to unmangled $N_ prefixes.");
                                }
                                else
                                {
                                    DebugWriteLine("Warning: No Module Map Found: $N_ prefixes will not be unmangled.");
                                }
                            }
                            break;
                        case "ObjectTypes":
                        case "Type":
                        case "Dicectory":
                        case "Sections":
                        case "PdbscopeReport":
                        case "Symbols":
                        case "SourceFiles":
                        case "File":
                            break;
                        case "Symbol":
                            string addrStr = reader.GetAttribute("addr");
                            Address addr;
                            if (addrStr != null && Address.TryParse(addrStr, NumberStyles.AllowHexSpecifier, null, out addr))
                            {
                                if (addr < lastAddress)
                                {
                                    // Get Size
                                    string sizeStr = reader.GetAttribute("size");
                                    uint size = 0;
                                    if (sizeStr != null)
                                    {
                                        uint.TryParse(sizeStr, out size);
                                    }

                                    // Get Children 
                                    children.Clear();
                                    string to = reader.GetAttribute("to");
                                    if (to != null)
                                    {
                                        GetChildrenForAddresses(ref children, to);
                                    }

                                    // Get Name, make a type out of it
                                    string name;
                                    NodeTypeIndex typeIndex = GetTypeForXmlElement(knownTypes, reader, size, out name);

                                    // Currently PdbScope files have extra information lines where it shows the different generic instantiations associated
                                    // with a given symbol.  These ways have the same address as the previous entry and have no size (size will be 0) so
                                    // we filter these lines out with the following condition. 
                                    if (prevAddr != addr || size != 0)
                                    {
                                        prevAddr = addr;

                                        if (addr < expectedAddr)
                                        {
                                            DebugWriteLine(string.Format("Got Address {0:x} which is less than the expected address {1:x}.  Discarding {2}",
                                                addr, expectedAddr, name));
                                            badValues++;
                                            if (50 < badValues)
                                            {
                                                throw new ApplicationException("Too many cases where the addresses were not ascending in the file");
                                            }

                                            continue;           // discard
                                        }
                                        /*** We want to make sure we account for all bytes, so log when we see gaps ***/
                                        // If we don't match see if it is because of section boundary. 
                                        if (addr != expectedAddr)
                                        {
                                            EmitNodesForGaps(sections, expectedAddr, addr);
                                        }

                                        expectedAddr = addr + size;

                                        NodeIndex nodeIndex = GetNodeIndex((Address)addr);
                                        SetNode(nodeIndex, typeIndex, (int)size, children);

                                        // See if this is a good root
                                        if (foundBestRoot != 0 && name != null)
                                        {
                                            if (name == "RHBinder__ShimExeMain")
                                            {
                                                RootIndex = nodeIndex;
                                                foundBestRoot = 0;
                                            }
                                            else if (0 < foundBestRoot && name.Contains("ILT$Main"))
                                            {
                                                RootIndex = nodeIndex;
                                                foundBestRoot = 1;
                                            }
                                            else if (1 < foundBestRoot && name.Contains("DllMainCRTStartup"))
                                            {
                                                RootIndex = nodeIndex;
                                                foundBestRoot = 1;
                                            }
                                            else if (2 < foundBestRoot && name.Contains("Main"))
                                            {
                                                RootIndex = nodeIndex;
                                                foundBestRoot = 2;
                                            }
                                        }

                                        // Remember first node.
                                        if (firstNodeIndex == NodeIndex.Invalid)
                                        {
                                            firstNodeIndex = nodeIndex;
                                        }
                                    }
                                }
                                else
                                {
                                    DebugWriteLine(string.Format("Warning Discarding Symbol node {0:x} outside the last address in the image {1:x}", addr, lastAddress));
                                }
                            }
                            else
                            {
                                DebugWriteLine("Error: symbol without addr");
                            }

                            break;
                        default:
                            DebugWriteLine(string.Format("Skipping unknown element {0}", reader.Name));
                            break;
                    }
                }
            }

            EmitNodesForGaps(sections, expectedAddr, lastAddress);

            if (RootIndex == NodeIndex.Invalid)
            {
                RootIndex = firstNodeIndex;
            }

            DebugWriteLine(string.Format("Image Base {0:x} LastAddress {1:x}", imageBase, lastAddress));
            DebugWriteLine(string.Format("Total Virtual Size {0} ({0:x})", lastAddress - imageBase));
            DebugWriteLine(string.Format("Total File Size    {0} ({0:x})", TotalSize));
        }
        AllowReading();
    }

    #region private

    /// <summary>
    /// Loads the Module map associate with the EXE/DLL dllFilePath.    This allows us to decode $N_ prefixes in project N Dlls.   
    /// </summary>
    private void LoadModuleMap(string dllFilePath, string pdbScopeFilePath)
    {
        try
        {
            string originalDllFilePath = dllFilePath;
            DebugWriteLine("Module being analyzed: " + dllFilePath);

            // PdbScope XML is user-controlled and the FilePath attribute can name an
            // arbitrary path (including UNC) that would otherwise reach File.Exists /
            // SymbolReader and leak NTLM credentials.  Only trust paths that normalize
            // to a real file under the XML file's own directory.  If validation fails
            // we simply skip the module -- no "next to the binary" fallback, because a
            // malicious PdbScope file should not be able to coerce us into resolving
            // anything outside what the XML explicitly and safely references.
            if (!TryResolveTrustedFilePath(dllFilePath, pdbScopeFilePath, out dllFilePath))
            {
                DebugWriteLine("Ignoring untrusted module path from PdbScope XML: " + originalDllFilePath);
                return;
            }

            if (!File.Exists(dllFilePath))
            {
                DebugWriteLine("Error: The DLL not found at " + dllFilePath + ".");
                return;
            }

            DebugWriteLine("Found DLL/EXE file " + dllFilePath);
            using (var symReader = new SymbolReader(PerfView.App.CommandProcessor.LogFile))
            {
                // TryResolveTrustedFilePath has already validated that dllFilePath is a
                // real local file under the PdbScope XML's own directory, so the DLL
                // itself is trusted.  SymbolReader will derive additional probe paths
                // from that DLL (PDB next to the DLL, the PE debug directory's embedded
                // pdbFileName, the user's symbol cache, configured symbol servers); the
                // PE-embedded path in particular could be an arbitrary string baked into
                // the DLL at build time.  Allow any probe path that is not obviously
                // remote so legitimate local lookups succeed while still refusing UNC /
                // URI / NT-namespace paths that would trigger an SMB authentication
                // probe and leak NTLM credentials.  (The default behavior when
                // SecurityCheck is null is to reject every probe -- we have to set
                // something for symbol resolution to work at all.)
                symReader.SecurityCheck = name => !PathUtilities.IsRemotePath(name);

                string pdbFilePath = symReader.FindSymbolFilePathForModule(dllFilePath);
                if (pdbFilePath == null)
                {
                    DebugWriteLine("Error: The PDB for DLL: " + dllFilePath + " not found.");
                    return;
                }

                DebugWriteLine("Found pdb file " + pdbFilePath);
                var module = symReader.OpenNativeSymbolFile(pdbFilePath);
                m_moduleMap = module.GetMergedAssembliesMap();
            }
        }
        catch (Exception e)
        {
            DebugWriteLine("Error: reading PDB file " + e.Message);
        }
    }

    internal static bool TryResolveTrustedFilePath(string filePath, string pdbScopeFilePath, out string trustedFilePath)
    {
        trustedFilePath = null;

        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(pdbScopeFilePath) || PathUtilities.IsRemotePath(filePath))
        {
            return false;
        }

        try
        {
            string pdbScopeDirectory = Path.GetDirectoryName(Path.GetFullPath(pdbScopeFilePath));

            // PdbScope XML is user-controlled. Only trust paths that normalize under the XML file's directory.
            string fullFilePath = Path.IsPathRooted(filePath)
                ? Path.GetFullPath(filePath)
                : Path.GetFullPath(Path.Combine(pdbScopeDirectory, filePath));

            if (PathUtilities.IsRemotePath(fullFilePath) || !PathUtilities.IsPathWithinDirectory(fullFilePath, pdbScopeDirectory))
            {
                return false;
            }

            trustedFilePath = fullFilePath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Figures out the best name for an unknown region (a gap).  'sections' is the SORTED list of PE file sections
    /// so we can give the gaps the best names and to deal with the padding at the end of sections.   
    /// </summary>
    private void EmitNodesForGaps(Queue<Section> sections, Address startGap, Address endGap)
    {
        // Any sections completely before the gap we can just skip since we process gaps in order and the sections are in order.  
        while (sections.Count > 0 && sections.Peek().EndRoundedUpToPage <= startGap)
        {
            var section = sections.Dequeue();
            Debug.Assert(sections.Count == 0 || sections.Peek().Start == section.EndRoundedUpToPage);
        }

        while (sections.Count > 0)
        {
            var section = sections.Peek();

            // Anything between the last section and the start of this one we log as unknown.  
            if (startGap < section.Start)
            {
                var subRegionEnd = Math.Min(section.Start, endGap);
                EmitRegion(startGap, subRegionEnd, "UNKNOWN");
                startGap = subRegionEnd;

            }
            if (endGap <= startGap)
            {
                return;
            }

            // Do we overlap with the section at all?
            if (startGap < section.End)
            {
                Debug.Assert(section.Start <= startGap);        // We ensure this with conditions above.  
                var subRegionEnd = Math.Min(endGap, section.End);
                EmitRegion(startGap, subRegionEnd, "Section " + section.Name + " UNKNOWN");
                startGap = subRegionEnd;
            }
            if (endGap <= startGap)
            {
                return;
            }

            // Do we overlap with the section padding region?
            if (startGap < section.EndRoundedUpToPage)
            {
                Debug.Assert(section.End <= startGap);        // We ensured this with second conditions above.  
                var subRegionEnd = Math.Min(endGap, section.EndRoundedUpToPage);

                // File padding is smaller (512 bytes) than virtual memory padding we emit the size of the file not the virtual memory
                var paddingSize = subRegionEnd - startGap;
                var filePaddingSize = paddingSize % 512;
                if (filePaddingSize != 512)
                {
                    EmitRegion(startGap, startGap + filePaddingSize, "Section " + section.Name + " padding");
                }

                startGap = subRegionEnd;
            }
            if (endGap <= startGap)
            {
                return;
            }

            sections.Dequeue();
        }
        if (endGap <= startGap)
        {
            return;
        }
        // Anything after the last section is unknown.  
        EmitRegion(startGap, endGap, "UNKNOWN");
    }

    private void EmitRegion(Address start, Address end, string name)
    {
        uint size = (uint)(end - start);
        if (name == "UNKNOWN")
        {
            DebugWriteLine(string.Format("UNKNOWN GAP In symbols from {0:x} of size {1:x}", start, size));
        }

        SetNode(GetNodeIndex(start), CreateType(name), (int)size, new GrowableArray<NodeIndex>());
    }

    private struct Section
    {
        public string Name;
        public Address Start;
        public uint Size;
        public Address End { get { return Start + Size; } }
        public Address EndRoundedUpToPage { get { return (End + 0xFFF) & ~((Address)0xFFF); } }      // round up to 4096 byte boundary
    }

    private void DebugWriteLine(string message)
    {
        PerfView.App.CommandProcessor.LogFile.WriteLine(message);
    }

    private void GetChildrenForAddresses(ref GrowableArray<NodeIndex> children, string to)
    {
        // TODO inefficient
        foreach (var numStr in to.Split(' '))
        {
            int num;
            if (int.TryParse(numStr, NumberStyles.HexNumber, null, out num))
            {
                children.Add(GetNodeIndex((Address)num));
            }
        }
    }

    private NodeTypeIndex GetTypeForXmlElement(Dictionary<string, NodeTypeIndex> knownTypes, XmlReader reader, uint size, out string rawName)
    {
        rawName = reader.GetAttribute("name");
        string fullName = rawName ?? "";
        bool showSize = false;

        bool isRuntimeData = false;
        string moduleName = null;
        if (m_moduleMap != null)
        {
            if (rawName.Contains("::"))
            {
                moduleName = "CoreLib";
            }
            else
            {
                isRuntimeData = true;
            }

            if (0 <= fullName.IndexOf('$'))
            {
                Regex prefixMatch = new Regex(@"\$(\d+)_");
                fullName = prefixMatch.Replace(fullName, delegate (Match m)
                {
                    var original = m.Groups[1].Value;
                    var moduleIndex = int.Parse(original);

                    string name = null;
                    string fullAssemblyName;
                    if (m_moduleMap.TryGetValue(moduleIndex, out fullAssemblyName))
                    {
                        var assemblyName = new AssemblyName(fullAssemblyName);
                        name = assemblyName.Name;
                    }
                    if (name == null)
                    {
                        return "$" + original + "_";
                    }

                    if (m.Groups[1].Index == 0)
                    {
                        moduleName = name;
                        return "";
                    }
                    else
                    {
                        var lessThanIdx = fullName.IndexOf('<');
                        if (lessThanIdx < 0 || m.Groups[1].Index < lessThanIdx)
                        {
                            moduleName = null;
                        }

                        return name + "!";
                    }
                });
            }
        }

        // TODO HACK, viewer treats { } specially (removes them) figure out where/why... 
        fullName = fullName.Replace('{', '[');
        fullName = fullName.Replace('}', ']');

        var _idx = fullName.LastIndexOf('_');
        if (0 <= _idx)
        {

            if (IsHexNumSuffix(fullName, _idx + 1))
            {
                fullName = fullName.Substring(0, _idx);
            }
        }

        if (fullName.StartsWith("FrozenData"))
        {
            showSize = true;
            fullName = "FrozenData";
        }
        else if (fullName.StartsWith("FrozenString"))
        {
            showSize = true;
            fullName = "FrozenString";
        }
        else if (fullName.StartsWith("InitData"))
        {
            fullName = "InitData";
        }
        else if (fullName.StartsWith("OpaqueDataBlob"))
        {
            showSize = true;
            fullName = "OpaqueDataBlob (Probably Reflection MetaData)";
        }
        else if (fullName.StartsWith("InterfaceDispatchCell"))
        {
            _idx = fullName.IndexOf('_');
            if (0 <= _idx)
            {
                var end = fullName.IndexOf("_slot", _idx + 1);
                if (end < 0)
                {
                    end = fullName.Length;
                }

                fullName = "InterfaceDispatchCell" + fullName.Substring(_idx, end - _idx);
            }
        }
        else if (fullName.StartsWith("Unknown"))
        {
            fullName = Regex.Replace(fullName, @"Unknown\d*", "Unknown");
        }
        else if (fullName.StartsWith("__imp__#"))
        {
            fullName = "__imp__#NNN";
        }

        if (isRuntimeData)
        {
            fullName = "RUNTIME_DATA " + fullName;
        }

        string src = reader.GetAttribute("src");
        if (src != null && src != "<stdin>")
        {
            fullName += "@" + Path.GetFileName(src);
        }

        if (1000 < size || 100 < size && showSize)
        {
            if (100000 < size)
            {
                fullName += " (>100K)";
            }
            else if (10000 < size)
            {
                fullName += " (>10K)";
            }
            else if (1000 < size)
            {
                fullName += " (>1K)";
            }
            else
            {
                fullName += " (>100)";
            }
        }

        string tag = reader.GetAttribute("tag");

        // TODO FIX NOW remove ASAP (10/14) THIS IS A HACK imports with large sizes are wrong, fix PdbScope and remove
        //if (10000 < size && tag == "import")
        //{
        //    fullName = "UNKNOWN";
        //    tag = null;
        //}

        if (tag == null)
        {
            tag = "other";
        }

        fullName += " #" + tag + "#";

        NodeTypeIndex ret;
        if (!knownTypes.TryGetValue(fullName, out ret))
        {
            ret = CreateType(fullName, moduleName, (int)size);
            knownTypes[fullName] = ret;
        }
        return ret;
    }

    private bool IsHexNumSuffix(string str, int startIndex)
    {
        // We need it to have at least 3 digits
        if ((str.Length - startIndex) < 3)
        {
            return false;
        }

        while (startIndex < str.Length)
        {
            char c = str[startIndex];
            if (!(Char.IsDigit(c) || ('A' <= c && c <= 'F')))
            {
                return false;
            }

            startIndex++;
        }
        return true;
    }

    private Dictionary<int, string> m_moduleMap;      // TODO FIX NOW not needed after PdbScope is fixed.  
    #endregion
}


/// <summary>
/// Knows how to read the project N metadata.csv file format
/// </summary>
public class ProjectNMetaDataLogReader
{
    public ProjectNMetaDataLogReader() { }
    public MemoryGraph Read(string projectNMetaDataLog)
    {
        m_graph = new MemoryGraph(1000);
        m_knownTypes = new Dictionary<string, NodeTypeIndex>(1000);

        using (TextReader reader = File.OpenText(projectNMetaDataLog))
        {
            int lineNum = 0;
            string line;
            try
            {
                // Skip headers line
                line = reader.ReadLine();
                lineNum++;
                if (line == null)
                {
                    return null;
                }

                LineData lineData = new LineData();
                for (; ; )
                {
                    line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    lineNum++;

                    Match m = Regex.Match(line, "^(\\S+), +(\\S+), +\"(.*)\", +\"(.*?)\"$");
                    if (m.Success)
                    {
                        uint newOffset = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber) & 0xFFFFFF;
                        lineData.Size = (int)(newOffset - lineData.Offset);
                        if (lineNum > 2)
                        {
                            NodeIndex nodeIndex = AddLineData(ref lineData);
                            if (lineNum == 3)
                            {
                                m_graph.RootIndex = nodeIndex;
                            }
                        }

                        lineData.Offset = newOffset;
                        lineData.Kind = m.Groups[2].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        lineData.Name = m.Groups[3].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        lineData.Children.Clear();
                        if (m.Groups[4].Length > 0)
                        {
                            string[] handleStrs = m.Groups[4].Value.Split(' ');
                            foreach (var handleStr in handleStrs)
                            {
                                lineData.Children.Add(m_graph.GetNodeIndex(uint.Parse(handleStr, NumberStyles.HexNumber) & 0xFFFFFF));
                            }
                        }
                    }
                    else
                    {
                        throw new FileFormatException();
                    }
                }
                if (lineNum > 1)
                {
                    lineData.Size = 16;  // TODO Better estimate. 
                    AddLineData(ref lineData);
                }
            }
            catch (Exception e)
            {
                throw new FileFormatException("Error on line number " + lineNum + "  " + e.Message);
            }
        }
        m_graph.AllowReading();
        return m_graph;
    }

    private NodeIndex AddLineData(ref LineData lineData)
    {
        NodeIndex nodeIndex = m_graph.GetNodeIndex(lineData.Offset);
        NodeTypeIndex nodeType = GetType(lineData.Kind + " " + lineData.Name, lineData.Size);
        m_graph.SetNode(nodeIndex, nodeType, lineData.Size, lineData.Children);
        return nodeIndex;
    }

    #region private
    private struct LineData
    {
        public int Size;
        public uint Offset;
        public string Kind;
        public string Name;
        public GrowableArray<NodeIndex> Children;
    }

    private NodeTypeIndex GetType(string name, int size = -1)
    {
        NodeTypeIndex ret;
        if (!m_knownTypes.TryGetValue(name, out ret))
        {
            ret = m_graph.CreateType(name, null, size);
            m_knownTypes.Add(name, ret);
        }
        return ret;
    }

    private MemoryGraph m_graph;
    private Dictionary<string, NodeTypeIndex> m_knownTypes;
    #endregion
}
