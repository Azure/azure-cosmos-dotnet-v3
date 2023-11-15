namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    class Program
    {
        public static void Main()
        {
            Task.Delay(1);
            //FaultInjectionConditionBuilder cb = new FaultInjectionConditionBuilder();
            //FaultInjectionCondition condition = cb
            //    .WithOperationType(FaultInjectionOperationType.ReadItem)
            //    .Build();

            //FaultInjectionServerErrorResultBuilder rb = FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay);
            //FaultInjectionServerErrorResult ser = rb.WithTimes(1).WithDelay(TimeSpan.FromSeconds(3)).Build();

            //FaultInjectionRuleBuilder ruleBuilder = new FaultInjectionRuleBuilder("The FIRST RULE");
            //List<FaultInjectionRule> rules = new List<FaultInjectionRule> { ruleBuilder.WithCondition(condition).WithResult(ser).Build() };

            //FaultInjector injector = new FaultInjector(rules);

            //CosmosClientOptions clientOptions = new CosmosClientOptions()
            //{ 
            //    FaultInjector = injector
            //};

            //CosmosClient client = new(
            //    accountEndpoint: endpoint,
            //    authKeyOrResourceToken: key,
            //    clientOptions: clientOptions);

            //Database db = await client.CreateDatabaseIfNotExistsAsync("testDB", 400);

            //Container container;

            //Console.WriteLine("Creating Container if not exist");
            //ContainerProperties cp = new ContainerProperties()
            //{
            //    Id = "testContainer",
            //    PartitionKeyPath = "/l1"
            //};

            //container = await db.CreateContainerIfNotExistsAsync(cp);

            //try
            //{
            //    ItemResponse<dynamic> funfunfun = await container.ReadItemAsync<dynamic>("document0", new PartitionKey("0"));
            //    Console.WriteLine(funfunfun.Resource);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e.Message);
            //}

            //ItemResponse<dynamic> xsecond = await container.ReadItemAsync<dynamic>("document10", new PartitionKey("0"));
            //Console.WriteLine("\n\n\n\n\n\nThe following rules were applied:");

            //injector.GetApplicationContext().GetApplicationByRuleId("The FIRST RULE").ForEach(((DateTime time, Guid guid)a) => Console.WriteLine($"Time: {a.time}, Guid: {a.guid}"));

        }
    }
}
