// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    public class ItemBenchmarkHelper
    {
        internal ToDoActivity TestItem { get; }
        internal CosmosClient TestClient { get; }
        internal Container TestContainer { get; }
        internal byte[] TestItemBytes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemBenchmark"/> class.
        /// </summary>
        public ItemBenchmarkHelper(bool useCustomSerialzier = false)
        {
            this.TestClient = MockDocumentClient.CreateMockCosmosClient(useCustomSerialzier);
            this.TestContainer = this.TestClient.GetDatabase("myDB").GetContainer("myColl");

            using (FileStream tmp = File.OpenRead("samplepayload.json"))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    tmp.CopyTo(ms);
                    this.TestItemBytes = ms.ToArray();
                }
            }

            using (MemoryStream ms = new MemoryStream(this.TestItemBytes))
            {
                string payloadContent = File.ReadAllText("samplepayload.json");
                this.TestItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);
            }
        }
    }
}
