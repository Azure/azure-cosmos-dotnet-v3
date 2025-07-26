// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Cosmos;

const string CosmosBaseUri = "https://{0}.documents.azure.com:443/";
string accountName = "cosmosaot1";
string primaryKey = Environment.GetEnvironmentVariable("KEY");
Console.WriteLine($"COSMOS_PRIMARY_KEY: {primaryKey}");

CosmosClientOptions clientOptions = new CosmosClientOptions { AllowBulkExecution = true };
clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing = false;

CosmosClient client = new CosmosClient(
    string.Format(CosmosBaseUri, accountName),
    primaryKey,
    clientOptions);


FeedIterator<DatabaseProperties> iterator = client.GetDatabaseQueryIterator<DatabaseProperties>();
while (iterator.HasMoreResults)
{
    FeedResponse<DatabaseProperties> results = await iterator.ReadNextAsync();
    foreach (DatabaseProperties r in results)
    {
        Console.WriteLine(r.Id);
    }
}