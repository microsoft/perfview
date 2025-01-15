// Copyright (c) Microsoft Corporation.  All rights reserved
// 
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics.Eventing;
using System.Collections.ObjectModel;
using Microsoft.Diagnostics.Tracing;

#if TARGET_FRAMEWORK_4_5_OR_HIGHER
using System.Runtime.InteropServices.WindowsRuntime;
#endif

class Program
{
    static class EventSourceReflectionProxy
    {
        public static string ManifestGenerator { get; set; }

#if USE_EVENTSOURCE_REFLECTION
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static Type GetBuiltinEventSourceType()
        {
            // defining this function allows us to avoid loading the builtin EventSource assembly while
            // JIT-ing GetEventSourceBaseIfOverridden when the ManifestGenerator is *not* "builtin"
            return typeof(Microsoft.Diagnostics.Tracing.EventSource); 
        }
        private static Type GetEventSourceBaseIfOverridden()
        {
            Type result = null;

            if (string.IsNullOrEmpty(ManifestGenerator))
                ManifestGenerator = "builtin";

            if (string.Compare(ManifestGenerator, "base", true) == 0)
                return null;
            else if (string.Compare(ManifestGenerator, "builtin", true) == 0)
                return GetBuiltinEventSourceType();

            if (!File.Exists(ManifestGenerator))
                throw new ApplicationException(string.Format("Failed to find file specified by ManifestGenerator ({0}).", ManifestGenerator));

            try
            {
                var asm = Assembly.LoadFrom(ManifestGenerator);
                result = asm.GetTypes().FirstOrDefault(t => t.Name == "EventSource");
            }
            catch { /* ignore errors */ }

            if (result == null)
                throw new ApplicationException(string.Format("Failed to find EventSource type in assembly specified by ManifestGenerator ({0}).", ManifestGenerator));

            return result;
        }

        private static Type GetBaseEventSourceType(Type reflectionOnlyDerivedEventSourceType)
        {
            // If overriding base EventSource return the overriding type
            Type loadFromEvtSrc = GetEventSourceBaseIfOverridden();
            if (loadFromEvtSrc != null)
                return loadFromEvtSrc;

            // At this point we have the assemblies defining (a) the custom event source and (b) the base class EventSource 
            // loaded as reflection-only assemblies. In order to run methods on EventSource we're now loading the base type for 
            // execution.
            Type reflectionOnlyEvtSrc = reflectionOnlyDerivedEventSourceType.BaseType;

            // find the "root" non-abstract EventSource. This leads to us ignoring GetName/GenerateManifest methods overridden
            // in derived abstract EventSources. We do this in order to avoid attempting to load for execution a user assembly 
            // that might not be "compatible" with us (e.g. platform specific assembly, or app store assembly)
            while (reflectionOnlyEvtSrc.IsAbstract) 
                reflectionOnlyEvtSrc = reflectionOnlyEvtSrc.BaseType;

            Assembly assm = reflectionOnlyEvtSrc.Assembly;
            if (assm.ReflectionOnly) 
                assm = Assembly.LoadFrom(assm.Location);
            loadFromEvtSrc = assm.GetType(reflectionOnlyEvtSrc.FullName);
            if (loadFromEvtSrc == null)
                throw new ApplicationException(string.Format("Failed to load base EventSource type while processing {0}.", reflectionOnlyDerivedEventSourceType.FullName));

            return loadFromEvtSrc;
        }
#endif
        internal static string GetName(Type eventSourceType)
        {
#if USE_EVENTSOURCE_REFLECTION
            var loadFromEvtSrcType = GetBaseEventSourceType(eventSourceType);

            MethodInfo mi = loadFromEvtSrcType.GetMethod("GetName", BindingFlags.Static | BindingFlags.Public, 
                                                         null, new Type[]{typeof(Type)}, null);
            if (mi == null)
                throw new ApplicationException(string.Format("Base EventSource type does not implement expected GetName() method (processing {0}).", eventSourceType.FullName));
            string name = (string)mi.Invoke(null, new object[] { eventSourceType });
            return name;
#else
            return EventSource.GetName(eventSourceType);
#endif
        }
        internal static string GenerateManifest(Type eventSourceType, string manifestDllPath, bool bForce = true)
        {
#if USE_EVENTSOURCE_REFLECTION
            var loadFromEvtSrcType = GetBaseEventSourceType(eventSourceType);

            Type tyGmf = loadFromEvtSrcType.Assembly.GetType(loadFromEvtSrcType.Namespace + ".EventManifestOptions");

            string man = null;
            MethodInfo mi = null;
            if (tyGmf != null)
            {
                mi = loadFromEvtSrcType.GetMethod("GenerateManifest", BindingFlags.Static | BindingFlags.Public,
                                                  null, new Type[] { typeof(Type), typeof(string), tyGmf }, null);
            }

            if (mi != null)
            {
                int flags = (bForce ? 0x0b : 0x0f); // Strict + Culture + AllowEventSourceOverride + maybe (OnlyIfRegistrationNeeded)
                man = (string)mi.Invoke(null, new object[] { eventSourceType, manifestDllPath, flags });
            }
            else
            {
                mi = loadFromEvtSrcType.GetMethod("GenerateManifest", BindingFlags.Static | BindingFlags.Public,
                                                    null, new Type[] { typeof(Type), typeof(string) }, null);
                if (mi == null)
                    throw new ApplicationException(string.Format("Base EventSource type does not implement expected GenerateManifest or GenerateManifestEx method", eventSourceType.FullName));

                man = (string)mi.Invoke(null, new object[] { eventSourceType, manifestDllPath });
            }
            return man;
#else
            return EventSource.GenerateManifest(eventSourceType, manifestDllPath);
#endif
        }
    }

