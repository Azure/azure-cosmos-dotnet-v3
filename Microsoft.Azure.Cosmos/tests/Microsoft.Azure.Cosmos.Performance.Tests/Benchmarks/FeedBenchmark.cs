// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace BenchmarkSDK
{
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    [MemoryDiagnoser]
    public class FeedBenchmark
    {
        private CosmosClient client;
        private Container containerOneHundred;
        private Container containerOneThousand;
        private Container containerTenThousand;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public FeedBenchmark()
        {
        }

        [GlobalSetup]
        public void Setup()
        {
            this.client = new CosmosClient(
                accountEndpoint: null,
                authKeyOrResourceToken: null);


            Database db = this.client.CreateDatabaseIfNotExistsAsync("BenchMarkDb").GetAwaiter().GetResult();
            this.containerOneHundred = this.CreateAndPopulateContainer(db, "OneHundredItems", 100);
            this.containerOneThousand = this.CreateAndPopulateContainer(db, "OneThousandItems", 1000);
            this.containerTenThousand = this.CreateAndPopulateContainer(db, "TenThousandItems", 10000);
        }

        [Benchmark]
        public async Task ReadFeedOfT100()
        {
            await this.ReadFeedOfT(this.containerOneHundred, 100);
        }

        [Benchmark]
        public async Task ReadFeedOfT1000()
        {
            await this.ReadFeedOfT(this.containerOneThousand, 1000);
        }

        [Benchmark]
        public async Task ReadFeedOfT10000()
        {
            await this.ReadFeedOfT(this.containerTenThousand, 10000);
        }

        private async Task ReadFeedOfT(Container container, int count)
        {
            FeedIterator<Book> resultIterator = container.GetItemQueryIterator<Book>();
            List<Book> allBooks = new List<Book>();
            while (resultIterator.HasMoreResults)
            {
                FeedResponse<Book> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception();
                }

                allBooks.AddRange(response);
            }

            if (allBooks.Count != count)
            {
                throw new Exception(allBooks.Count.ToString());
            }
        }

        [Benchmark]
        public async Task ReadFeedStream100()
        {
            await this.ReadFeedStream(this.containerOneHundred);
        }

        [Benchmark]
        public async Task ReadFeedStream1000()
        {
            await this.ReadFeedStream(this.containerOneThousand);
        }

        [Benchmark]
        public async Task ReadFeedStream10000()
        {
            await this.ReadFeedStream(this.containerTenThousand);
        }

        private async Task ReadFeedStream(Container container)
        {
            FeedIterator resultIterator = container.GetItemQueryStreamIterator();
            while (resultIterator.HasMoreResults)
            {
                using(ResponseMessage response = await resultIterator.ReadNextAsync())
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private Container CreateAndPopulateContainer(Database db, string id, int count)
        {
            ContainerResponse containerResponse = db.CreateContainerIfNotExistsAsync(
               id,
               "/pk",
               10000).GetAwaiter().GetResult();

            Container container = containerResponse;
            if (containerResponse.StatusCode == HttpStatusCode.Created)
            {
                Book book = Book.CreateRandomBook(pk: "Book");
                for (int i = 0; i < count; i++)
                {
                    book.id = Guid.NewGuid().ToString();
                    container.CreateItemAsync<Book>(book, new PartitionKey(book.pk)).GetAwaiter().GetResult();
                }
            }

            return container;
        }
    }
}
