namespace Cosmos.Samples.Shared
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    //------------------------------------------------------------------------------------------------
    // This sample demonstrates the use of Cosmos's server side JavaScript capabilities
    // using Stored Procedures
    //------------------------------------------------------------------------------------------------
    public class Program
    {
        //Assign a id for your database & collection 
        private static readonly string DatabaseId = "samples";
        private static readonly string ContainerId = "serversidejs-samples";

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static async Task Main(string[] args)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                //Read the Cosmos endpointUrl and authorisationKeys from configuration
                //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.RunDemoAsync(client, DatabaseId, ContainerId);
                }
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        // <RunDemoAsync>
        private static async Task RunDemoAsync(
            CosmosClient client,
            string databaseId,
            string containerId)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);

            ContainerProperties containerSettings = new ContainerProperties(containerId, "/LastName");

            // Delete the existing container to prevent create item conflicts
            using (await database.GetContainer(containerId).DeleteContainerStreamAsync())
            { }

            // Create with a throughput of 1000 RU/s
            Container container = await database.CreateContainerIfNotExistsAsync(
                containerSettings,
                throughput: 1000);

            //Run a simple script
            await Program.RunSimpleScript(container);

            // Run Bulk Import
            await Program.RunBulkImport(container);

            // Run OrderBy
            await Program.RunOrderBy(container);

            //// Uncomment to Cleanup
            //await database.DeleteAsync();
        }
        // </RunDemoAsync>

        /// <summary>
        /// Runs a simple script which just does a server side query
        /// </summary>
        // <RunSimpleScript>
        private static async Task RunSimpleScript(Container container)
        {
            // 1. Create stored procedure for script.
            string scriptFileName = @"js\SimpleScript.js";
            string scriptId = Path.GetFileNameWithoutExtension(scriptFileName);

            await TryDeleteStoredProcedure(container, scriptId);
            Scripts cosmosScripts = container.Scripts;
            StoredProcedureResponse sproc = await cosmosScripts.CreateStoredProcedureAsync(
                new StoredProcedureProperties(
                    scriptId, 
                    File.ReadAllText(scriptFileName)));

            // 2. Create a document.
            SampleDocument doc = new SampleDocument
            {
                Id = Guid.NewGuid().ToString(),
                LastName = "Estel",
                Headquarters = "Russia",
                Locations = new Location[] { new Location { Country = "Russia", City = "Novosibirsk" } },
                Income = 50000
            };

            ItemResponse<SampleDocument> created = await container.CreateItemAsync(doc, new PartitionKey(doc.LastName));

            // 3. Run the script. Pass "Hello, " as parameter. 
            // The script will take the 1st document and echo: Hello, <document as json>.
            StoredProcedureExecuteResponse<string> response = await container.Scripts.ExecuteStoredProcedureAsync<string>(
                scriptId,
                new PartitionKey(doc.LastName),
                new dynamic[] { "Hello" });

            Console.WriteLine("Result from script: {0}\r\n", response.Resource);

            await container.DeleteItemAsync<SampleDocument>(doc.Id, new PartitionKey(doc.LastName));
        }
        // </RunSimpleScript>

        /// <summary>
        /// Import many documents using stored procedure.
        /// </summary>
        // <RunBulkImport>
        private static async Task RunBulkImport(Container container)
        {
            string inputDirectory = @".\Data\";
            string inputFileMask = "*.json";
            int maxFiles = 2000;
            int maxScriptSize = 50000;

            // 1. Get the files.
            string[] fileNames = Directory.GetFiles(inputDirectory, inputFileMask);
            DirectoryInfo di = new DirectoryInfo(inputDirectory);
            FileInfo[] fileInfos = di.GetFiles(inputFileMask);

            // 2. Prepare for import.
            int currentCount = 0;
            int fileCount = maxFiles != 0 ? Math.Min(maxFiles, fileNames.Length) : fileNames.Length;

            // 3. Create stored procedure for this script.
            string scriptId = "BulkImport";
            string body = File.ReadAllText(@".\JS\BulkImport.js");

            await TryDeleteStoredProcedure(container, scriptId);
            Scripts cosmosScripts = container.Scripts;
            StoredProcedureResponse sproc = await cosmosScripts.CreateStoredProcedureAsync(new StoredProcedureProperties(scriptId, body));

            // 4. Create a batch of docs (MAX is limited by request size (2M) and to script for execution.
            // We send batches of documents to create to script.
            // Each batch size is determined by MaxScriptSize.
            // MaxScriptSize should be so that:
            // -- it fits into one request (MAX request size is 16Kb).
            // -- it doesn't cause the script to time out.
            // -- it is possible to experiment with MaxScriptSize to get best performance given number of throttles, etc.
            while (currentCount < fileCount)
            {
                // 5. Create args for current batch.
                //    Note that we could send a string with serialized JSON and JSON.parse it on the script side,
                //    but that would cause script to run longer. Since script has timeout, unload the script as much
                //    as we can and do the parsing by client and framework. The script will get JavaScript objects.
                string argsJson = CreateBulkInsertScriptArguments(fileNames, currentCount, fileCount, maxScriptSize);
                dynamic[] args = new dynamic[] { JsonConvert.DeserializeObject<dynamic>(argsJson) };

                // 6. execute the batch.
                StoredProcedureExecuteResponse<int> scriptResult = await cosmosScripts.ExecuteStoredProcedureAsync<int>(
                    scriptId,
                    new PartitionKey("Andersen"),
                    args);

                // 7. Prepare for next batch.
                int currentlyInserted = scriptResult.Resource;
                currentCount += currentlyInserted;
            }

            // 8. Validate
            int numDocs = 0;

            using (FeedIterator<dynamic> setIterator = container.GetItemQueryIterator<dynamic>())
            {
                while (setIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await setIterator.ReadNextAsync();
                    numDocs += response.Count();
                }
            }

            Console.WriteLine("Found {0} documents in the collection. There were originally {1} files in the Data directory\r\n", numDocs, fileCount);
        }
        // </RunBulkImport>

        /// <summary>
        /// Get documents ordered by some doc property. This is done using OrderBy stored procedure.
        /// </summary>
        // <RunOrderBy>
        private static async Task RunOrderBy(Container container)
        {
            // 1. Create or get the stored procedure.
            string body = File.ReadAllText(@"js\OrderBy.js");
            string scriptId = "OrderBy";

            await TryDeleteStoredProcedure(container, scriptId);
            Scripts cosmosScripts = container.Scripts;
            StoredProcedureResponse sproc = await cosmosScripts.CreateStoredProcedureAsync(new StoredProcedureProperties(scriptId, body));

            // 2. Prepare to run stored procedure. 
            string orderByFieldName = "FamilyId";
            string filterQuery = string.Format(CultureInfo.InvariantCulture, "SELECT r.FamilyId FROM root r WHERE r.{0} > 10", orderByFieldName);
            // Note: in order to do a range query (> 10) on this field, the collection must have a range index set for this path (see ReadOrCreateCollection).

            int? continuationToken = null;
            int batchCount = 0;
            do
            {
                // 3. Run the stored procedure.
                StoredProcedureExecuteResponse<OrderByResult> response = await cosmosScripts.ExecuteStoredProcedureAsync<OrderByResult>(
                    scriptId,
                    new PartitionKey("Andersen"),
                    new dynamic[] { filterQuery, orderByFieldName, continuationToken });

                // 4. Process stored procedure response.
                continuationToken = response.Resource.Continuation;

                Console.WriteLine($"Printing documents filtered/ordered by '{filterQuery}' and ordered by '{orderByFieldName}', batch #{batchCount++}, count #{response.Resource.Result.Length}");
            } while (continuationToken != null);
            // 5. To take care of big response, loop until Response.continuation token is null (see OrderBy.js for details).
        }
        // </RunOrderBy>

        public class Location
        {
            public string City { get; set; }
            public string Country { get; set; }
        }
        public class SampleDocument
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            public string LastName { get; set; }
            public Location[] Locations { get; set; }
            public string Headquarters { get; set; }
            public int Income { get; set; }
        }

        public class LoggingEntry
        {
            [JsonProperty("size")]
            public int Size { get; set; }

            [JsonProperty("LastName")]
            public string DeviceId { get; set; }
        }

        public class LoggingAggregateEntry
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("isMetadata")]
            public bool IsMetadata { get; set; }

            [JsonProperty("minSize")]
            public int MinSize { get; set; }

            [JsonProperty("maxSize")]
            public int MaxSize { get; set; }

            [JsonProperty("totalSize")]
            public int TotalSize { get; set; }

            [JsonProperty("LastName")]
            public string DeviceId { get; set; }
        }

        internal class OrderByResult
        {
            public dynamic[] Result { get; set; }
            public int? Continuation { get; set; }
        }

        /// <summary>
        /// Creates the script for insertion
        /// </summary>
        /// <param name="currentIndex">the current number of documents inserted. this marks the starting point for this script</param>
        /// <param name="maxScriptSize">the maximum number of characters that the script can have</param>
        /// <returns>Script as a string</returns>
        // <CreateBulkInsertScriptArguments>
        private static string CreateBulkInsertScriptArguments(string[] docFileNames, int currentIndex, int maxCount, int maxScriptSize)
        {
            StringBuilder jsonDocumentArray = new StringBuilder();
            jsonDocumentArray.Append("[");

            if (currentIndex >= maxCount)
            {
                return string.Empty;
            }

            jsonDocumentArray.Append(File.ReadAllText(docFileNames[currentIndex]));

            int scriptCapacityRemaining = maxScriptSize;
            string separator = string.Empty;

            int i = 1;
            while (jsonDocumentArray.Length < scriptCapacityRemaining && (currentIndex + i) < maxCount)
            {
                jsonDocumentArray.Append(", " + File.ReadAllText(docFileNames[currentIndex + i]));
                i++;
            }

            jsonDocumentArray.Append("]");
            return jsonDocumentArray.ToString();
        }
        // </CreateBulkInsertScriptArguments>

        /// <summary>
        /// If a Stored Procedure is found on the DocumentCollection for the Id supplied it is deleted
        /// </summary>
        /// <param name="collectionLink">DocumentCollection to search for the Stored Procedure</param>
        /// <param name="sprocId">Id of the Stored Procedure to delete</param>
        /// <returns></returns>\
        // <TryDeleteStoredProcedure>
        private static async Task TryDeleteStoredProcedure(Container container, string sprocId)
        {
            Scripts cosmosScripts = container.Scripts;

            try
            {
                StoredProcedureResponse sproc = await cosmosScripts.ReadStoredProcedureAsync(sprocId);
                await cosmosScripts.DeleteStoredProcedureAsync(sprocId);
            }
            catch(CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                //Nothing to delete
            }
        }
        // </TryDeleteStoredProcedure>
    }
}