    static int Main()
    {
#if USE_EVENTSOURCE_REFLECTION
        // If this app does not take a dependency on a specific EventSource implementation we'll need to use the EventSource 
        // type that represents the base type of the custom event source that needs to be registered. To accomplish this we 
        // need to set up a separate domain that has the app base set to the location of the assembly to be registered, and
        // the config file set to the config file that might be associated with this assembly, in the case the assembly is
        // an EXE. Having the worker domain set up in this way allows for the regular assembly lookup rules to kick in and
        // perform the expected lookup the the EventSource-base assembly based on the passed in assembly.
        // In the worker domain we load in the "reflection-only" context the assembly passed in as an argument and allow its
        // dependency to be loaded as well. Once the base assembly is loaded in the reflection-only context we retrieve its
        // location and call LoadFrom() on the assembly in order to get access to the EventSource static methods we need to
        // call.
        var workerDomainFriendlyName = "eventRegister_workerDomain";
        if (AppDomain.CurrentDomain.FriendlyName != workerDomainFriendlyName)
        {
            // See code:#CommandLineDefinitions for Command line defintions
            CommandLine commandLine = new CommandLine();
            var ads = new AppDomainSetup();
            // ads.PrivateBinPath = Path.GetDirectoryName(Path.GetFullPath(commandLine.DllPath));
            ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            // ads.ApplicationBase = Path.GetDirectoryName(Path.GetFullPath(commandLine.DllPath));
            // var configName = Path.GetFullPath(commandLine.DllPath) + ".config";
            // if (Path.GetExtension(commandLine.DllPath) == ".exe" && File.Exists(configName))
            //     ads.ConfigurationFile = configName;
            var workerDom = AppDomain.CreateDomain(workerDomainFriendlyName, null, ads, new System.Security.PermissionSet(System.Security.Permissions.PermissionState.Unrestricted));
            // make sure the worker domain is aware of the additional paths to probe when resolving the EventSource base type
            if (commandLine.ReferencePath != null)
                workerDom.SetData("ReferencePath", Environment.ExpandEnvironmentVariables(commandLine.ReferencePath));
            return workerDom.ExecuteAssembly(typeof(Program).Assembly.Location);
        }
        else
#endif
        return CommandLineUtilities.RunConsoleMainWithExceptionProcessing(delegate
        {
            // See code:#CommandLineDefinitions for Command line defintions
            CommandLine commandLine = new CommandLine();
            List<Tuple<string, string>> regDlls;
            EventSourceReflectionProxy.ManifestGenerator = commandLine.ManifestGenerator;
            switch (commandLine.Command)
            {
                // Create the XML description in the data directory "will be called 
                case CommandLine.CommandType.DumpManifest:
                    var manifests = CreateManifestsFromManagedDll(commandLine.DllPath, commandLine.ManifestPrefix);
                    if (manifests.Count == 0)
                        Console.WriteLine("Info: No event source classes " + (commandLine.ForceAll ? "" : "needing registration ") + "found in " + commandLine.DllPath);
                    break;
                case CommandLine.CommandType.CompileManifest:
                    CompileManifest(commandLine.ManPath, System.IO.Path.ChangeExtension(commandLine.ManPath, ".dll"));
                    break;
                case CommandLine.CommandType.DumpRegDlls:
                    regDlls = CreateRegDllsFromManagedDll(commandLine.DllPath, commandLine.ForceAll, commandLine.ManifestPrefix);
                    if (regDlls.Count == 0)
                        Console.WriteLine("Info: No event source classes " + (commandLine.ForceAll ? "" : "needing registration ") + "found in " + commandLine.DllPath);
                    break;
                case CommandLine.CommandType.Install:
                    regDlls = RegisterManagedDll(commandLine.DllPath, false, commandLine.ManifestPrefix);
                    if (regDlls.Count == 0)
                        Console.WriteLine("Info: No event source classes " + (commandLine.ForceAll ? "" : "needing registration ") + "found in " + commandLine.DllPath);
                    break;
                case CommandLine.CommandType.Uninstall:
                    regDlls = RegisterManagedDll(commandLine.DllPath, true, commandLine.ManifestPrefix);
                    if (regDlls.Count == 0)
                        Console.WriteLine("Info: No event source classes " + (commandLine.ForceAll ? "" : "needing registration ") + "found in " + commandLine.DllPath);
                    break;
            }
            return 0;
        });
    }

