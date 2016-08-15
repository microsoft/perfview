
// This code is needed if you wish to use refection in the .NET COre way (using GetTypeInfo()).  
// But you also want the code to compile the same code to run on say V4.0 runtimes.  


// This code should only be included if you are compiling for pre-V4.5 systems.   Otherwise you should leave it out

namespace System
{
    public static class TypeExtensions
    {
        public static Type GetTypeInfo(this Type t) { return t;  }
    }
}
