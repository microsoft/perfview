#if !PERFVIEW_COLLECT
using Azure.Core;
using Azure.Identity;
#endif
using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Utilities
{
    [Flags]
    public enum SymbolsAuthenticationType
    {
        Environment = 1,
        AzureCli = 2,
        VisualStudio = 4,
        Interactive = 8
    }

#if !PERFVIEW_COLLECT
    public static class SymbolsAuthenticationUtilities
    {
        internal static ChainedTokenCredential CreateTokenCredential(SymbolsAuthenticationType authTypes)
        {
            var credentials = new List<TokenCredential>();

            if (authTypes.HasFlag(SymbolsAuthenticationType.Environment))
            {
                credentials.Add(new EnvironmentCredential());
            }

            if (authTypes.HasFlag(SymbolsAuthenticationType.AzureCli))
            {
                credentials.Add(new AzureCliCredential());
            }

            if (authTypes.HasFlag(SymbolsAuthenticationType.VisualStudio))
            {
                credentials.Add(new VisualStudioCredential());
            }

            if (authTypes.HasFlag(SymbolsAuthenticationType.Interactive))
            {
                credentials.Add(new InteractiveBrowserCredential());
            }

            // If no credentials are specified, default to Interactive
            if (credentials.Count == 0)
            {
                credentials.Add(new InteractiveBrowserCredential());
            }

            return new ChainedTokenCredential(credentials.ToArray());
        }
    }
#endif
}
