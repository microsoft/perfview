using Azure.Identity;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Symbols.Authentication;
using System;
using System.IO;
using System.Reflection;

namespace Samples
{
    public static class SymwebManagedIdentityAuthSample
    {
        private const string SymbolServerUrl = @"https://symweb.azurefd.net";

        public static void Run(string[] args)
        {
            if (args.Length <= 1)
            {
                Console.WriteLine("You must specify a client id in order to use this sample.");
                return;
            }

            string clientId = args[1];
            string localSymbolCachePath = args.Length >= 3 ? args[2] : null;
            string dllToLookup = args.Length >= 4 ? args[3] : null;

            if (string.IsNullOrEmpty(localSymbolCachePath))
            {
                localSymbolCachePath = Path.Combine(Directory.GetCurrentDirectory(), "test-symbol-cache");
                Console.WriteLine($"User did not specify a local symbol cache directory.  Choosing '{localSymbolCachePath}'.");
            }

            if (string.IsNullOrEmpty(dllToLookup))
            {
                dllToLookup = Assembly.GetExecutingAssembly().Location;
                Console.WriteLine($"User did not specify a dll to lookup.  Choosing the current DLL: {dllToLookup}");
            }

            string symbolPath = $"SRV*{localSymbolCachePath}*{SymbolServerUrl}";

            Console.WriteLine($"Setting symbol path to {symbolPath}");
            Console.WriteLine($"Attempting to download symbols for {dllToLookup}");

            /**************************************************************************************************************/
            /*** Begin Example ***/

            // Setup the token credential that the handler will use to authenticate.
            ManagedIdentityCredential credential = new ManagedIdentityCredential(clientId);

            // Create a new symbols authentication handler and configure it for authentication to symweb.
            SymbolReaderAuthenticationHandler symbolReaderAuthHandler = new SymbolReaderAuthenticationHandler()
                .AddHandler(new SymwebHandler(Console.Out, credential));

            // Create a SymbolReader with the authentication handler.
            using (SymbolReader symbolReader = new SymbolReader(Console.Out, symbolPath, symbolReaderAuthHandler))
            {
                string pathToPDB = symbolReader.FindSymbolFilePathForModule(dllToLookup);
                if (string.IsNullOrEmpty(pathToPDB))
                {
                    Console.WriteLine("PDB not found.");
                }
                else
                {
                    Console.WriteLine($"PDB written to {pathToPDB}.");
                }
            }

            /*** End Example ***/
            /**************************************************************************************************************/
        }
    }
}