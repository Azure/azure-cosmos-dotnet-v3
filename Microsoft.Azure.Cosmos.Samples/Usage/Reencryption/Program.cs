namespace Cosmos.Samples.Reencryption
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Data.Encryption.AzureKeyVaultProvider;
    using Microsoft.Extensions.Configuration;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/en-us/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos.Encryption NuGet package - 
    //    https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample/Driver - demonstrates, how reencryption of encrypted data in the Cosmos DB can be carried out using Always Encrypted CosmosDB SDK Client-Side encryption.
    // This can be used to change/rotate Data Encryption Keys or change the Client Encryption Policy.
    // The sample creates seperate tasks for each of the feed range, and saves the continuation token if the task is interrupted in between
    // or if the user decides to stop the reencryption and decides to continue it later.
    // The user gets the option to use an existing continuationToken/bookmark from the last saved checkpoint which was saved earlier in a file. If the reencryption activity is
    // finished the user can discard the file.
    //
    // SrcDatabase - database containing the containers that needs to be reencrypted with new data encryption key or if a change in encryption policy is required.
    //
    // SrcContainer - Container Id which requires a reencryption of data or change in encryption policy(you might want to add additional paths to be encrypted etc.).
    //
    // DstContainer - Destination container(should be created in advance), with new encryption policy set. This container will now house the reencrypted data.
    //
    // If you wish to change the data encryption key then you can use the same policy but want to change the keys for each of the policy paths.
    // The new keys should be created prior its usage in the policy. The destination container
    // should be created with the new policy before you use it in this sample/driver code.
    //
    // Note:
    // IsFFChangeFeedSupported(in Constants.cs file) value has been set to false. Full Fidelity change feed is in Preview mode and requires it to be enabled. This allows for reencryption
    // to carried on, when the source container is still receiving writes when reencryption is being carried.This can be set to true when the feature is available or is enabled on the
    // database account. If the feature is not enabled, please make sure the source container is not receiving any writes before you carry out the reencryption activity.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static string SrcDatabase = null;
        private static string SrcContainer = null;
        private static string DstContainer = null;
        private static readonly string ContinuationTokenFile = "continuationTokenFile.txt";
        private static CosmosClient client = null;
        private static string MasterKeyUrl = null;

        // <Main>
        public static async Task Main(string[] _)
        {
            try
            {
                // Read the Cosmos endpointUrl and authorizationKey from configuration.
                // These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys".
                // Keep these values in a safe and secure location. Together they provide administrative access to your Cosmos account.
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                // Get the Akv Master Key Path.
                MasterKeyUrl = configuration["MasterKeyUrl"];
                if (string.IsNullOrEmpty(MasterKeyUrl))
                {
                    throw new ArgumentNullException("Please specify a valid Azure Key Path in the appSettings.json");
                }

                // Get the Source Database name. 
                SrcDatabase = configuration["SrcDatabase"];
                if (string.IsNullOrEmpty(SrcDatabase))
                {
                    throw new ArgumentNullException("Please specify a valid database name. ");
                }

                // Get the Source Container name.
                SrcContainer = configuration["SrcContainer"];
                if (string.IsNullOrEmpty(SrcContainer))
                {
                    throw new ArgumentNullException("Please specify a valid container name. ");
                }

                // Get the Destination Container name.
                DstContainer = configuration["DstContainer"];
                if (string.IsNullOrEmpty(DstContainer))
                {
                    throw new ArgumentNullException("Please specify a valid container name. ");
                }

                // Get the Token Credential that is capable of providing an OAuth Token.
                TokenCredential tokenCredential = GetTokenCredential(configuration);
                AzureKeyVaultKeyStoreProvider azureKeyVaultKeyStoreProvider = new AzureKeyVaultKeyStoreProvider(tokenCredential);

                Program.client = Program.CreateClientInstance(configuration, azureKeyVaultKeyStoreProvider);

                await Program.CreateAndRunReencryptionTasks(client);
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Message: {0} Error: {1}", baseException.Message, e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        private static CosmosClient CreateClientInstance(IConfigurationRoot configuration, AzureKeyVaultKeyStoreProvider azureKeyVaultKeyStoreProvider)
        {
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

            CosmosClientOptions options = new CosmosClientOptions
            {
                AllowBulkExecution = true,
            };
            CosmosClient encryptionCosmosClient = new CosmosClient(endpoint, authKey, options);

            // enable encryption support on the cosmos client.
            return encryptionCosmosClient.WithEncryption(azureKeyVaultKeyStoreProvider);
        }

        private static X509Certificate2 GetCertificate(string clientCertThumbprint)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certs = store.Certificates.Find(findType: X509FindType.FindByThumbprint, findValue: clientCertThumbprint, validOnly: false);
            store.Close();

            if (certs.Count == 0)
            {
                throw new ArgumentException("Certificate with thumbprint not found in CurrentUser certificate store");
            }

            return certs[0];
        }

        private static TokenCredential GetTokenCredential(IConfigurationRoot configuration)
        {
            // Application credentials for authentication with Azure Key Vault.
            // This application must have keys/wrapKey and keys/unwrapKey permissions
            // on the keys that will be used for encryption.
            string clientId = configuration["ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentNullException("Please specify a valid ClientId in the appSettings.json");
            }

            // Get the Tenant ID 
            string tenantId = configuration["TenantId"];
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentNullException("Please specify a valid TenantId in the appSettings.json");
            }

            // Certificate's public key must be at least 2048 bits.
            string clientCertThumbprint = configuration["ClientCertThumbprint"];
            if (string.IsNullOrEmpty(clientCertThumbprint))
            {
                throw new ArgumentNullException("Please specify a valid ClientCertThumbprint in the appSettings.json");
            }

            return new ClientCertificateCredential(tenantId, clientId, Program.GetCertificate(clientCertThumbprint));
        }


        private static async Task CreateAndRunReencryptionTasks(CosmosClient client)
        {
            Container sourceContainer = client.GetContainer(SrcDatabase, SrcContainer);
            Container targetContainer = client.GetContainer(SrcDatabase, DstContainer);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            // get the feed ranges
            IReadOnlyList<FeedRange> ranges = await sourceContainer.GetFeedRangesAsync();

            // create a reencryption task for each feed range.
            List<Task> reencryptionTasks = new List<Task>();

            foreach (FeedRange feedRange in ranges)
            {
                Console.WriteLine("Creating task for reencryption, tapping into feedrange: {0}", feedRange.ToString());
                reencryptionTasks.Add(ExecuteReencrytionAsync(sourceContainer, feedRange, cancellationTokenSource.Token));
            }

            Console.WriteLine("\n Reencryption Progress. Press esc key to exit. \n");
            await CheckReencryptionProgressOrCancelReencryptionTasksAsync(sourceContainer, targetContainer, cancellationTokenSource);

            try
            {
                await Task.WhenAll(reencryptionTasks);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Reencryption operation was cancelled.");
            }
            finally
            {
                Console.WriteLine("\n If the reencryption task is complete,do you want to delete the continuation token file.(y/n) \n");                
                while (true)
                {
                    string ans = Console.ReadLine();
                    if (ans != null && ans == "y")
                    {
                        foreach (FeedRange feedRange in ranges)
                        {
                            System.IO.File.Delete(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id);
                        }
                        break;
                    }
                    else if (ans != null && ans == "n")
                    {
                        break;
                    }
                    else
                    {
                        Console.Write("\n Only y or n Allowed \n");
                    }
                }
                cancellationTokenSource.Dispose();
            }
        }


        private static async Task ExecuteReencrytionAsync(Container sourceContainer, FeedRange feedRange = null,CancellationToken cancellationToken = default)
        {
            string continuationToken = null;

            try
            {
                bool checkifContTokenFile = System.IO.File.Exists(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id);

                if (checkifContTokenFile)
                {
                    Console.WriteLine("\n ContinuationToken/Bookmark file found for this container and feedrange. Do you want to use it.(y/n) \n");
                    while (true)
                    {
                        string ans = Console.ReadLine();
                        if (ans != null && ans == "y")
                        {
                            continuationToken = System.IO.File.ReadAllText(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id);
                            break;
                        }
                        else if (ans != null && ans == "n")
                        {
                            break;
                        }
                        else
                        {
                            Console.Write("\n Only y or n Allowed \n");
                        }
                    }
                }
            }
            catch(Exception)
            {
                Console.WriteLine("No continuation token file found for this feedrange:", @"continuationTokenFile.txt" + feedRange.ToString());
            }

            if(string.IsNullOrEmpty(continuationToken))
            {
                continuationToken = null;
            }

            ReencryptionResponseMessage responseMessage;
            do
            {
                responseMessage = await ReencryptionHelperAsync(sourceContainer, feedRange, continuationToken, cancellationToken);

                if (responseMessage != null)
                {
                    continuationToken = responseMessage.ContinuationToken;
                }

            } while (responseMessage != null);

            return;
        }

        private static async Task<ReencryptionResponseMessage> ReencryptionHelperAsync(
            Container sourceContainer,
            FeedRange feedRange = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            ReencryptionIterator iterator = await sourceContainer.GetReencryptionIteratorAsync(
                DstContainer,
                client,
                CheckAndSetWritesAsStopped,
                sourceFeedRange: feedRange,
                continuationToken: continuationToken);

            ReencryptionResponseMessage responseMessage = null;

            while (iterator.HasMoreResults)
            {
                responseMessage = await iterator.EncryptNextAsync(cancellationToken);                
                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                System.IO.File.WriteAllText(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id, responseMessage.ContinuationToken);
            }

            if (iterator.HasMoreResults == false)
            {
                return null;
            }
            
            return responseMessage;
        }

        private static bool CheckAndSetWritesAsStopped()
        {
            // return true, indicating the writes on the source container have stopped.
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                return true;
            }

            // set this to false only if Full Fidelity change feed is enabled on your account.
            return true;
        }

        private static async Task CheckReencryptionProgressOrCancelReencryptionTasksAsync(Container sourceContainer, Container targetContainer, CancellationTokenSource cancellationToken)
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    cancellationToken.Cancel();
                    return;
                }

                // fetch and display the progress every 3 seconds.
                await Task.Delay(3000);
                await GetMigrationProgressPercentageAsync(sourceContainer, targetContainer, cancellationToken.Token);
            }
        }

        private static async Task<float> GetMigrationProgressPercentageAsync(Container sourceContainer, Container targetContainer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ContainerRequestOptions containerRequestOptions = new ContainerRequestOptions { PopulateQuotaInfo = true };
            ContainerResponse result = await sourceContainer.ReadContainerAsync(containerRequestOptions);
            string usage = result.Headers["x-ms-resource-usage"];
            string[] quotas = usage.Split(';');
            long sourceContainerTotalDocCount = long.Parse(quotas.GetValue(5).ToString().Split('=').GetValue(1).ToString());

            result = await targetContainer.ReadContainerAsync(containerRequestOptions);
            usage = result.Headers["x-ms-resource-usage"];
            quotas = usage.Split(';');
            long destinationContainerTotalDocCount = long.Parse(quotas.GetValue(5).ToString().Split('=').GetValue(1).ToString());

            float progress = 100 * (float)((double)destinationContainerTotalDocCount / (double)sourceContainerTotalDocCount);
            if (progress > 100)
            {
                progress = 100 - (progress - 100);
            }

            if (destinationContainerTotalDocCount <= sourceContainerTotalDocCount)
            {
                ShowReencryptionProgressBar((int)destinationContainerTotalDocCount, (int)sourceContainerTotalDocCount, (int)progress);
            }
            else
            {
                destinationContainerTotalDocCount = sourceContainerTotalDocCount - (destinationContainerTotalDocCount - sourceContainerTotalDocCount);
                ShowReencryptionProgressBar((int)destinationContainerTotalDocCount, (int)sourceContainerTotalDocCount, (int)progress);
            }
            
            return progress;
        }

        private static void ShowReencryptionProgressBar(int progress, int total, int progressCurrent)
        {
            int progressBar = 40;

            Console.CursorLeft = 0;
            Console.CursorLeft = progressBar + 1;
            Console.CursorLeft = 1;

            double percentageCompletion = Convert.ToDouble(progress) / total;
            int currentCompletionBar = Convert.ToInt16(progressBar * percentageCompletion);

            Console.BackgroundColor = progressCurrent != 100 ? ConsoleColor.Red : ConsoleColor.Green;

            Console.Write(" ".PadRight(currentCompletionBar));

            Console.BackgroundColor = ConsoleColor.Gray;
            Console.Write(" ".PadRight(progressBar - currentCompletionBar));

            Console.CursorLeft = progressBar + 1;
            Console.BackgroundColor = ConsoleColor.Black;

            string output = " " + progress.ToString() + " of " + total.ToString() + " (" + progressCurrent.ToString() + " %) ";
            Console.Write(output.PadRight(15));
        }
    }
}