    // These correspond to command line commands
    /// <summary>
    /// Create a manifest file for each EventSource in 'managedDll'.  The manifest will 
    /// have the name manifestPrefix + ProviderName + .etwManifest.dll.  It is assumed
    /// that the manifest DLL name is manifestPrefix + ProviderName + .etwManifest.man
    /// 
    /// manifestPrefix and be null, in which case the basename of managedDllPath is used. 
    /// </summary>
    private static List<string> CreateManifestsFromManagedDll(string managedDllPath, string manifestPrefix=null)
    {
        if (manifestPrefix == null)
            manifestPrefix = Path.ChangeExtension(managedDllPath, null);

        bool bErrors = false;
        List<string> ret = new List<string>();
        foreach (var eventSource in GetEventSources(managedDllPath))
        {
            try
            {
                string providerName = EventSourceReflectionProxy.GetName(eventSource);
                string manifestXmlPath = manifestPrefix + "." + providerName + ".etwManifest.man";
                string manifestDllPath = manifestPrefix + "." + providerName + ".etwManifest.dll";
                CreateManifest(eventSource, manifestDllPath, manifestXmlPath);
                if (!File.Exists(manifestXmlPath))
                    continue;
                Console.WriteLine("Created manifest {0}", manifestXmlPath);
                ret.Add(manifestXmlPath);
            }
            catch (MultiErrorException e)
            {
                bErrors = true;
                Console.WriteLine("Error: " + eventSource.FullName + ": " + e.Message);
                foreach (var error in e.Errors)
                    Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + error);
            }
            catch (ApplicationException e)
            {
                bErrors = true;
                Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + e.Message);
            }
        }
        if (bErrors)
            throw new ApplicationException("Failures encountered creating manifests for EventSources in " + managedDllPath);

        return ret;
    }
    /// <summary>
    /// 
    /// </summary>
    private static List<Tuple<string, string>> CreateRegDllsFromManagedDll(string managedDllPath, bool bForceAll, string manifestPrefix = null)
    {
        if (manifestPrefix == null)
            manifestPrefix = Path.ChangeExtension(managedDllPath, null);

        bool bErrors = false;
        List<Tuple<string, string>> ret = new List<Tuple<string, string>>();
        foreach (var eventSource in GetEventSources(managedDllPath))
        {
            try
            {
                string providerName = EventSourceReflectionProxy.GetName(eventSource);
                string manifestXmlPath = manifestPrefix + "." + providerName + ".etwManifest.man";
                string manifestDllPath = manifestPrefix + "." + providerName + ".etwManifest.dll";
                CreateManifest(eventSource, manifestDllPath, manifestXmlPath, bForceAll);
                if (!File.Exists(manifestXmlPath))
                    continue;
                CompileManifest(manifestXmlPath, manifestDllPath);
                ret.Add(Tuple.Create(manifestXmlPath, manifestDllPath));
            }
            catch (MultiErrorException e)
            {
                bErrors = true;
                Console.WriteLine("Error: " + eventSource.FullName + ": " + e.Message);
                foreach (var error in e.Errors)
                    Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + error);
            }
            catch (ApplicationException e)
            {
                bErrors = true;
                Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + e.Message);
            }
        }
        if (bErrors)
            throw new ApplicationException("Failures encountered creating registration DLLs for EventSources in " + managedDllPath);

        return ret;
    }
    /// <summary>
    /// Create a manifest file for each EventSource in 'managedDll'.  and registers (or unregisters)
    /// those manifests with the system.  
    /// </summary>
    private static List<Tuple<string, string>> RegisterManagedDll(string managedDllPath, bool unregister, string manifestPrefix = null)
    {
        if (manifestPrefix == null)
            manifestPrefix = Path.ChangeExtension(managedDllPath, null);

        bool bErrors = false;
        List<Tuple<string, string>> ret = new List<Tuple<string, string>>();
        foreach (var eventSource in GetEventSources(managedDllPath))
        {
            try
            {
                string providerName = EventSourceReflectionProxy.GetName(eventSource);
                string manifestXmlPath = manifestPrefix + "." + providerName + ".etwManifest.man";
                string manifestDllPath = manifestPrefix + "." + providerName + ".etwManifest.dll";
                CreateManifest(eventSource, manifestDllPath, manifestXmlPath);
                if (!File.Exists(manifestXmlPath))
                    continue;
                CompileManifest(manifestXmlPath, manifestDllPath);
                RegisterManifestDll(manifestXmlPath, manifestDllPath, unregister);
                ret.Add(Tuple.Create(manifestXmlPath, manifestDllPath));
            }
            catch (MultiErrorException e)
            {
                bErrors = true;
                Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + e.Message);
                foreach (var error in e.Errors)
                    Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + error);
            }
            catch (ApplicationException e)
            {
                bErrors = true;
                Console.Error.WriteLine("Error: " + eventSource.FullName + ": " + e.Message);
            }
        }
        if (bErrors)
            throw new ApplicationException("Failures encountered during " + (unregister ? "unregistration" : "registration") + " of EventSources in " + managedDllPath);

        return ret;
    }

    /// <summary>
    /// Returns true when the "candidate" type derives from a type named "EventSource" through any number
    /// of abstract classes
    /// </summary>
    private static bool IsEventSourceType(Type candidate)
    {
        // return false for "object" and interfaces
        if (candidate.BaseType == null)
            return false;

        // now go up the inheritance chain until hitting a concrete type ("object" at worse)
        do
        {
            candidate = candidate.BaseType;
        }
        while (candidate != null && candidate.IsAbstract);
        return candidate != null && candidate.Name == "EventSource";
    }

    // These are helper functions.  
    /// <summary>
    /// Returns a list of types from 'dllPath' that inherit from EventSource  
    /// </summary>
    private static List<Type> GetEventSources(string dllPath)
    {
        List<Type> ret = new List<Type>();
        Assembly assembly;
        try
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;

            if (System.Environment.OSVersion.Version >= new Version(6, 2, 0, 0))
            {
                // on Win8+ support winmd assembly resolution
                SubscribeToWinRTReflectionOnlyNamespaceResolve();
            }

            assembly = Assembly.ReflectionOnlyLoadFrom(dllPath);

            foreach (Type type in assembly.GetTypes())
            {
                if (IsEventSourceType(type))
                {
                    ret.Add(type);
                }
            }
        }
        catch (ReflectionTypeLoadException rtle)
        {
            string msg = rtle.Message;
            foreach (var le in rtle.LoaderExceptions)
                msg += "\r\nLoaderException:\r\n" + le.ToString();
            throw new ApplicationException(msg);
        }
        catch (Exception e)
        {
            // Convert to an application exception TODO is this a good idea?
            throw new ApplicationException(e.Message);
        }

        return ret;
    }

    #region WinRT Reflection-Only Load Handling
