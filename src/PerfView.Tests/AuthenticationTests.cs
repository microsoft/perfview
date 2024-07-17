using Azure.Core;
using Microsoft.Diagnostics.Symbols.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using PerfView;
using System;
using Xunit;

namespace PerfViewTests
{
    public class AuthenticationTests
    {
        [Theory]
        [InlineData("/myorg", false, null)] // Must be an absolute URI
        [InlineData("https://example.com/path/_api/test?param1=value1", false, null)] // Not AzureDevOps
        [InlineData("https://dev.azure.com/myorg", true, "https://dev.azure.com/myorg")]
        [InlineData("https://artifacts.dev.azure.com/myorg", true, "https://dev.azure.com/myorg")]
        [InlineData("https://myorg.visualstudio.com/myproject/repos/myrepo/blah.txt", true, "https://myorg.visualstudio.com")]
        [InlineData("https://myorg.artifacts.visualstudio.com/_apis/Symbol/symsrv", true, "https://myorg.visualstudio.com")]
        [InlineData("http://dev.azure.com/myorg", true, "http://dev.azure.com/myorg")] // Works with other schemes
        [InlineData("https://dev.azure.com:443/myorg", true, "https://dev.azure.com/myorg")] // Standard port
        [InlineData("https://dev.azure.com:1234/myorg", true, "https://dev.azure.com:1234/myorg")] // Non-standard port
        public void AzureDevOpsAuthorityTheory(string fullUri, bool expectedReturn, string expectedAuthority)
        {
            var uri = new Uri(fullUri, UriKind.RelativeOrAbsolute);
            var expectedAuthorityUri = expectedAuthority is null ? null : new Uri(expectedAuthority, UriKind.RelativeOrAbsolute);

            Assert.Equal(expectedReturn, AzureDevOps.TryGetAuthority(uri, out Uri actualAuthorityUri));
            Assert.Equal(expectedAuthorityUri, actualAuthorityUri);
        }

        [Theory]
        [InlineData("/relative/path", false, null)] // Must be an absolute URI
        [InlineData("https://example.com/path/_api/test?param1=value1", false, null)] // Not GitHub
        [InlineData("https://github.com/random/path/doc.htm", true, "https://github.com")]
        [InlineData("https://raw.githubusercontent.com/user/repo/commitId/src/path/file.cs", true, "https://github.com")]
        [InlineData("http://raw.githubusercontent.com/user/repo/commitId/src/path/file.cs", true, "http://github.com")]
        [InlineData("https://raw.githubusercontent.com:443/user/repo/commitId/src/path/file.cs", true, "https://github.com")]
        [InlineData("https://raw.githubusercontent.com:9999/user/repo/commitId/src/path/file.cs", true, "https://github.com:9999")]
        public void GitHubAuthorityTheory(string fullUri, bool expectedReturn, string expectedAuthority)
        {
            var uri = new Uri(fullUri, UriKind.RelativeOrAbsolute);
            var expectedAuthorityUri = expectedAuthority is null ? null : new Uri(expectedAuthority, UriKind.RelativeOrAbsolute);

            Assert.Equal(expectedReturn, GitHub.TryGetAuthority(uri, out Uri actualAuthorityUri));
            Assert.Equal(expectedAuthorityUri, actualAuthorityUri);
        }
    }
}
