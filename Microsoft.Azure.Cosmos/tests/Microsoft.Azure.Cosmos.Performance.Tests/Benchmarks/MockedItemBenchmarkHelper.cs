// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System.IO;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    public class MockedItemBenchmarkHelper
    {
        public static readonly string ExistingItemId = "lets-benchmark";
        public static readonly string NonExistingItemId = "cant-see-me";

        public static readonly PartitionKey ExistingPartitionId = new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId);

        internal ToDoActivity TestItem { get; }
        internal CosmosClient TestClient { get; }
        internal Container TestContainer { get; }
        internal byte[] TestItemBytes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockedItemBenchmark"/> class.
        /// </summary>
        public MockedItemBenchmarkHelper(bool useCustomSerializer = false)
        {
            this.TestClient = MockDocumentClient.CreateMockCosmosClient(useCustomSerializer);
            this.TestContainer = this.TestClient.GetDatabase("myDB").GetContainer("myColl");

            using (FileStream tmp = File.OpenRead("samplepayload.json"))
            using (MemoryStream ms = new MemoryStream())
            {
                tmp.CopyTo(ms);
                this.TestItemBytes = ms.ToArray();
            }

            using (MemoryStream ms = new MemoryStream(this.TestItemBytes))
            {
                string payloadContent = File.ReadAllText("samplepayload.json");
                this.TestItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);
            }
        }

        public MemoryStream GetItemPayloadAsStream()
        {
            return new MemoryStream(
                this.TestItemBytes,
                index: 0,
                count: this.TestItemBytes.Length,
                writable: false,
                publiclyVisible: true);
        }
    }
}