#if TARGET_FRAMEWORK_4_5_OR_HIGHER
    static void WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve(object sender, NamespaceResolveEventArgs e)
    {
        foreach (string s in WindowsRuntimeMetadata.ResolveNamespace(e.NamespaceName, null))
        {
            e.ResolvedAssemblies.Add(Assembly.ReflectionOnlyLoadFrom(s));
        }
    }

    static void SubscribeToWinRTReflectionOnlyNamespaceResolve()
    {
        WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve;
    }
#else
    static void WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve(object sender, object /*NamespaceResolveEventArgs*/ e)
    {
        // foreach (string s in WindowsRuntimeMetadata.ResolveNamespace(e.NamespaceName, null))
        // {
        //     e.ResolvedAssemblies.Add(Assembly.ReflectionOnlyLoadFrom(s));
        // }
        Assembly mscorlibAsm = typeof(object).Assembly;
        Type twrm = mscorlibAsm.GetType("System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeMetadata");

        Type tnrea = mscorlibAsm.GetType("System.Runtime.InteropServices.WindowsRuntime.NamespaceResolveEventArgs");
        var namespaceName = (string)tnrea.GetProperty("NamespaceName").GetValue(e, null);
        var winmdFileNames = (IEnumerable<string>)twrm.InvokeMember("ResolveNamespace",
                              BindingFlags.InvokeMethod, null, null, new object[] { namespaceName, null });
        foreach(var s in winmdFileNames)
        {
            var assms = (Collection<Assembly>)tnrea.GetProperty("ResolvedAssemblies").GetValue(e, null);
            assms.Add(Assembly.ReflectionOnlyLoadFrom(s));
        }
    }

    static void SubscribeToWinRTReflectionOnlyNamespaceResolve()
    {
        // WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve;

        Assembly mscorlibAsm = typeof(object).Assembly;
        Type twrm = mscorlibAsm.GetType("System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeMetadata");

        if (twrm != null)
        {
            // Get the EventInfo representing the ReflectionOnlyNamespaceResolve event, 
            // and the type of delegate that handles the event.
            EventInfo evRonsr = twrm.GetEvent("ReflectionOnlyNamespaceResolve");
            Type tDelegate = evRonsr.EventHandlerType;

            // If you already have a method with the correct signature, you can simply get a MethodInfo for it.  
            MethodInfo miHandler =
                typeof(Program).GetMethod("WindowsRuntimeMetadata_ReflectionOnlyNamespaceResolve",
                    BindingFlags.NonPublic | BindingFlags.Static);

            // Create an instance of the delegate. Using the overloads 
            // of CreateDelegate that take MethodInfo is recommended. 
            Delegate d = Delegate.CreateDelegate(tDelegate, null, miHandler, true);

            // Get the "add" accessor of the event and invoke it late-bound
            MethodInfo addHandler = evRonsr.GetAddMethod();
            addHandler.Invoke(evRonsr, new object[] { d });
        }
    }
