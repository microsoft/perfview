
namespace System.Security
{
    //
    // Summary:
    //     Allows managed code to call into unmanaged code without a stack walk. This class
    //     cannot be inherited.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = true, Inherited = false)]
    public sealed class SuppressUnmanagedCodeSecurityAttribute : Attribute
    {
        //
        // Summary:
        //     Initializes a new instance of the System.Security.SuppressUnmanagedCodeSecurityAttribute
        //     class.
        public SuppressUnmanagedCodeSecurityAttribute() { }
    }
}


namespace System.Runtime.InteropServices
{
    //
    // Summary:
    //     Allows an unmanaged method to call a managed method.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AllowReversePInvokeCallsAttribute : Attribute
    {
        //
        // Summary:
        //     Initializes a new instance of the System.Runtime.InteropServices.AllowReversePInvokeCallsAttribute
        //     class.
        public AllowReversePInvokeCallsAttribute() { }
    }
}

namespace System
{
    //
    // Summary:
    //     The exception that is thrown when a non-fatal application error occurs.
    public class ApplicationException : Exception
    {

        //
        // Summary:
        //     Initializes a new instance of the System.ApplicationException class with a specified
        //     error message.
        //
        // Parameters:
        //   message:
        //     A message that describes the error.
        public ApplicationException(string message) : base(message) { }
        public ApplicationException(string message, Exception e) : base(message, e) { }
    }

    public class EntryPointNotFoundException : TypeLoadException
    {
        public EntryPointNotFoundException(string message) : base(message) { }
    }
}

#if !NOT_WINDOWS

namespace System.Diagnostics
{
    public class Trace
    {
        static public void Write(string message) { }
        static public void WriteLine(string message) { Write(message); Write("\n"); }
    }
}


// DotNet 4.5+ additions

#endif