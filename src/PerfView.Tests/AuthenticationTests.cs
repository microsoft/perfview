using Azure.Core;
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

        [Fact]
        public void AuthTokenCreateBasicFromUsernameAndPasswordTest()
        {
            Assert.Throws<ArgumentException>(() => AuthToken.CreateBasicFromUsernameAndPassword(null, "secret"));
            Assert.Throws<ArgumentException>(() => AuthToken.CreateBasicFromUsernameAndPassword("", "secret"));
            Assert.Throws<ArgumentException>(() => AuthToken.CreateBasicFromUsernameAndPassword("me", null));
            Assert.Throws<ArgumentException>(() => AuthToken.CreateBasicFromUsernameAndPassword("me", ""));

            var authToken = AuthToken.CreateBasicFromUsernameAndPassword("me", "secret");
            Assert.Equal("me", authToken.UserName);
            Assert.Equal("secret", authToken.Password);
            Assert.Equal("bWU6c2VjcmV0", authToken.Token);
            Assert.Equal(AuthScheme.Basic, authToken.Scheme);
            Assert.NotEqual(default, authToken.ExpiresOn);
        }

        [Fact]
        public void AuthTokenTryParseFromOAuthTokenTest()
        {
            Assert.False(AuthToken.TryParseFromOAuthToken(null, null, out _));
            Assert.False(AuthToken.TryParseFromOAuthToken("me", "not an OAuth token", out _));

            var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiryTime = currentUnixTime + (8 * 60 * 60); // 8 hours
            DateTime expectedExpiresOn = DateTimeOffset.FromUnixTimeSeconds(expiryTime).UtcDateTime;

            string token = new JsonWebTokenHandler().CreateToken(payload:
                $@"{{
                      ""key"" : ""value"",
                      ""exp"" : ""{expiryTime}""
                    }}");

            Assert.True(AuthToken.TryParseFromOAuthToken("me", token, out AuthToken authToken));
            Assert.Equal(AuthScheme.Bearer, authToken.Scheme);
            Assert.Equal(token, authToken.Token);
            Assert.Equal(expectedExpiresOn, authToken.ExpiresOn);
            Assert.Equal("me", authToken.UserName);
            Assert.Null(authToken.Password);
        }

        [Fact]
        public void AuthTokenFromAzureCoreAccessTokenTest()
        {
            string expectedToken = "abcdefg";
            DateTimeOffset expectedExpiresOn = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30);

            AccessToken accessToken = new AccessToken(expectedToken, expectedExpiresOn);
            AuthToken authToken = AuthToken.FromAzureCoreAccessToken(accessToken);
            
            Assert.Equal(AuthScheme.Bearer, authToken.Scheme);
            Assert.Equal(expectedToken, authToken.Token);
            Assert.Equal(expectedExpiresOn, authToken.ExpiresOn);
            Assert.Null(authToken.UserName);
            Assert.Null(authToken.Password);
        }
    }
}
