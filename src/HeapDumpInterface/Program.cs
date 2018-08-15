namespace HeapDump
{
    // This code is never meant to be run.  It is a reference assembly for HeapDump.exe.  
    // It only exposes those classes that I wish to export (GCHeap), and thus avoid 
    // multiple definition errors at compile time.  
    internal class Program
    {
        internal static void Main(string[] args)
        {
        }
    }
}
