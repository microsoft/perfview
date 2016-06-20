namespace TraceEventAPIServer
{
    public interface ICallTreeDataProviderFactory
    {
        ICallTreeDataProvider Get();
    }
}