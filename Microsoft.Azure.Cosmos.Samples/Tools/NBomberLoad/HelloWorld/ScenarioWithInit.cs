
namespace NBomber
{
    using Microsoft.Azure.Cosmos;
    using NBomber.CSharp;

    public class ScenarioWithInit
    {
        private readonly Random rand = new Random();

        private Database database;

        private Container container;

        private readonly Dictionary<string, string> idPkMap = new Dictionary<string, string>();

        public void Run()
        {
            var scn1 = Scenario.Create("scenario_1", async context =>
            {
                string randomKey = this.idPkMap.Keys.ElementAt(this.rand.Next(0, this.idPkMap.Count));
                string pk = this.idPkMap[randomKey];
                ItemResponse<ToDoActivity> readResponse = await this.container.ReadItemAsync<ToDoActivity>(randomKey, new PartitionKey(pk));
                return Response.Ok();
            })
            .WithoutWarmUp()
            .WithLoadSimulations( new[] {
                 Simulation.Inject(rate: 10000, interval: TimeSpan.FromSeconds(1),  during: TimeSpan.FromSeconds(300)),
                })
            .WithInit(async context =>
            {
                string partitionKey = "/pk";
                string databaseName = "MyTestDatabase";
                string containerName = "MyTestContainer";
                string endpoint = "<test>";
                string authKey = "<test>";
                string connectionString = $"AccountEndpoint={endpoint};AccountKey={authKey};";

                Environment.SetEnvironmentVariable("AZURE_COSMOS_REPLICA_VALIDATION_ENABLED", "true");
                CosmosClientOptions clientOptions = new()
                {
                    ApplicationPreferredRegions = new List<string>()
                {
                    Regions.WestCentralUS,
                    Regions.EastUS2,
                    Regions.WestUS,
                 }, // Remove this for actual run.
                };

                CosmosClient cosmosClient = new(
                    connectionString: connectionString,
                    clientOptions: clientOptions);

                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                       id: databaseName);

                ContainerResponse containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
                    containerProperties: new ContainerProperties(
                        id: containerName,
                        partitionKeyPath: partitionKey),
                    throughput: 20000,
                    cancellationToken: CancellationToken.None);

                await Task.Delay(4000);
                this.database = databaseResponse.Database;
                this.container = containerResponse.Container; // use get methods.

                for (int i=0; i<1000; i++)
                {
                    try
                    {
                        ToDoActivity activityItem = ToDoActivity.CreateRandomToDoActivity(randomTaskNumber: true);

                        // Create Item.
                        ItemResponse<ToDoActivity> createResponse = await this.container.CreateItemAsync<ToDoActivity>(activityItem);

                        this.idPkMap[activityItem.id] = activityItem.pk;
                    }
                    catch (Exception ex)
                    {
                        context.Logger.Error("Create Item Failed: " + ex.Message);
                    }
                }

                // You can do here any initialization logic: populate the database, etc.
                context.Logger.Information("Initialization Completed.");
                await Task.CompletedTask;
            })
            .WithClean(context =>
            {
                // You can do here any cleaning logic: clearing the database, etc.
                context.Logger.Information("Cleanup Completed.");
                return Task.CompletedTask;
            });

            NBomberRunner
                .RegisterScenarios(scn1)
                .Run();
        }

        public class ToDoActivity
        {
#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }


            public int taskNum { get; set; }

            public double cost { get; set; }

            public string description { get; set; }

            public string pk { get; set; }

            public string CamelCase { get; set; }

            public int? nullableInt { get; set; }

            public bool valid { get; set; }

            public ToDoActivity[] children { get; set; }
#pragma warning restore IDE1006 // Naming Styles

            public override bool Equals(Object obj)
            {
                if (obj is not ToDoActivity input)
                {
                    return false;
                }

                return string.Equals(this.id, input.id)
                    && this.taskNum == input.taskNum
                    && this.cost == input.cost
                    && string.Equals(this.description, input.description)
                    && string.Equals(this.pk, input.pk);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static async Task<IList<ToDoActivity>> CreateRandomItems(
                Microsoft.Azure.Cosmos.Container container,
                int pkCount,
                int perPKItemCount = 1,
                bool randomPartitionKey = true,
                bool randomTaskNumber = false)
            {
                List<ToDoActivity> createdList = new List<ToDoActivity>();
                for (int i = 0; i < pkCount; i++)
                {
                    string pk = "PKC";
                    if (randomPartitionKey)
                    {
                        pk += Guid.NewGuid().ToString();
                    }

                    for (int j = 0; j < perPKItemCount; j++)
                    {
                        ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity(
                            pk: pk,
                            id: null,
                            randomTaskNumber: randomTaskNumber);

                        createdList.Add(temp);

                        await container.CreateItemAsync<ToDoActivity>(item: temp);
                    }
                }

                return createdList;
            }

            public static ToDoActivity CreateRandomToDoActivity(
                string pk = null,
                string id = null,
                bool randomTaskNumber = false)
            {
                if (string.IsNullOrEmpty(pk))
                {
                    pk = "PKC" + Guid.NewGuid().ToString();
                }
                id ??= Guid.NewGuid().ToString();

                int taskNum = 42;
                if (randomTaskNumber)
                {
                    taskNum = Random.Shared.Next();
                }

                return new ToDoActivity()
                {
                    id = id,
                    description = "CreateRandomToDoActivity",
                    pk = pk,
                    taskNum = taskNum,
                    cost = double.MaxValue,
                    CamelCase = "camelCase",
                    children = new ToDoActivity[]
                    { new ToDoActivity { id = "child1", taskNum = 30 },
                  new ToDoActivity { id = "child2", taskNum = 40}
                    },
                    valid = true,
                    nullableInt = null
                };
            }
        }
    }
}


