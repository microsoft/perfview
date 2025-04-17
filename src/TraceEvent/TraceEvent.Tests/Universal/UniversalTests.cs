using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;
using System;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class UniversalTests : TestBase
    {
        public UniversalTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void ProcessMappingMetadataParser_Empty()
        {
            string rawJson = @"{}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.Null(metadata);
        }

        [Fact]
        public void PEProcessMappingMetadata_Full()
        {
            string rawJson = @"{""type"": ""PE"",""name"": ""/_/artifacts/obj/System.Memory/Release/net9.0/System.Memory.pdb"",""date_time"": 3531865237,""age"": 1,""signature"": ""04eb8abedfcabda01f0f1387df07a0ba"",""perfmap_signature"": ""5723162253f404da627eafe069c8e92e"",""perfmap_version"": 1,""perfmap_name"": ""System.Memory.ni.r2rmap""}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.NotNull(metadata);
            Assert.Equal(ProcessMappingSymbolMetadataFileType.PE, metadata.Type);
            PEProcessMappingSymbolMetadata peMetadata = (PEProcessMappingSymbolMetadata)metadata;
            Assert.Equal("/_/artifacts/obj/System.Memory/Release/net9.0/System.Memory.pdb", peMetadata.PdbName);
            Assert.Equal(new Guid("04eb8abedfcabda01f0f1387df07a0ba"), peMetadata.PdbSignature);
            Assert.Equal(1, peMetadata.PdbAge);
            Assert.Equal(3531865237, peMetadata.DateTime);
            Assert.Equal(new Guid("5723162253f404da627eafe069c8e92e"), peMetadata.PerfmapSignature);
            Assert.Equal(1, peMetadata.PerfmapVersion);
            Assert.Equal("System.Memory.ni.r2rmap", peMetadata.PerfmapName);
        }

        [Fact]
        public void PEProcessMappingMetadata_NoPerfMapData()
        {
            string rawJson = @"{""type"": ""PE"",""name"": ""/_/artifacts/obj/System.Memory/Release/net9.0/System.Memory.pdb"",""date_time"": 3531865237,""age"": 1,""signature"": ""04eb8abedfcabda01f0f1387df07a0ba""}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.NotNull(metadata);
            Assert.Equal(ProcessMappingSymbolMetadataFileType.PE, metadata.Type);
            PEProcessMappingSymbolMetadata peMetadata = (PEProcessMappingSymbolMetadata)metadata;
            Assert.Equal("/_/artifacts/obj/System.Memory/Release/net9.0/System.Memory.pdb", peMetadata.PdbName);
            Assert.Equal(new Guid("04eb8abedfcabda01f0f1387df07a0ba"), peMetadata.PdbSignature);
            Assert.Equal(1, peMetadata.PdbAge);
            Assert.Equal(3531865237, peMetadata.DateTime);
            Assert.Equal(Guid.Empty, peMetadata.PerfmapSignature);
            Assert.Equal(0, peMetadata.PerfmapVersion);
            Assert.Null(peMetadata.PerfmapName);
        }

        [Fact]
        public void PEProcessMappingMetadata_Empty()
        {
            string rawJson = @"{""type"": ""PE""}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.NotNull(metadata);
            Assert.Equal(ProcessMappingSymbolMetadataFileType.PE, metadata.Type);
            PEProcessMappingSymbolMetadata peMetadata = (PEProcessMappingSymbolMetadata)metadata;
            Assert.Null(peMetadata.PdbName);
            Assert.Equal(Guid.Empty, peMetadata.PdbSignature);
            Assert.Equal(0, peMetadata.PdbAge);
            Assert.Equal(0, peMetadata.DateTime);
            Assert.Equal(Guid.Empty, peMetadata.PerfmapSignature);
            Assert.Equal(0, peMetadata.PerfmapVersion);
            Assert.Null(peMetadata.PerfmapName);
        }

        [Fact]
        public void ELFProcessMappingMetadata_Full()
        {
            string rawJson = @"{""type"": ""ELF"",""debug_link"": ""60568bc25641b07e93f84f98c77093f3b279f7.debug"",""build_id"": ""4b60568bc25641b07e93f84f98c77093f3b279f7""}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.NotNull(metadata);
            Assert.Equal(ProcessMappingSymbolMetadataFileType.ELF, metadata.Type);
            ELFProcessMappingSymbolMetadata peMetadata = (ELFProcessMappingSymbolMetadata)metadata;
            Assert.Equal("60568bc25641b07e93f84f98c77093f3b279f7.debug", peMetadata.DebugLink);
            Assert.Equal("4b60568bc25641b07e93f84f98c77093f3b279f7", peMetadata.BuildId);
        }

        [Fact]
        public void ELFProcessMappingMetadata_NoDebugLink()
        {
            string rawJson = @"{""type"": ""ELF"",""build_id"": ""4b60568bc25641b07e93f84f98c77093f3b279f7""}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.NotNull(metadata);
            Assert.Equal(ProcessMappingSymbolMetadataFileType.ELF, metadata.Type);
            ELFProcessMappingSymbolMetadata peMetadata = (ELFProcessMappingSymbolMetadata)metadata;
            Assert.Null(peMetadata.DebugLink);
            Assert.Equal("4b60568bc25641b07e93f84f98c77093f3b279f7", peMetadata.BuildId);
        }

        [Fact]
        public void ELFProcessMappingMetadata_Empty()
        {
            string rawJson = @"{""type"": ""ELF""}";
            ProcessMappingSymbolMetadata metadata = ProcessMappingSymbolMetadataParser.TryParse(rawJson);
            Assert.NotNull(metadata);
            Assert.Equal(ProcessMappingSymbolMetadataFileType.ELF, metadata.Type);
            ELFProcessMappingSymbolMetadata peMetadata = (ELFProcessMappingSymbolMetadata)metadata;
            Assert.Null(peMetadata.DebugLink);
            Assert.Null(peMetadata.BuildId);
        }
    }
}