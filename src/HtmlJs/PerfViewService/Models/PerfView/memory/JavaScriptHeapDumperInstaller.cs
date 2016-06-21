using System;
using System.IO;
using Utilities;

class JavaScriptHeapDumperInstaller
{
    public static bool InstallJavaScriptDumper(TextWriter log, out bool didInstall)
    {
        didInstall = false;
        string system32Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
        var ret = InstallJavaScriptDumper(system32Dir, SupportFiles.ProcessArch, log, ref didInstall);
        if (!ret)
            return false;

        // On 64 bit install both places. 
        var nativeArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
        if (nativeArch != null && nativeArch.Length > 0)
        {
            system32Dir = Path.Combine(Path.GetDirectoryName(system32Dir), "SysNative");
            ret = InstallJavaScriptDumper(system32Dir, nativeArch, log, ref didInstall);
        }
        return ret;
    }

    private static bool InstallJavaScriptDumper(string system32Dir, string arch, TextWriter log, ref bool didInstall)
    {
        var targetDll = Path.Combine(system32Dir, "RetailDumperV2.dll");
        if (!File.Exists(targetDll))
        {
            var dumperDll = Path.Combine(SupportFiles.SupportFileDir, arch, "RetailDumperV2.dll");
            if (!File.Exists(dumperDll))
            {
                log.WriteLine(@"The JavaScript dumper is not support on architecture {0}.", arch);
                return false;
            }
            try
            {
                log.WriteLine("Copy {0} to {1}", dumperDll, targetDll);
                File.Copy(dumperDll, targetDll, true);
            }
            catch (UnauthorizedAccessException)
            {
                log.WriteLine(@"Can't access Windows\System32 directory.");
                log.WriteLine("To support JavaScript dumping a new DLL nees to be installed.");
                log.WriteLine("Thus you need to be elevated (once per arch) to allow this installation.");
                return false;
            }

            bool success = false;
            try
            {
                var options = new CommandOptions().AddOutputStream(log).AddTimeout(20000);
                log.WriteLine("Registering dumper dll");

                // funkyness because we are in the WOW.  once regsvr32 has been run it SysNative no longer works, so I need to use System32. 
                var realSysDir = Path.Combine(Path.GetDirectoryName(system32Dir), "System32");
                var realTarget = Path.Combine(realSysDir, "RetailDumperV2.dll");
                var regSvrCmd = system32Dir + @"\regsvr32.exe /s " + Command.Quote(realTarget);
                log.WriteLine("Running: " + regSvrCmd);
                Command.Run(regSvrCmd, options);

                var setxCmd = system32Dir + "\\setx.exe /m  JS_DM_CLSID \"{BEF7EA6E-C7B6-4A2B-80B4-67A08D2288E7}\"";
                log.WriteLine("Running: " + setxCmd);
                Command.Run(setxCmd, options);

                var icaclsCmd = system32Dir + @"\icacls.exe " + Command.Quote(realTarget) + " /grant *S-1-15-2-1:F";
                log.WriteLine("Running: " + icaclsCmd);
                Command.Run(icaclsCmd, options);

                success = true;
                didInstall = true;
            }
            finally
            {
                if (!success)
                    FileUtilities.ForceDelete(targetDll);
            }
        }
        return true;
    }

}
