using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing.Stacks;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// A stack source that displays the size of the IL as a stack source.       
    /// </summary>
    class ManagedExeSizeStackSource : InternStackSource
    {
        public ManagedExeSizeStackSource(string managedExePath)
        {
            // Make the full path the root node.   
            var stackBase = Interner.CallStackIntern(Interner.FrameIntern("FILE: " + Path.GetFileName(managedExePath)), StackSourceCallStackIndex.Invalid);

            StackSourceSample sample = new StackSourceSample(this); ;

            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(managedExePath);
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += delegate(object sender, ResolveEventArgs args)
            {
                Trace.WriteLine("RequestingAssembly " + args.RequestingAssembly.FullName);
                Trace.WriteLine("Name Requested " + args.Name);
                Assembly ret = null;
                try
                {
                    if (args.Name.StartsWith("System") || args.Name.StartsWith("Microsoft"))
                        ret = Assembly.ReflectionOnlyLoad(args.Name);
                }
                catch (Exception)
                {
                }
                if (ret == null)
                    Trace.WriteLine("Could not resolve assembly reference " + args.Name);
                return ret;
            };
            Type[] types = null;
            try
            {
                Trace.WriteLine("Calling GetTypes");
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Trace.WriteLine("Got exception " + e);
                foreach (var loaderException in e.LoaderExceptions)
                    Trace.WriteLine("Loader Exception " + loaderException);
                types = e.Types;
            }
            Trace.WriteLine("Got " + types.Length + " types");

            foreach (Type type in types)
            {
                if (type == null)
                    continue;
                Trace.WriteLine("Looking at TYPE " + type.FullName);
                foreach (MethodInfo methodDef in type.GetMethods())
                {
                    Trace.WriteLine("Looking at METHOD " + methodDef.Name);
                    MethodBody methodBody = methodDef.GetMethodBody();
                    int ilLen = 0;
                    if (methodBody != null)
                    {
                        byte[] il = methodBody.GetILAsByteArray();
                        if (il != null)
                            ilLen = il.Length;
                    }
                    PerfView.App.CommandProcessor.LogFile.WriteLine(string.Format("Method {0} size {1}", methodDef.Name, ilLen));

                    sample.StackIndex = Interner.CallStackIntern(Interner.FrameIntern("METHOD " + methodDef.Name), stackBase);
                    sample.Metric = ilLen;
                    AddSample(sample);
                }
            }
            Interner.DoneInterning();
            Trace.WriteLine("Done");
        }
    }
}
