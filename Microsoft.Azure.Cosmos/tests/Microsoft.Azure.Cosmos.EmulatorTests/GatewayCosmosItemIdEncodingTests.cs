namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;

    [TestClass]
    public class GatewayCosmosItemIdEncodingTests : CosmosItemIdEncodingTestsBase
    {
        protected override void ConfigureClientBuilder(CosmosClientBuilder builder)
        {
            builder.WithConnectionModeGateway();
        }
    }
}
