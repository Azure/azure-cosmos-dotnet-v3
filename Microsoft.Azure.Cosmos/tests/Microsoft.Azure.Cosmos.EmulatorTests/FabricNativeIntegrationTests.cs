namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FabricNativeIntegrationTests : BaseCosmosClientHelper
    {
        private Container container = null;
        private List<ToDoActivity> items;
        private readonly string PartitionKey = "/pk";
        private readonly string DatabaseName = "dkunda-fabric-cdb";
        private readonly string ContainerName = "dkunda-fabric-cdb-container";
        private static readonly string FabricAuthorityHost = ConfigurationManager.GetEnvironmentVariable<string>("FABRIC_AUTHORITY_HOST", null);
        private static readonly string FabricEndpointUri = ConfigurationManager.GetEnvironmentVariable<string>("FABRIC_ENDPOINT_URI", null);

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.items = new();

            CosmosClientOptions options = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
            };

            CosmosClient cosmosClient = new(
                FabricNativeIntegrationTests.FabricEndpointUri,
                new MyTokenCredential(),
                options);

            Database database = cosmosClient.GetDatabase(this.DatabaseName);
            this.container = await database.CreateContainerIfNotExistsAsync(this.ContainerName, this.PartitionKey);

            if (this.items.Count == 0)
            {
                Random random = new Random();
                for (int i = 0; i < 25; i++)
                {
                    int id = random.Next();
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                    item.pk = $"pk-{id}";
                    item.id = id.ToString();
                    ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item);
                    Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);

                    this.items.Add(item);
                }
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.container.DeleteContainerAsync();
        }

        [TestMethod]
        public async Task CreateItemTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item);

            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReadItemTest()
        {
            foreach (ToDoActivity item in this.items)
            {
                ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                    item.id,
                    new Cosmos.PartitionKey(item.pk));

                Assert.IsNotNull(itemResponse);
                Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            }
        }

        [TestMethod]
        public async Task TriggerOperationsTest()
        {
            Scripts scripts = this.container.Scripts;

            TriggerProperties settings = new TriggerProperties
            {
                Id = Guid.NewGuid().ToString(),
                Body = FabricNativeIntegrationTests.GetTriggerFunction(".05"),
                TriggerOperation = TriggerOperation.Create,
                TriggerType = TriggerType.Pre
            };

            // Validate Create Trigger.
            TriggerResponse triggerResponse = await scripts.CreateTriggerAsync(settings);
            double reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, triggerResponse.StatusCode);
            Assert.IsNotNull(triggerResponse.Diagnostics);
            string diagnostics = triggerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            FabricNativeIntegrationTests.ValidateTriggerSettings(settings, triggerResponse);

            // Validate Read Trigger.
            triggerResponse = await scripts.ReadTriggerAsync(settings.Id);
            reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, triggerResponse.StatusCode);
            Assert.IsNotNull(triggerResponse.Diagnostics);
            diagnostics = triggerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            FabricNativeIntegrationTests.ValidateTriggerSettings(settings, triggerResponse);

            // Validate Replace Trigger.
            TriggerProperties updatedSettings = triggerResponse.Resource;
            updatedSettings.Body = FabricNativeIntegrationTests.GetTriggerFunction(".42");

            TriggerResponse replaceResponse = await scripts.ReplaceTriggerAsync(updatedSettings);
            FabricNativeIntegrationTests.ValidateTriggerSettings(updatedSettings, replaceResponse);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.IsNotNull(replaceResponse.Diagnostics);
            diagnostics = replaceResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));

            // Validate Delete Trigger.
            replaceResponse = await scripts.DeleteTriggerAsync(updatedSettings.Id);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, replaceResponse.StatusCode);
            Assert.IsNotNull(replaceResponse.Diagnostics);
            diagnostics = replaceResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
        }

        [TestMethod]
        public async Task BulkCreateItemTest()
        {
            Random random = new Random();
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                int id = random.Next();
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = $"pk-{id}";
                item.id = $"bulk-{id}";

                tasks.Add(this.container.CreateItemAsync<ToDoActivity>(item, new PartitionKey(item.pk)));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = tasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task StoredProcedureTest()
        {
            Random random = new Random();
            string scriptId = Guid.NewGuid().ToString();
            Scripts cosmosScripts = this.container.Scripts;

            // 1. Create stored procedure for script.
            StoredProcedureResponse createSprocResponse = await cosmosScripts.CreateStoredProcedureAsync(
                new StoredProcedureProperties(
                    scriptId,
                    GetStoredProcedureFunction()));

            Assert.IsNotNull(createSprocResponse);
            Assert.AreEqual(HttpStatusCode.Created, createSprocResponse.StatusCode);

            // 2. Create a document.
            int id = random.Next();
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            item.pk = $"pk-{id}";
            item.id = $"stored-proc-{id}";

            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);

            // 3. Run the script. Pass "Hello, " as parameter. 
            // The script will take the 1st document and echo: Hello, <document as json>.
            StoredProcedureExecuteResponse<string> readSprocResponse = await cosmosScripts.ExecuteStoredProcedureAsync<string>(
                scriptId,
                new PartitionKey(item.pk),
                new dynamic[] { "Hello" });

            Assert.IsNotNull(readSprocResponse);
            Assert.AreEqual(HttpStatusCode.OK, readSprocResponse.StatusCode);
            Assert.IsTrue(readSprocResponse.Resource.Contains("Hello"));
        }

        private static string GetTriggerFunction(string taxPercentage)
        {
            return @"function AddTax() {
                var item = getContext().getRequest().getBody();

                // Validate/calculate the tax.
                item.tax = calculateTax(item.cost);
                
                // Insert auto-created field 'createdTime'.
                item.createdTime = new Date();

                // Update the request -- this is what is going to be inserted.
                getContext().getRequest().setBody(item);
                function calculateTax(amt) {
                    // Simple input validation.

                    return amt * " + taxPercentage + @";
                }
            }";
        }

        private static string GetStoredProcedureFunction()
        {
            return @"function simple(prefix) {
                var collection = getContext().getCollection();

                // Query documents and take 1st item.
                var isAccepted = collection.queryDocuments(
                    collection.getSelfLink(),
                    'SELECT * FROM root r',
                    function (err, feed, options) {
                        if (err) throw err;

                        // Check the feed and if it's empty, set the body to 'no docs found',
                        // Otherwise just take 1st element from the feed.
                        if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
                        else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]));
                    });

                if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
            }";
        }

        private static void ValidateTriggerSettings(TriggerProperties triggerSettings, TriggerResponse cosmosResponse)
        {
            TriggerProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(triggerSettings.Body, settings.Body,
                "Trigger function do not match");
            Assert.AreEqual(triggerSettings.Id, settings.Id,
                "Trigger id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
            Assert.IsNotNull(cosmosResponse.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));
            SelflinkValidator.ValidateTriggerSelfLink(cosmosResponse.Resource.SelfLink);
        }

        public class MyTokenCredential : TokenCredential
        {
            private readonly TokenCredential credential;
            public MyTokenCredential()
            {
                this.credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
                {
                    AuthorityHost = new Uri(FabricNativeIntegrationTests.FabricAuthorityHost),
                });
            }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                AccessToken token = await this.credential.GetTokenAsync(new TokenRequestContext(new[] { "https://cosmos.azure.com/.default" }), CancellationToken.None);
                return token;
            }
        }
    }
}
