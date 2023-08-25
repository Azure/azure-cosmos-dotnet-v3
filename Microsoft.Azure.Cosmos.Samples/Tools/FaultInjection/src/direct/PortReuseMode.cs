namespace Microsoft.Azure.Documents
{
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum PortReuseMode
    {
        // Do not rename any of the values.
        // They are used as app.config settings by external code.
        ReuseUnicastPort = 0,
        PrivatePortPool = 1,
    }
}
