namespace Microsoft.Azure.Documents
{
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum QueryPlanGenerationMode
    {
        /// <summary>
        /// The SDK will auto detect the environment and availability of the ServiceInterop and 
        /// fallback to resolve the query plan as an HTTP request
        /// </summary>
        DefaultWindowsX64NativeWithFallbackToGateway = 0,

        /// <summary>
        /// The SDK will only use the local ServiceInterop to generate the query plan. 
        /// The local ServiceInterop only works on Windows with application running in x64
        /// </summary>
        WindowsX64NativeOnly = 1,

        /// <summary>
        /// The SDK will always go to gateway to get the query plan.
        /// </summary>
        GatewayOnly = 2
    }
}
