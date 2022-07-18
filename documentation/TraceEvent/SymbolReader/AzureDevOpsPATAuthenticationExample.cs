using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.Examples
{
    /// <summary>
    /// This example shows how to download symbols from an authenticated Azure DevOps symbol server using a personal access token.
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            // Setup your symbol path.
            // ACTION REQUIRED: Set the URL based on your tenant name (replace example).
            string sympath = @"SRV*C:\Temp\SymCache*https://example.artifacts.visualstudio.com/defaultcollection/_apis/symbol/symsrv/";

            // Create an instance of SymbolReader, passing in the handler that will perform authentication.
            // Note: The input type to the SymbolReader constructor is System.Net.Http.DelegatingHandler.
            // In this example, we create a MessageProcessingHandler which is a class that inherits from DelegatingHandler and simplifies the work required.
            using (SymbolReader symReader = new SymbolReader(TextWriter.Null, sympath, new AzureDevOpsPATAuthenticationHandler()))
            {
                // ACTION REQUIRED: Point to the DLL whose symbols should be downloaded.
                string pdbPath = symReader.FindSymbolFilePathForModule(@"C:\Path\To\example.dll");
                Console.WriteLine($"PDB downloaded to: {pdbPath}");
            }
        }
    }

    // Implementation that will perform authentication when HttpClient attempts to download sources and/or symbols.
    internal sealed class AzureDevOpsPATAuthenticationHandler : MessageProcessingHandler
    {
        internal AzureDevOpsPATAuthenticationHandler()
        {
            InnerHandler = new HttpClientHandler();
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Setup the authorization header.
            // ACTION REQUIRED: Update the domain to match your tenant name.
            if (string.Equals("example.artifacts.visualstudio.com", request.RequestUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                // ACTION REQUIRED: Update the username and personal access token.
                string username = "user@domain.com";
                string pat = "personal-access-token";
                string headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{pat}"));

                request.Headers.Add("Authorization", $"Basic {headerValue}");
            }

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response;
        }
    }
}
