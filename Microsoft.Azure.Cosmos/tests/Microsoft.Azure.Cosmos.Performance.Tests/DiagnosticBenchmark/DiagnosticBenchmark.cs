namespace Microsoft.Azure.Cosmos.Performance.Tests
{
    using System;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;

    [MemoryDiagnoser]
    public class DiagnosticBenchmark
    {
        private static readonly ItemRequestOptions noDiagnosticRequestOptions = new ItemRequestOptions()
        {
            DiagnosticContextFactory = () => EmptyCosmosDiagnosticsContext.Singleton
        };

        private CosmosClient cosmosClient;
        private Container container;
        private ToDoActivity toDoActivity;
        private ItemResponse<ToDoActivity> itemResponse;
        

        public class ToDoActivity
        {
            public ToDoActivity(
                string id = null)
            {
                if (id == null)
                {
                    id = Guid.NewGuid().ToString();
                }

                this.id = id;
                this.partitionKey = new PartitionKey(id);
                this.description = "CreateRandomToDoActivity";
                this.status = "ToDoActivity";
                this.taskNum = 42;
                this.cost = double.MaxValue;
                this.CamelCase = "camelCase";
                this.children = null;
                this.valid = true;
            }
            public string id { get; }
            public PartitionKey partitionKey { get; }
            public int taskNum { get; }
            public double cost { get; }
            public string description { get; }
            public string status { get; }
            public string CamelCase { get; }

            public bool valid { get; }

            public ToDoActivity[] children { get; }

            public override bool Equals(object obj)
            {
                if (!(obj is ToDoActivity input))
                {
                    return false;
                }

                return string.Equals(this.id, input.id)
                    && this.taskNum == input.taskNum
                    && this.cost == input.cost
                    && string.Equals(this.description, input.description)
                    && string.Equals(this.status, input.status);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        [GlobalSetup]
        public void GlobalSetupDiagnosticBenchmark()
        {
            this.Setup().GetAwaiter().GetResult();
        }

        [Benchmark]
        public async Task ReadItemAsync()
        {
            ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                this.toDoActivity.id,
                this.toDoActivity.partitionKey);

            if (itemResponse.Resource == null)
            {
                throw new Exception();
            }
        }

        [Benchmark]
        public async Task ReadItemNoDiagnosticsAsync()
        {
            ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                this.toDoActivity.id,
                this.toDoActivity.partitionKey,
                noDiagnosticRequestOptions);

            if (itemResponse.Resource == null)
            {
                throw new Exception();
            }
        }

        [Benchmark]
        public async Task ReadItemWithDiagnosticsAsync()
        {
            ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                this.toDoActivity.id,
                this.toDoActivity.partitionKey);

            if (itemResponse.Resource == null)
            {
                throw new Exception();
            }

            string diagnostics = itemResponse.Diagnostics.ToString();
            if (string.IsNullOrEmpty(diagnostics))
            {
                throw new Exception();
            }
        }

        [Benchmark]
        public void DiagnosticsSerialization()
        {
            string diagnostics = this.itemResponse.Diagnostics.ToString();
            if (string.IsNullOrEmpty(diagnostics))
            {
                throw new Exception();
            }
        }

        private async Task Setup()
        {
            this.cosmosClient = new CosmosClient(
               "https://localhost:8081",
               "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

            Database database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                nameof(DiagnosticBenchmark));
            this.container = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(nameof(GlobalSetupDiagnosticBenchmark), "/id"),
                ThroughputProperties.CreateAutoscaleThroughput(100000));
            this.toDoActivity = new ToDoActivity();
            this.itemResponse = await this.container.CreateItemAsync<ToDoActivity>(this.toDoActivity, this.toDoActivity.partitionKey);
        }
    }
}