#endif
    #endregion WinRT Reflection-Only Load Handling

    static Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
    {
        // ensure we handle retargeting and unification
        string name = AppDomain.CurrentDomain.ApplyPolicy(args.Name);
        Assembly assm = null;

        try
        { assm = Assembly.ReflectionOnlyLoad(name); }
        catch
        { }

        if (assm != null)
            return assm;

        string paths = (string)AppDomain.CurrentDomain.GetData("ReferencePath");
        if (string.IsNullOrEmpty(paths))
            return null;

        foreach (var path in paths.Split(';'))
        {
            try
            { assm = Assembly.ReflectionOnlyLoadFrom(path); }
            catch 
            { }
            if (assm != null && assm.FullName == name)
                return assm;
        }

        return null;
    }

    /// <summary>
    /// Given an eventSourceType, and the name of the DLL that the manifest where the manifest
    /// is intended to be placed, create the manifest XML and put it in the file manifestXmlPath
    /// </summary>
    private static void CreateManifest(Type eventSourceType, string manifestDllPath, string manifestXmlPath, bool bForce = true)
    {
        try
        {
            string providerXML = (string)EventSourceReflectionProxy.GenerateManifest(eventSourceType, Path.GetFullPath(manifestDllPath), bForce);
            if (!string.IsNullOrEmpty(providerXML))
            {
                using (TextWriter writer = File.CreateText(manifestXmlPath))
                {
                    writer.Write(providerXML);
                }
            }
        }
        catch (TargetInvocationException e)
        {
            string msg = e.InnerException != null ? e.InnerException.Message : e.Message;
            throw new MultiErrorException("Generation of ETW manifest failed", msg);
        }
    }
    /// <summary>
    /// Takes an manifest XML in manifestXmlPath and compiles it into a DLL that can 
    /// be registered using wevtutil placing it in string manifestDllPath
    /// </summary>
    private static void CompileManifest(string manifestXmlPath, string manifestDllPath)
    {
        DirectoryUtilities.Clean(WorkingDir);

        // Get the tools and put them on my path. 
        string toolsDir = Path.Combine(WorkingDir, "tools");
        Directory.CreateDirectory(toolsDir);
        ResourceUtilities.UnpackResourceAsFile(@".\mc.exe", Path.Combine(toolsDir, "mc.exe"));
        ResourceUtilities.UnpackResourceAsFile(@".\rc.exe", Path.Combine(toolsDir, "rc.exe"));
        ResourceUtilities.UnpackResourceAsFile(@".\rcdll.dll", Path.Combine(toolsDir, "rcdll.dll"));

        // put runtimeDir directory on the path so that we have the CSC on the path. 
        string runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        Environment.SetEnvironmentVariable("PATH", toolsDir + ";" + runtimeDir + ";" + Environment.GetEnvironmentVariable("PATH"));

        // Unpack the winmeta.xml file 
        string dataDir = Path.Combine(WorkingDir, "data");
        Directory.CreateDirectory(dataDir);
        ResourceUtilities.UnpackResourceAsFile(@".\winmeta.xml", Path.Combine(dataDir, "winmeta.xml"));

        // Compile Xml manifest into a binary reasource, this produces a
        //     * MANIFEST.rc               A text description of the resource which refers to 
        //     * MANIFESTTEMP.Bin          The binary form of the manifest        
        //     * MANIFEST_MSG00001.Bin     The localized strings from the manifest
        Console.WriteLine("Compiling Manifest {0} to ETW binary form.", manifestXmlPath);
        try
        {
            string inputXml = Path.Combine(dataDir, Path.GetFileName(manifestXmlPath));
            File.Copy(manifestXmlPath, inputXml);
            Command.Run(string.Format("{0} -W \"{1}\" -b \"{2}\"",
                                Path.Combine(toolsDir, "mc.exe"),
                                Path.Combine(dataDir, "winmeta.xml"),
                                inputXml),
                    new CommandOptions().AddCurrentDirectory(dataDir));

            string rcFilePath = Path.ChangeExtension(inputXml, ".rc");
            Debug.Assert(File.Exists(rcFilePath));

            // Compile the .rc file into a .res file (which is the binary form of everything in the
            // .rc file
            Command.Run(string.Format("rc \"{0}\"", rcFilePath));
            string resFilePath = Path.ChangeExtension(inputXml, ".res");
            Debug.Assert(File.Exists(resFilePath));

            // Make a dummy c# file 
            string csFilePath = Path.Combine(dataDir, Path.GetFileNameWithoutExtension(inputXml) + ".cs");
            File.WriteAllText(csFilePath, "");

            // At this point we have .RES file, we now create a etw manifest dll to hold it a
            Console.WriteLine("Creating Manifest DLL to hold binary Manifest at {0}.", manifestDllPath);
            FileUtilities.ForceDelete(manifestDllPath);

            Command.Run(string.Format("csc /nologo /target:library \"/out:{0}\" \"/win32res:{1}\" \"{2}\"",
                manifestDllPath, resFilePath, csFilePath));
            Debug.Assert(File.Exists(manifestDllPath));
        }
        catch (Exception e)
        {
            throw new ApplicationException(
                "ERROR: compilation of the ETW manifest failed.\r\n" +
                "File: " + manifestXmlPath + "\r\n" +
                "Details:\r\n" +
                e.Message);
        }
        finally
        {
            DirectoryUtilities.Clean(WorkingDir);
        }
    }
    /// <summary>
    /// Registers (or unregisters if unregister=true) manifestDllPath with the OS
    /// </summary>
    private static void RegisterManifestDll(string manifestXmlPath, string manifestDllPath = null, bool unregister = false)
    {
        Console.WriteLine("Using wevtutil to {0} manifest {1}", unregister ? "uninstall" : "install", manifestXmlPath);
        string resdll = "";
        if (!unregister && !string.IsNullOrEmpty(manifestDllPath))
            resdll = string.Format(" /rf:\"{0}\" /mf:\"{0}\"", Path.GetFullPath(manifestDllPath));
        Command.Run(string.Format("wevtutil {1} \"{0}\"{2}", manifestXmlPath, unregister ? "uninstall-manifest" : "install-manifest", resdll));
    }

    private static string WorkingDir
    {
        get
        {
            if (s_WorkingDir == null)
            {
                string tempDir = Environment.GetEnvironmentVariable("TEMP");
                if (tempDir == null)
                    tempDir = ".";
                s_WorkingDir = Path.Combine(tempDir, "eventRegister" + System.Diagnostics.Process.GetCurrentProcess().Id);
            }
            return s_WorkingDir;
        }
    }
    private static string s_WorkingDir;
}

