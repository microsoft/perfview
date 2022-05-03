#if NET462
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.Diagnostics.Symbols
{
    public static class Adal
    {
        private static object s_lock = new object();
        private static DateTime s_lastAttempt = DateTime.MinValue;
        private static string s_accessToken;
        private static DateTimeOffset s_accessTokenExpiresOn = DateTimeOffset.MinValue;

        public static async Task<string> AcquireTokenAsync()
        {
            lock (s_lock)
            {
                // If there is a token and it hasn't expired yet, use it
                if (s_accessToken != null && DateTime.UtcNow < s_accessTokenExpiresOn.UtcDateTime)
                {
                    return s_accessToken;
                }

                // Do not prompt more than once/minute
                var now = DateTime.UtcNow;
                if (now < s_lastAttempt + TimeSpan.FromSeconds(60))
                {
                    return null;
                }
                s_lastAttempt = now;
            }

            var tenantId = "microsoft.com";
            var authority = $"https://login.microsoftonline.com/{tenantId}";
            var resourceId = "499b84ac-1321-427f-aa17-267ca6975798"; // ADO

            var clientId = "1950a258-227b-4e31-a9cf-717495945fc2"; // Azure PowerShell
            var redirectUri = new Uri("urn:ietf:wg:oauth:2.0:oob");

            var authContext = new Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext(authority);

            try
            {
                var token = await authContext.AcquireTokenAsync(resourceId, clientId, redirectUri, new PlatformParameters(PromptBehavior.Auto));
                lock (s_lock)
                {
                    s_accessToken = token.AccessToken;
                    s_accessTokenExpiresOn = token.ExpiresOn;
                }
                return token.AccessToken;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
#endif