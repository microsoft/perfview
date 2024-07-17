using Azure.Core;
using Microsoft.Diagnostics.Symbols.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using Xunit;

namespace SymbolsAuthTests
{
    public class AuthenticationTests
    {
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
