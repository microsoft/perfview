using System;

namespace Samples
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            int exampleNum;
            if (args.Length != 0)
            {
                exampleNum = int.Parse(args[0]);
            }
            else
            {
                PrintUsage();
                return;
            }

            switch (exampleNum)
            {
                case 1:
                    SymwebInteractivePopupAuthSample.Run(args);
                    break;
                case 2:
                    SymwebManagedIdentityAuthSample.Run(args);
                    break;
                default:
                    PrintUsage();
                    break;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Samples.exe <ExampleNumber> [optional args for example]");
            Console.WriteLine("Samples:");
            Console.WriteLine("\t#1: Symweb interactive pop-up authentication");
            Console.WriteLine("\tArguments:");
            Console.WriteLine("\t\tDirectory to store downloaded symbols.");
            Console.WriteLine("\t\tPath to DLL whose PDB should be looked up on the symbol server.");
            Console.WriteLine("Example:");
            Console.WriteLine("\t\"SymbolsAuth.Samples.exe 1\" // Use default values to test symbol server authentication.");
            Console.WriteLine("\t\"SymbolsAuth.Samples.exe 1 C:\\SymCache Test.dll\" // Specify values to test symbol server authentication.");
            Console.WriteLine("\t#1: Symweb managed identity authentication");
            Console.WriteLine("\tArguments:");
            Console.WriteLine("\t\tClientId of the managed identity.");
            Console.WriteLine("\t\tDirectory to store downloaded symbols.");
            Console.WriteLine("\t\tPath to DLL whose PDB should be looked up on the symbol server.");
            Console.WriteLine("Example:");
            Console.WriteLine("\t\"SymbolsAuth.Samples.exe 2 <clientid>\" // Use default values to test symbol server authentication.");
            Console.WriteLine("\t\"SymbolsAuth.Samples.exe 2 <clientid> C:\\SymCache Test.dll\" // Specify values to test symbol server authentication.");
        }
    }
}