class MultiErrorException : ApplicationException
{
    public MultiErrorException(string msg, string multilineMsg, string sep = null)
        : base(msg)
    { 
        if (sep == null) 
            sep = Environment.NewLine; 

        Errors = multilineMsg.Split(new string[] { sep }, int.MaxValue, StringSplitOptions.RemoveEmptyEntries);
    }

    public string[] Errors { get; private set; }
}

/// <summary>
/// The code:CommandLine class holds the parsed form of all the Command line arguments.  It is
/// intialized by handing it the 'args' array for main, and it has a public field for each named argument
/// (eg -debug). See code:#CommandLineDefinitions for the code that defines the arguments (and the help
/// strings associated with them). 
/// 
/// See code:CommandLineParser for more on parser itself.   
/// </summary>
class CommandLine
{
    public CommandLine()
    {
        bool usersGuide = false;
        CommandLineParser.ParseForConsoleApplication(delegate(CommandLineParser parser)
        {
            string dllPathHelp = "The path to the DLL containing the Event provider (the class that subclasses EventSource).";
            // parser.NoDashOnParameterSets = true;
            // #CommandLineDefinitions
            parser.DefineOptionalQualifier("ReferencePath", ref ReferencePath, "If specified, use this list of semi-colon separated assemblies to resolve the assembly containing the EventSource base class. Use only if regular resolution does not work adequately.");
            parser.DefineOptionalQualifier("ManifestGenerator", ref ManifestGenerator, 
                "Specifies what code runs to validate and generate the manifest for the user-defined event source classes. " +
                "Use \"builtin\" (the default choice) to choose the tool's builtin EventSource. " +
                "Use \"base\" to choose the code from the base class of the user-defined event source. " +
                "Or use a path name to choose the first \"EventSource\" type from the assembly specified by the path.");

            parser.DefineParameterSet("UsersGuide", ref usersGuide, true, "Display the users guide.");

            parser.DefineParameterSet("DumpManifest", ref Command, CommandType.DumpManifest, "Just generates the XML manifest for the managed code.");
            parser.DefineParameter("AssemblyPath", ref DllPath, dllPathHelp);
            parser.DefineOptionalParameter("ManifestXmlPrefix", ref ManifestPrefix, "The file name prefix used to generate output file names for the provider manifests.");

            parser.DefineParameterSet("CompileManifest", ref Command, CommandType.CompileManifest, "Just generates the registration DLL from the XML manifest.");
            parser.DefineParameter("ManifestXmlPath", ref ManPath, "The path to the XML manifest containing the Event provider");
            DllPath = ManPath; // fake it so "Main" can create the secondary app domain

            parser.DefineParameterSet("DumpRegDlls", ref Command, CommandType.DumpRegDlls, "Just generates the XML manifest and registration DLL for the managed code.");
            parser.DefineOptionalQualifier("ForceAll", ref ForceAll, "If specified, generate manifests and registration DLLs for all EventSource-derived classes, otherwise it generates them only for the classes that need explicit registration.");
            parser.DefineParameter("AssemblyPath", ref DllPath, dllPathHelp);
            parser.DefineOptionalParameter("ManifestXmlPrefix", ref ManifestPrefix, "The file name prefix used to generate output file names for the provider manifests.");

            parser.DefineParameterSet("Uninstall", ref Command, CommandType.Uninstall, "Uninstall all providers defined in the assembly.");
            parser.DefineParameter("AssemblyPath", ref DllPath, dllPathHelp);

            parser.DefineDefaultParameterSet("Installs managed Event Tracing for Windows (ETW) event providers defined in the assembly.");
            parser.DefineParameter("AssemblyPath", ref DllPath, dllPathHelp);
            parser.DefineOptionalParameter("ManifestXmlPrefix", ref ManifestPrefix, "The file name prefix used to generate output file names for the provider manifests.");
        });
        if (usersGuide)
            UsersGuide.DisplayConsoleAppUsersGuide("UsersGuide.htm");
    }
    public enum CommandType
    {
        Install,
        Uninstall,
        DumpManifest,
        CompileManifest,
        DumpRegDlls,
    }

    public CommandType Command = CommandType.Install;
    public string DllPath;
    public string ManPath;
    public string ManifestPrefix;
    public string ReferencePath;
    public string ManifestGenerator = "builtin"; // "base", "path_to_assm_containing_EventSource_type"
    public bool ForceAll;
};

