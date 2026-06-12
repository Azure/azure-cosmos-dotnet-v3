namespace Cosmos.Samples.Shared
{
    using System;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites -
    //
    // 1. An Azure Cosmos account -
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. An Azure AD (Microsoft Entra ID) identity that has been granted a Cosmos DB
    //    data-plane role on the account (for example "Cosmos DB Built-in Data Contributor").
    //    https://learn.microsoft.com/azure/cosmos-db/how-to-setup-rbac
    //
    // 3. Microsoft.Azure.Cosmos and Azure.Identity NuGet packages.
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates connecting a CosmosClient using Azure AD (Microsoft Entra ID)
    //          via an Azure.Core TokenCredential, instead of an account key.
    //
    // Note: constructing a CosmosClient is lazy and does not perform any network call until the
    //       first operation, so this sample illustrates the AAD construction surface without
    //       requiring a live, RBAC-enabled account.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        // <Main>
        public static void Main(string[] _)
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

                // DefaultAzureCredential resolves a token from the environment, a managed identity,
                // the Azure CLI, Visual Studio, etc. Any Azure.Core TokenCredential works here.
                TokenCredential tokenCredential = new DefaultAzureCredential();

                Program.CreateClientWithTokenCredential(endpoint, tokenCredential);
                Program.CreateClientWithCustomRefreshInterval(endpoint, tokenCredential);
                Program.CreateClientWithBuilder(endpoint, tokenCredential);
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
        /// The simplest way to authenticate with Azure AD: pass a TokenCredential to the CosmosClient.
        /// The SDK acquires a token for the account scope and refreshes it in the background before expiry.
        /// </summary>
        private static void CreateClientWithTokenCredential(
            string endpoint,
            TokenCredential tokenCredential)
        {
            // <CreateClientWithTokenCredential>
            using CosmosClient client = new CosmosClient(endpoint, tokenCredential);

            // To eagerly initialize the connection and route to specific containers, use the
            // TokenCredential overload of CreateAndInitializeAsync, for example:
            //
            //   IReadOnlyList<(string, string)> containers = new List<(string, string)>
            //   {
            //       ("database-id", "container-id")
            //   };
            //   using CosmosClient initialized = await CosmosClient.CreateAndInitializeAsync(
            //       endpoint, tokenCredential, containers);
            // </CreateClientWithTokenCredential>
            Console.WriteLine($"Created CosmosClient for '{client.Endpoint}' using a TokenCredential.");
        }

        /// <summary>
        /// Controls how aggressively the background token refresh runs. The default is 50% of the
        /// token's lifetime; set <see cref="CosmosClientOptions.TokenCredentialBackgroundRefreshInterval"/>
        /// to override it. The recommended minimum is 5 minutes.
        /// </summary>
        private static void CreateClientWithCustomRefreshInterval(
            string endpoint,
            TokenCredential tokenCredential)
        {
            // <CreateClientWithCustomRefreshInterval>
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                TokenCredentialBackgroundRefreshInterval = TimeSpan.FromMinutes(30)
            };

            using CosmosClient client = new CosmosClient(endpoint, tokenCredential, clientOptions);
            // </CreateClientWithCustomRefreshInterval>
            Console.WriteLine(
                $"Created CosmosClient with a background token refresh interval of " +
                $"{clientOptions.TokenCredentialBackgroundRefreshInterval}.");
        }

        /// <summary>
        /// CosmosClientBuilder also accepts a TokenCredential for fluent configuration.
        /// </summary>
        private static void CreateClientWithBuilder(
            string endpoint,
            TokenCredential tokenCredential)
        {
            // <CreateClientWithBuilder>
            CosmosClientBuilder builder = new CosmosClientBuilder(endpoint, tokenCredential)
                .WithApplicationName("EntraIdAuthenticationSample");

            using CosmosClient client = builder.Build();
            // </CreateClientWithBuilder>

            // If your account requires a non-default token scope, set the environment variable
            // AZURE_COSMOS_AAD_SCOPE_OVERRIDE before creating the client. When it is not set, the
            // SDK requests "https://{account-host}/.default" and falls back to
            // "https://cosmos.azure.com/.default" if the resource scope is rejected (AADSTS500011).
            Console.WriteLine($"Created CosmosClient for '{client.Endpoint}' using CosmosClientBuilder.");
        }
    }
}
