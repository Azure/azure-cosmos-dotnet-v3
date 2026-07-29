namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;

    [TestClass]
    public class DirectCosmosItemIdEncodingTests : CosmosItemIdEncodingTestsBase
    {
        protected override void ConfigureClientBuilder(CosmosClientBuilder builder)
        {
            // Check environment variable first to allow switching to Gateway mode for vNext emulator testing
            string connectionModeEnv = Cosmos.ConfigurationManager.GetEnvironmentVariable<string>("AZURE_COSMOS_EMULATOR_CONNECTION_MODE", string.Empty);
            if (string.Equals(connectionModeEnv, "Gateway", StringComparison.OrdinalIgnoreCase))
            {
                builder.WithConnectionModeGateway();
            }
            else if (string.Equals(connectionModeEnv, "Direct", StringComparison.OrdinalIgnoreCase))
            {
                builder.WithConnectionModeDirect();
            }
            else
            {
                // Default: Use Direct mode when environment variable is not set
                builder.WithConnectionModeDirect();
            }
        }
    }
}
