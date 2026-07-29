namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;

    [TestClass]
    public class RoutingGatewayCosmosItemIdEncodingTests : CosmosItemIdEncodingTestsBase
    {
        protected override void ConfigureClientBuilder(CosmosClientBuilder builder)
        {
            // Check environment variable first to allow explicit mode switching
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
                // Default: Use Gateway mode when environment variable is not set
                builder.WithConnectionModeGateway();
            }
        }
    }
}
