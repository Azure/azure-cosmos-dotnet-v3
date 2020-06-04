namespace Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Threading.Tasks;
    using Azure.Core.Diagnostics;
    using Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DiagnosticsScopeTests
    {
        [TestMethod]
        public async Task VerifyScope()
        {
            using ClientDiagnosticListener testListener = new ClientDiagnosticListener();

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                return TestHandler.ReturnSuccess(new MockDiagnostics());
            });

            client.RequestHandler.InnerHandler = testHandler;
            await client.GetContainer("test", "test").CreateItemAsync(new Dictionary<string, object> { { "id", "test" } });
            ClientDiagnosticListener.ProducedDiagnosticScope sendScope = testListener.AssertScope(DiagnosticProperty.ResourceOperationActivityName(ResourceType.Document, OperationType.Create),
                new KeyValuePair<string, string>(DiagnosticProperty.Diagnostics, new MockDiagnostics().ToString()),
                new KeyValuePair<string, string>(DiagnosticProperty.ResourceUri, "dbs/test/colls/test"),
                new KeyValuePair<string, string>(DiagnosticProperty.Container, "dbs/test/colls/test"),
                new KeyValuePair<string, string>(DiagnosticProperty.ResourceType, ResourceType.Document.ToString()),
                new KeyValuePair<string, string>(DiagnosticProperty.OperationType, OperationType.Create.ToString()));
        }

        [TestMethod]
        public async Task VerifyConsoleLogger()
        {
            AzureEventSourceListener.CreateConsoleLogger(EventLevel.Informational);

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                return TestHandler.ReturnSuccess(new MockDiagnostics());
            });

            client.RequestHandler.InnerHandler = testHandler;
            await client.GetContainer("test", "test").CreateItemAsync(new Dictionary<string, object> { { "id", "test" } });
        }

        private class MockDiagnostics : CosmosDiagnostics
        {
            public override string ToString() => "This is a diagnostics";
        }
    }
}
