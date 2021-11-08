namespace Cosmos.Samples.ReEncryption
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
    using System.IO;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    //
    // 1. Microsoft.Azure.Cosmos.Encryption NuGet package - 
    //    https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample/Driver - demonstrates, how reEncryption of encrypted data in the Cosmos DB can be carried out using Always Encrypted CosmosDB SDK Client-Side encryption.
    // This can be used to change/rotate Data Encryption Keys or change the Client Encryption Policy.
    // The sample creates seperate tasks for each of the feed range, and saves the continuation token if the task is interrupted in between
    // or if the user decides to stop the reEncryption and decides to continue it later.
    // The user gets the option to use an existing continuationToken/bookmark from the last saved checkpoint which was saved earlier in a file. If the reEncryption activity is
    // finished the user can discard the file.
    //
    // SrcDatabase - database containing the containers that needs to be reencrypted with new data encryption key or if a change in encryption policy is required.
    //
    // SrcContainer - Container Id which requires a reEncryption of data or change in encryption policy(you might want to add additional paths to be encrypted etc.).
    //
    // DstContainer - Destination container(should be created in advance), with new encryption policy set. This container will now house the reencrypted data.
    //
    // If you wish to change the data encryption key then, you can use the same policy, but you have to change the keys for each of the policy paths.
    // The new keys should be created prior its usage in the policy. The destination container
    // should be created with the new policy before you use it in this sample/driver code.
    //
    // Note:
    // IsFFChangeFeedSupported(in Constants.cs file) value has been set to false. Full Fidelity change feed is in Preview mode and has to be enabled on an account to use the feature. This allows for reEncryption
    // to be carried out, when the source container is still receiving writes.This can be set to true when the feature is available and is enabled on the
    // database account. If the feature is not enabled, please make sure the source container is not receiving any writes before you carry out the reEncryption activity.
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        private static string SourceDatabase = null;
        private static string SourceContainer = null;
        private static string DestinationContainer = null;
        private static readonly int PageHintSize = 100; 
        private static readonly string ContinuationTokenFile = "continuationTokenFile";
        private static CosmosClient client = null;

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

                // Get the Source Database name. 
                SourceDatabase = configuration["SourceDatabase"];
                if (string.IsNullOrEmpty(SourceDatabase))
                {
                    throw new ArgumentNullException("Please specify a valid database name. ");
                }

                // Get the Source Container name.
                SourceContainer = configuration["SourceContainer"];
                if (string.IsNullOrEmpty(SourceContainer))
                {
                    throw new ArgumentNullException("Please specify a valid container name. ");
                }

                // Get the Destination Container name.
                DestinationContainer = configuration["DestinationContainer"];
                if (string.IsNullOrEmpty(DestinationContainer))
                {
                    throw new ArgumentNullException("Please specify a valid container name. ");
                }

                // Get the Token Credential that is capable of providing an OAuth Token.
                TokenCredential tokenCredential = GetTokenCredential(configuration);
                AzureKeyVaultKeyStoreProvider azureKeyVaultKeyStoreProvider = new AzureKeyVaultKeyStoreProvider(tokenCredential);

                Program.client = Program.CreateClientInstance(configuration, azureKeyVaultKeyStoreProvider);

                await Program.CreateAndRunReEncryptionTasks(client);
            }
            catch (CosmosException cosmosException)
            {
                Console.WriteLine(cosmosException.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Message: {0} Error: {1}", baseException.Message, e);
            }
            finally
            {
                Console.WriteLine("ReEncryption activity has been stopped successfully.");
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

        private static async Task CreateAndRunReEncryptionTasks(CosmosClient client)
        {
            Container sourceContainer = client.GetContainer(SourceDatabase, SourceContainer);
            Container targetContainer = client.GetContainer(SourceDatabase, DestinationContainer);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            // get the feed ranges
            IReadOnlyList<FeedRange> ranges = await sourceContainer.GetFeedRangesAsync();

            // create a reEncryption task for each feed range.
            List<Task> reEncryptionTasks = new List<Task>();

            foreach (FeedRange feedRange in ranges)
            {
                Console.WriteLine("Creating task for reEncryption, tapping into feedrange: {0}", feedRange.ToString());
                reEncryptionTasks.Add(ExecuteReEncrytionAsync(sourceContainer, feedRange, cancellationTokenSource.Token));
            }

            Console.WriteLine("\n ReEncryption in progress. Press esc key to exit. \n");
            await CheckReEncryptionProgressOrCancelReEncryptionTasksAsync(sourceContainer, targetContainer, cancellationTokenSource);

            try
            {
                await Task.WhenAll(reEncryptionTasks);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("ReEncryption operation was cancelled.");
            }
            finally
            {
                Console.WriteLine("\n If the reEncryption task is complete,do you want to delete the continuation token file.(y/n) \n");                
                while (true)
                {
                    string ans = Console.ReadLine();
                    if (ans != null && ans == "y")
                    {
                        foreach (FeedRange feedRange in ranges)
                        {
                            File.Delete(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id);
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

        private static async Task ExecuteReEncrytionAsync(Container sourceContainer, FeedRange feedRange,CancellationToken cancellationToken = default)
        {
            string continuationToken = null;

            bool doesContinuationTokenFileExist = File.Exists(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id);

            if (doesContinuationTokenFileExist)
            {
                Console.WriteLine("\n ContinuationToken/Bookmark file found for this container and feedrange. Do you want to use it.(y/n) \n");
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input != null && input == "y")
                    {
                        continuationToken = File.ReadAllText(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id);
                        break;
                    }
                    else if (input != null && input == "n")
                    {
                        break;
                    }
                    else
                    {
                        Console.Write("\n Only y or n Allowed \n");
                    }
                }
            }

            if (string.IsNullOrEmpty(continuationToken))
            {
                continuationToken = null;
            }

            ReEncryptionResponseMessage responseMessage;
            do
            {
                responseMessage = await ReEncryptNextAsync(sourceContainer, feedRange, continuationToken, cancellationToken);

                if (responseMessage != null)
                {
                    continuationToken = responseMessage.ContinuationToken;
                }

            } while (responseMessage != null);
        }

        private static async Task<ReEncryptionResponseMessage> ReEncryptNextAsync(
            Container sourceContainer,
            FeedRange feedRange = null,
            string continuationToken = null,
            CancellationToken cancellationToken = default)
        {
            // make sure the containers are configured with the throughput depending on how many requests you want to send to the Azure Cosmos DB service.
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions
            {
                PageSizeHint = PageHintSize,
            };

            ReEncryptionIterator iterator = await sourceContainer.GetReEncryptionIteratorAsync(
                DestinationContainer,
                client,
                CheckAndSetWritesAsStopped,
                changeFeedRequestOptions,
                sourceFeedRange: feedRange,
                continuationToken: continuationToken);

            ReEncryptionResponseMessage responseMessage = null;

            while (iterator.HasMoreResults)
            {
                responseMessage = await iterator.EncryptNextAsync(cancellationToken);                
                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                File.WriteAllText(@ContinuationTokenFile + feedRange.ToString() + sourceContainer.Id, responseMessage.ContinuationToken);
            }

            if (iterator.HasMoreResults == false)
            {
                return null;
            }
            
            return responseMessage;
        }

        private static bool CheckAndSetWritesAsStopped()
        {
            //Note: If you have enabled FullFidelity on your account and have active writes, you can perhaps poll to check if writes on the source container has stopped and return true.
            return true;
        }

        private static async Task CheckReEncryptionProgressOrCancelReEncryptionTasksAsync(Container sourceContainer, Container targetContainer, CancellationTokenSource cancellationToken)
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
                await GetReEncryptionProgressPercentageAsync(sourceContainer, targetContainer, cancellationToken.Token);
            }
        }

        private static async Task<float> GetReEncryptionProgressPercentageAsync(Container sourceContainer, Container targetContainer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ContainerRequestOptions containerRequestOptions = new ContainerRequestOptions { PopulateQuotaInfo = true };
            ContainerResponse result = await sourceContainer.ReadContainerAsync(containerRequestOptions);
            string usage = result.Headers["x-ms-resource-usage"];
            int index = usage.IndexOf("documentsCount");
            string[] quotas = usage.Remove(0, index).Split(';');
            long sourceContainerTotalDocCount = long.Parse(quotas.GetValue(0).ToString().Split('=').GetValue(1).ToString());

            result = await targetContainer.ReadContainerAsync(containerRequestOptions);
            usage = result.Headers["x-ms-resource-usage"];
            index = usage.IndexOf("documentsCount");
            quotas = usage.Remove(0, index).Split(';');
            long destinationContainerTotalDocCount = long.Parse(quotas.GetValue(0).ToString().Split('=').GetValue(1).ToString());

            float progress = 100 * (float)((double)destinationContainerTotalDocCount / (double)sourceContainerTotalDocCount);
            if (progress > 100)
            {
                progress = 100 - (progress - 100);
            }

            if (destinationContainerTotalDocCount <= sourceContainerTotalDocCount)
            {
                ShowReEncryptionProgressBar((int)destinationContainerTotalDocCount, (int)sourceContainerTotalDocCount, (int)progress);
            }
            else
            {
                destinationContainerTotalDocCount = sourceContainerTotalDocCount - (destinationContainerTotalDocCount - sourceContainerTotalDocCount);
                ShowReEncryptionProgressBar((int)destinationContainerTotalDocCount, (int)sourceContainerTotalDocCount, (int)progress);
            }

            return progress;
        }

        private static void ShowReEncryptionProgressBar(int progress, int total, int progressCurrent)
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
