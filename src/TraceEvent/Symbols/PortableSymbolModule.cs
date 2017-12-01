using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;


namespace Microsoft.Diagnostics.Symbols
{
    class PortableSymbolModule : ManagedSymbolModule
    {
        public PortableSymbolModule(SymbolReader reader, string pdbFileName) : this(reader, File.Open(pdbFileName, FileMode.Open, FileAccess.Read, FileShare.Read), pdbFileName) { }

        public PortableSymbolModule(SymbolReader reader, Stream stream, string pdbFileName = "") : base(reader, pdbFileName)
        {
            _stream = stream;
            _provider = MetadataReaderProvider.FromPortablePdbStream(_stream);
            _metaData = _provider.GetMetadataReader();

            InitializeFileToUrlMap();
        }

        public override Guid PdbGuid
        {
            get
            {
                // The first 16 bytes are the PDB Guid, the next 4 are the DLL timestamp.  
                var idBytes = _metaData.DebugMetadataHeader.Id;
                byte[] guidBytes = new byte[16];
                idBytes.CopyTo(0, guidBytes, 0, guidBytes.Length);
                return new Guid(guidBytes);
            }
        }

        public override SourceLocation SourceLocationForManagedCode(uint methodMetadataToken, int ilOffset)
        {
            MethodDefinitionHandle methodDefinitionHandle = (MethodDefinitionHandle)MetadataTokens.Handle((int)methodMetadataToken);
            MethodDebugInformationHandle methodDebugInfoHandle = methodDefinitionHandle.ToDebugInformationHandle();
            MethodDebugInformation methodDebugInfo = _metaData.GetMethodDebugInformation(methodDebugInfoHandle);

            int methodToken = MetadataTokens.GetToken(methodDebugInfoHandle.ToDefinitionHandle());
            SequencePoint lastSequencePoint = default(SequencePoint);
            foreach (SequencePoint sequencePoint in methodDebugInfo.GetSequencePoints())
            {
                if (sequencePoint.Offset > ilOffset)
                {
                    if (lastSequencePoint.Document.IsNil)
                        lastSequencePoint = sequencePoint;
                    break;
                }
                lastSequencePoint = sequencePoint;
            }
            if (lastSequencePoint.Document.IsNil)
                return null;
            return new SourceLocation(GetSourceFile(lastSequencePoint.Document), lastSequencePoint.StartLine);
        }

        #region private 

        private string GetUrlForFilePath(string buildTimeFilePath)
        {
            if (_fileToUrlMap != null)
            {
                foreach (Tuple<string, string> map in _fileToUrlMap)
                {
                    string path = map.Item1;
                    string urlReplacement = map.Item2;

                    if (buildTimeFilePath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                    {
                        string tail = buildTimeFilePath.Substring(path.Length, buildTimeFilePath.Length - path.Length).Replace('\\', '/');
                        return urlReplacement.Replace("*", tail);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Looks up SourceLink information (if present) and initializes _fileToUrlMap from it
        /// </summary>  
        private void InitializeFileToUrlMap()
        {
            string sourceLinkJson = GetSourceLinkJson();
            if (sourceLinkJson == null)
                return;

            // TODO this is not right for corner cases (e.g. file paths with " or , } in them)
            Match m = Regex.Match(sourceLinkJson, @"documents.?\s*:\s*{(.*?)}", RegexOptions.Singleline);
            if (m.Success)
            {
                string mappings = m.Groups[1].Value;
                while (!string.IsNullOrWhiteSpace(mappings))
                {
                    m = Regex.Match(m.Groups[1].Value, "^\\s*\"(.*?)\"\\s*:\\s*\"(.*?)\"\\s*,?(.*)", RegexOptions.Singleline);
                    if (m.Success)
                    {
                        if (_fileToUrlMap == null)
                            _fileToUrlMap = new List<Tuple<string, string>>();
                        string pathSpec = m.Groups[1].Value.Replace("\\\\", "\\");
                        if (pathSpec.EndsWith("*"))
                        {
                            pathSpec = pathSpec.Substring(0, pathSpec.Length - 1);      // Remove the *
                            _fileToUrlMap.Add(new Tuple<string, string>(pathSpec, m.Groups[2].Value));
                        }
                        else
                            _log.WriteLine("Warning: {0} does not end in *, skipping this mapping.", pathSpec);
                        mappings = m.Groups[3].Value;
                    }
                    else
                    {
                        _log.WriteLine("Error: Could not parse SourceLink Mapping: {0}", mappings);
                        break;
                    }
                }
            }
            else
                _log.WriteLine("Error: Could not parse SourceLink Json: {0}", sourceLinkJson);
        }

        private string GetSourceLinkJson()
        {
            foreach(CustomDebugInformationHandle customDebugInformationHandle in _metaData.CustomDebugInformation)
            {
                CustomDebugInformation customDebugInformation = _metaData.GetCustomDebugInformation(customDebugInformationHandle);

                EntityHandle parent = customDebugInformation.Parent;
                Guid guid = _metaData.GetGuid(customDebugInformation.Kind);
                if (guid == SourceLinkKind)
                {
                    BlobReader blobReader = _metaData.GetBlobReader(customDebugInformation.Value);
                    var ret =  blobReader.ReadUTF8(blobReader.Length);
                    return ret;
                }
            }

            return null;
        }

        private SourceFile GetSourceFile(DocumentHandle documentHandle)
        {
            return new PortablePdbSourceFile(_metaData.GetDocument(documentHandle), this);
        }

        private class PortablePdbSourceFile : SourceFile
        {
            internal PortablePdbSourceFile(Document sourceFileDocument, PortableSymbolModule portablePdb) : base(portablePdb)
            {
                _sourceFileDocument = sourceFileDocument;
                _portablePdb = portablePdb;

                Guid hashAlgorithmGuid = _portablePdb._metaData.GetGuid(_sourceFileDocument.HashAlgorithm);
                if (hashAlgorithmGuid == HashAlgorithmSha1)
                    _hashAlgorithm = System.Security.Cryptography.SHA1.Create();
                else if (hashAlgorithmGuid == HashAlgorithmSha256)
                    _hashAlgorithm = System.Security.Cryptography.SHA256.Create();
                if (_hashAlgorithm != null)
                    _hash = _portablePdb._metaData.GetBlobBytes(_sourceFileDocument.Hash);

                BuildTimeFilePath = _portablePdb._metaData.GetString(_sourceFileDocument.Name);
                _log.WriteLine("Opened Portable Pdb Source File: {0}", BuildTimeFilePath);
            }

            public override string Url { get { return _portablePdb.GetUrlForFilePath(BuildTimeFilePath); } }

            #region private 
            private static Guid HashAlgorithmSha1 = Guid.Parse("ff1816ec-aa5e-4d10-87f7-6f4963833460");
            private static Guid HashAlgorithmSha256 = Guid.Parse("8829d00f-11b8-4213-878b-770e8597ac16");

            // Fields 
            private Document _sourceFileDocument;
            private PortableSymbolModule _portablePdb;
            #endregion
        }   // Class PortablePdbSourceFile

        private static Guid SourceLinkKind = Guid.Parse("CC110556-A091-4D38-9FEC-25AB9A351A6A");

        // Needed by other things to look up data
        internal MetadataReader _metaData;

        List<Tuple<string, string>> _fileToUrlMap;      // Used by SourceLink to map build paths to URLs (see GetUrlForFilePath)
        MetadataReaderProvider _provider;
        Stream _stream;
        #endregion
    }
}

