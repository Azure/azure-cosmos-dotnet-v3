namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;

    [TestClass]
    public class ComputeGatewayCosmosItemIdEncodingTests : CosmosItemIdEncodingTestsBase
    {
        protected override string AccountEndpointOverride => Utils.ConfigurationManager.AppSettings["ComputeGatewayEndpoint"];

        protected override void ConfigureClientBuilder(CosmosClientBuilder builder)
        {
            builder.WithConnectionModeGateway();
        }
    }
}
