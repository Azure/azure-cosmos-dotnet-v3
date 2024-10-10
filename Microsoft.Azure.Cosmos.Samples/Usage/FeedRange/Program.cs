namespace ContainerIsFeedRangePartOf
{
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates Container's IsFeedRangePartOfAsync operation.
    // 1. **Creating a Cosmos DB Container (if it doesn't exist)**  
    //    The method calls `CreateContainerIfNotExistsAsync` to ensure that a Cosmos DB `Container` is created in the database if it doesn’t already exist.  
    //    - A new container is created with the following properties:
    //      - **Id**: A unique ID is generated for the container using `Guid.NewGuid().ToString()`.
    //      - **PartitionKeyPath**: Set to `/pk`.
    // 
    // 2. **Comparing Feed Ranges**  
    //    The method compares two feed ranges (`x` and `y`) using the `IsFeedRangePartOfAsync` method of the `Container`.  
    //    - It checks whether `FeedRange y` is part of `FeedRange x`.
    //    - The method asynchronously waits for the result (`true` or `false`) of the comparison.
    // 
    // 3. **Asserting the Expected Result**  
    //    After comparing the feed ranges, the method checks if the result matches the expected outcome (passed as the `expectedResult` parameter).  
    //    - If the result does not match the expected value, a `Debug.Assert` is triggered with a message:
    //      - `"Expected result: true, but got: false"`, or a similar message depending on the expected and actual result.
    // 
    // 4. **Handling Exceptions**  
    //    A `try-catch` block is used to catch any exceptions that occur during the container creation or feed range comparison.  
    //    - A `Debug.Assert` ensures that no exceptions are expected, and if an exception occurs, it triggers an assertion failure with details of the exception.
    // 
    // 5. **Releasing Resources (optional)**  
    //    This step would typically include:
    //    - **Deleting the database**: If the `database` is not `null`, it can be deleted using `database.DeleteAsync()`.
    //    - **Disposing of the CosmosClient**: If the `cosmosClient` is not `null`, it can be disposed of to release resources.

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

            string? endpoint = configuration["EndPointUrl"];
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
            }

            string? authKey = configuration["AuthorizationKey"];
            if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
            {
                throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
            }

            CosmosClient cosmosClient = new CosmosClient(endpoint, authKey);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(id: Guid.NewGuid().ToString());

            try 
            {
                // Given a container with a partition key exists, when two feed ranges are 
                // compared, where one covers the full range and the other covers a subset up to a 
                // specific value, then the second range should be part of the first.
                await Program.GivenContainerWithPartitionKeyExists_WhenFeedRangesAreCompared_ThenResultShouldBeAsExpected(
                    database: database,
                    x: FeedRange.FromJsonString(toStringValue: "{\"Range\":{\"min\":\"\",\"max\":\"FFFFFFFFFFFFFFFF\"}}"),
                    y: FeedRange.FromJsonString(toStringValue: "{\"Range\":{\"min\":\"\",\"max\":\"3FFFFFFFFFFFFFFF\"}}"),
                    expectedResult: true).ConfigureAwait(false);

                // Given a container with a partition key exists, when two feed ranges are 
                // compared, with one having a range from the minimum to a specific value and the other 
                // having a range between two specific values, then the feed ranges should not overlap.
                await Program.GivenContainerWithPartitionKeyExists_WhenFeedRangesAreCompared_ThenResultShouldBeAsExpected(
                    database: database,
                    x: FeedRange.FromJsonString(toStringValue: "{\"Range\":{\"min\":\"3FFFFFFFFFFFFFFF\",\"max\":\"7FFFFFFFFFFFFFFF\"}}"),
                    y: FeedRange.FromJsonString(toStringValue: "{\"Range\":{\"min\":\"\",\"max\":\"3FFFFFFFFFFFFFFF\"}}"),
                    expectedResult: false).ConfigureAwait(false);

                // Given a container with a partition key exists, when a feed range and 
                // a partition key-based feed range are compared, then the partition key feed range 
                // should be part of the specified range.
                await Program.GivenContainerWithPartitionKeyExists_WhenFeedRangesAreCompared_ThenResultShouldBeAsExpected(
                    database: database,
                    x: FeedRange.FromJsonString(toStringValue: "{\"Range\":{\"min\":\"\",\"max\":\"FFFFFFFFFFFFFFFF\"}}"),
                    y: FeedRange.FromPartitionKey(
                        new PartitionKeyBuilder()
                            .Add("WA")
                            .Add(Guid.NewGuid().ToString())
                            .Build()
                    ),
                    expectedResult: true).ConfigureAwait(false);

                // Given a container with a partition key exists, when a partition key-based feed range 
                // and a feed range are compared, then the partition key feed range 
                // should not be part of the specified range.
                await Program.GivenContainerWithPartitionKeyExists_WhenFeedRangesAreCompared_ThenResultShouldBeAsExpected(
                    database: database,
                    x: FeedRange.FromJsonString(toStringValue: "{\"Range\":{\"min\":\"3FFFFFFFFFFFFFFF\",\"max\":\"7FFFFFFFFFFFFFFF\"}}"),
                    y: FeedRange.FromPartitionKey(
                        new PartitionKeyBuilder()
                            .Add("WA")
                            .Add(Guid.NewGuid().ToString())
                            .Build()
                    ),
                    expectedResult: false).ConfigureAwait(false);
            }
            catch (Exception exception) 
            {
                Console.WriteLine(exception);
            }
            finally
            {
                Console.WriteLine($"Deleting database {database.Id}");

                _ = await database?.DeleteAsync();

                cosmosClient?.Dispose();
            }
        }

        static async Task GivenContainerWithPartitionKeyExists_WhenFeedRangesAreCompared_ThenResultShouldBeAsExpected(
            Database database,
            FeedRange x,
            FeedRange y,
            bool expectedResult)
        {
            try
            {
                Container container = await database.CreateContainerIfNotExistsAsync(containerProperties: new ContainerProperties
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKeyPath = "/pk",
                });

                bool results = await container
                    .IsFeedRangePartOfAsync(
                        x: x,
                        y: y)
                    .ConfigureAwait(continueOnCapturedContext: false);

                Debug.Assert(results == expectedResult,
                    $"Expected result: {expectedResult}, but got: {results}");
            }
            catch (Exception exception)
            {
                Debug.Assert(exception == null, $"No exception is expected with this scenario. {exception}");
            }
        }
    }
}