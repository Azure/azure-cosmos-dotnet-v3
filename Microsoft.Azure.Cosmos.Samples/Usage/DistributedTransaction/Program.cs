namespace Cosmos.Samples.DistributedTransaction
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites -
    //
    // 1. An Azure Cosmos account -
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package -
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates a cross-container money transfer scenario.
    //
    // Containers:
    //   - USBankAccounts:    Holds US-based bank accounts (partitioned by /id)
    //   - CanadianAccounts:  Holds Canadian bank accounts (partitioned by /id)
    //   - ExchangeRates:     Holds currency exchange rates (partitioned by /id)
    //
    // This sample first seeds initial data into the three containers, then performs
    // a cross-container transfer using the DistributedWriteTransaction API.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private const string DatabaseId = "DistributedTransactionSample";
        private const string USBankContainerId = "USBankAccounts";
        private const string CanadianAccountContainerId = "CanadianAccounts";
        private const string ExchangeRateContainerId = "ExchangeRates";

        private static Database database;
        private static Container usBankContainer;
        private static Container canadianAccountContainer;
        private static Container exchangeRateContainer;

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

                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.InitializeAsync(client);
                    await Program.ReadAndPrintAllContainersAsync();
                    await Program.TransferUsdToCadAsync(client);
                    await Program.CleanupAsync();
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

        /// <summary>
        /// Creates the database and three containers, then seeds initial data.
        /// </summary>
        private static async Task InitializeAsync(CosmosClient client)
        {
            Console.WriteLine("Creating database and containers...");

            database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);

            usBankContainer = await CreateContainerIfNotExistsAsync(USBankContainerId);
            canadianAccountContainer = await CreateContainerIfNotExistsAsync(CanadianAccountContainerId);
            exchangeRateContainer = await CreateContainerIfNotExistsAsync(ExchangeRateContainerId);

            // Insert Contoso's US bank account with $100 USD
            BankAccount usAccount = new BankAccount
            {
                Id = "contoso",
                AccountHolder = "Contoso",
                Balance = 100.00m,
                Currency = "USD"
            };

            // Insert Contoso's Canadian bank account with $0 CAD
            BankAccount caAccount = new BankAccount
            {
                Id = "contoso",
                AccountHolder = "Contoso",
                Balance = 0.00m,
                Currency = "CAD"
            };

            // Insert the USD to CAD exchange rate (1 USD = 1.38 CAD)
            ExchangeRate usdToCad = new ExchangeRate
            {
                Id = "USDToCAD",
                FromCurrency = "USD",
                ToCurrency = "CAD",
                Rate = 1.38m,
                LastUpdated = DateTime.UtcNow.ToString("o")
            };

            await usBankContainer.UpsertItemAsync(usAccount, new PartitionKey(usAccount.Id));
            Console.WriteLine($"  Inserted into {USBankContainerId}: {usAccount}");

            await canadianAccountContainer.UpsertItemAsync(caAccount, new PartitionKey(caAccount.Id));
            Console.WriteLine($"  Inserted into {CanadianAccountContainerId}: {caAccount}");

            await exchangeRateContainer.UpsertItemAsync(usdToCad, new PartitionKey(usdToCad.Id));
            Console.WriteLine($"  Inserted into {ExchangeRateContainerId}: {usdToCad}");

            Console.WriteLine();
            Console.WriteLine("Initialization complete. All records seeded successfully.");
            Console.WriteLine();
        }

        /// <summary>
        /// Reads and prints the current values from all three containers using standard (non-DTX) point reads.
        /// </summary>
        private static async Task ReadAndPrintAllContainersAsync()
        {
            Console.WriteLine("=== Reading Current State (non-DTX point reads) ===");
            Console.WriteLine();

            BankAccount usAccount = (await usBankContainer.ReadItemAsync<BankAccount>(
                "contoso", new PartitionKey("contoso"))).Resource;
            Console.WriteLine($"  [{USBankContainerId}]       {usAccount}");

            BankAccount caAccount = (await canadianAccountContainer.ReadItemAsync<BankAccount>(
                "contoso", new PartitionKey("contoso"))).Resource;
            Console.WriteLine($"  [{CanadianAccountContainerId}]  {caAccount}");

            ExchangeRate rate = (await exchangeRateContainer.ReadItemAsync<ExchangeRate>(
                "USDToCAD", new PartitionKey("USDToCAD"))).Resource;
            Console.WriteLine($"  [{ExchangeRateContainerId}]    {rate}");

            Console.WriteLine();
        }

        /// <summary>
        /// Transfers Contoso's entire $100 USD balance to the Canadian account,
        /// but only if the exchange rate is still 1.38 (conditional write via ETag).
        /// Uses DistributedWriteTransaction for atomic cross-container consistency.
        /// </summary>
        // <TransferUsdToCadAsync>
        private static async Task TransferUsdToCadAsync(CosmosClient client)
        {
            Console.WriteLine("=== Cross-Container Transfer with Conditional Write ===");
            Console.WriteLine();

            // Read current state from all three containers
            ItemResponse<BankAccount> usResponse = await usBankContainer.ReadItemAsync<BankAccount>(
                "contoso", new PartitionKey("contoso"));
            BankAccount usAccount = usResponse.Resource;

            ItemResponse<BankAccount> caResponse = await canadianAccountContainer.ReadItemAsync<BankAccount>(
                "contoso", new PartitionKey("contoso"));
            BankAccount caAccount = caResponse.Resource;

            ItemResponse<ExchangeRate> rateResponse = await exchangeRateContainer.ReadItemAsync<ExchangeRate>(
                "USDToCAD", new PartitionKey("USDToCAD"));
            ExchangeRate exchangeRate = rateResponse.Resource;
            string rateETag = rateResponse.ETag;

            Console.WriteLine("Before transfer:");
            Console.WriteLine($"  Contoso US:  ${usAccount.Balance:F2} USD");
            Console.WriteLine($"  Contoso CA:  ${caAccount.Balance:F2} CAD");
            Console.WriteLine($"  Rate (USDToCAD): {exchangeRate.Rate}");
            Console.WriteLine($"  Rate ETag:   {rateETag}");
            Console.WriteLine();

            // Transfer all $100 USD, converting at 1.38
            decimal transferAmountUsd = 100.00m;
            decimal transferAmountCad = transferAmountUsd * exchangeRate.Rate;

            Console.WriteLine($"Transferring ${transferAmountUsd:F2} USD (= ${transferAmountCad:F2} CAD)...");
            Console.WriteLine("Condition: exchange rate ETag must match (rate == 1.38).");
            Console.WriteLine();

            // Update balances
            usAccount.Balance -= transferAmountUsd;
            caAccount.Balance += transferAmountCad;

            // Create a distributed write transaction spanning all three containers.
            // The exchange rate replace uses IfMatchEtag as a conditional write:
            // the transaction will fail if the exchange rate was modified since we read it.
            DistributedWriteTransaction transaction = client.CreateDistributedWriteTransaction();

            transaction
                .ReplaceItem(
                    container: usBankContainer,
                    partitionKey: new PartitionKey(usAccount.Id),
                    id: usAccount.Id,
                    resource: usAccount)
                .ReplaceItem(
                    container: canadianAccountContainer,
                    partitionKey: new PartitionKey(caAccount.Id),
                    id: caAccount.Id,
                    resource: caAccount)
                .ReplaceItem(
                    container: exchangeRateContainer,
                    partitionKey: new PartitionKey(exchangeRate.Id),
                    id: exchangeRate.Id,
                    resource: exchangeRate,
                    requestOptions: new DistributedTransactionRequestOptions
                    {
                        IfMatchEtag = rateETag
                    });

            // Commit: all three operations succeed atomically, or none do.
            // If the exchange rate document was changed (rate != 1.38), the ETag
            // won't match and the entire transaction is rejected.
            DistributedTransactionResponse response = await transaction.CommitTransactionAsync();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine("Transaction committed successfully!");
                Console.WriteLine($"  Status Code:    {response.StatusCode}");
                Console.WriteLine($"  Request Charge: {response.RequestCharge:F2} RUs");
                Console.WriteLine($"  Activity Id:    {response.ActivityId}");
                Console.WriteLine();

                // Read back updated balances
                usAccount = (await usBankContainer.ReadItemAsync<BankAccount>(
                    "contoso", new PartitionKey("contoso"))).Resource;
                caAccount = (await canadianAccountContainer.ReadItemAsync<BankAccount>(
                    "contoso", new PartitionKey("contoso"))).Resource;

                Console.WriteLine("After transfer:");
                Console.WriteLine($"  Contoso US:  ${usAccount.Balance:F2} USD");
                Console.WriteLine($"  Contoso CA:  ${caAccount.Balance:F2} CAD");
            }
            else if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                Console.WriteLine("Transaction REJECTED — exchange rate was modified (precondition failed).");
                Console.WriteLine("No accounts were changed. Retry with the updated rate.");
            }
            else
            {
                Console.WriteLine($"Transaction failed with status: {response.StatusCode}");
                Console.WriteLine($"  Error:           {response.ErrorMessage}");
                Console.WriteLine($"  Activity Id:     {response.ActivityId}");
                Console.WriteLine($"  Request Charge:  {response.RequestCharge:F2} RUs");
                Console.WriteLine($"  IsRetriable:     {response.IsRetriable}");
                Console.WriteLine($"  Idempotency:     {response.IdempotencyToken}");
                Console.WriteLine($"  DiagnosticString:{response.DiagnosticString}");
                Console.WriteLine($"  Diagnostics:");
                Console.WriteLine(response.Diagnostics?.ToString() ?? "(null)");
            }

            Console.WriteLine();
            response.Dispose();
        }
        // </TransferUsdToCadAsync>

        private static async Task CleanupAsync()
        {
            Console.WriteLine("Cleaning up...");
            await database.DeleteAsync();
            Console.WriteLine("Done.");
        }

        private static async Task<Container> CreateContainerIfNotExistsAsync(string containerId)
        {
            ContainerResponse response = await database.CreateContainerIfNotExistsAsync(
                id: containerId,
                partitionKeyPath: "/id",
                throughput: 400);
            return response.Container;
        }
    }
}

