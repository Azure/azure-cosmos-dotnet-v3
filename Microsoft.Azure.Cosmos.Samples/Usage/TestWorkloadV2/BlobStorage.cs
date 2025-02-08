namespace TestWorkloadV2
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    internal class BlobStorage : IDriver
    {
        private BlobServiceClient blobServiceClient;

        private BlobContainerClient blobContainerClient;

        private CommonConfiguration configuration;

        private DataSource dataSource;

        private Random random;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new CommonConfiguration();
            configurationRoot.Bind(this.configuration);

            string connectionString = configurationRoot.GetValue<string>(this.configuration.ConnectionStringRef);

            this.blobServiceClient = new BlobServiceClient(connectionString);
            this.configuration.ConnectionStringForLogging = this.blobServiceClient.AccountName;

            this.blobContainerClient = this.blobServiceClient.GetBlobContainerClient(this.configuration.ContainerName);
            
            if(this.configuration.ShouldRecreateContainerOnStart)
            {
                if(await this.blobContainerClient.ExistsAsync())
                {
                    await this.blobContainerClient.DeleteAsync();
                }

                await this.blobContainerClient.CreateAsync();
            }

            this.dataSource = await DataSource.CreateAsync(this.configuration,
                paddingGenerator: (DataSource d) =>
                {
                    (MemoryStream stream, _) = this.GetNextItem(d);
                    int currentLen = (int)stream.Length;
                    string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen) : string.Empty;
                    return Task.FromResult(padding);
                },
                initialItemIdFinder: async () =>
                {
                    long lastItemId = -1;
                    if (!this.configuration.ShouldRecreateContainerOnStart)
                    {
                        lastItemId = await this.BinarySearchExistingIdAsync(0, DataSource.WorkerIdMultiplier);
                    }

                    return lastItemId + 1;
                });

            this.random = new Random(CommonConfiguration.RandomSeed);

            return (this.configuration, this.dataSource);
        }

        private async Task<long> BinarySearchExistingIdAsync(long start, long end)
        {
            if (start == end)
            {
                return start;
            }

            long mid = (start + end) / 2;
            string midId = DataSource.GetId(mid);
            await foreach(BlobItem x in this.blobContainerClient.GetBlobsAsync(prefix: midId))
            {
                start = mid + 1;
                return await this.BinarySearchExistingIdAsync(start, end);
            }

            end = mid;
            return await this.BinarySearchExistingIdAsync(start, end);
        }

        public async Task CleanupAsync()
        {
            if (this.configuration.ShouldDeleteContainerOnFinish)
            {
                await this.blobContainerClient.DeleteAsync();
            }
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            context = null;

            if (this.configuration.RequestType == RequestType.Create)
            {
                (MemoryStream stream, string blobName) = this.GetNextItem(this.dataSource);
                return this.blobContainerClient.UploadBlobAsync(blobName, stream);
            }
            else if(this.configuration.RequestType == RequestType.PointRead)
            {
                long randomId = this.random.NextInt64(this.dataSource.InitialItemId);
                string id = DataSource.GetId(randomId);
                return this.blobContainerClient.GetBlobClient(id).DownloadContentAsync();
            }

            throw new NotImplementedException(this.configuration.RequestType.ToString());
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            ResponseAttributes responseAttributes;
            responseAttributes.RequestCharge = 0;

            //Task<Response<BlobContentInfo>> task = (Task<Response<BlobContentInfo>>)request;
            if (request.IsCompletedSuccessfully)
            {
                responseAttributes.StatusCode = HttpStatusCode.OK;
            }
            else
            {
                 Exception e = request.Exception;
                responseAttributes.StatusCode = HttpStatusCode.ServiceUnavailable; // todo: refine based on exception
            }

            return responseAttributes;
        }

        private (MemoryStream stream, string blobName) GetNextItem(DataSource dataSource)
        {
            (MyDocument myDocument, int _) = dataSource.GetNextItemToInsert();
            string value = JsonConvert.SerializeObject(myDocument, JsonSerializerSettings);
            return (new MemoryStream(Encoding.UTF8.GetBytes(value)), myDocument.Id.ToString());
        }
    }
}
