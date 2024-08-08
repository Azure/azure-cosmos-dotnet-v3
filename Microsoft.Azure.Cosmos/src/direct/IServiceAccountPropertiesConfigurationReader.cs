namespace Microsoft.Azure.Documents
{
    internal interface IServiceAccountPropertiesConfigurationReader : IServiceConfigurationReader
    {
        bool EnableNRegionSynchronousCommit { get; }
    }
}
