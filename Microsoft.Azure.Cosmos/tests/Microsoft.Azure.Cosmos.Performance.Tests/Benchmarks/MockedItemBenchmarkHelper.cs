// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Newtonsoft.Json;

    /// <summary>
    /// Benchmark for Item related operations.
    /// </summary>
    public class MockedItemBenchmarkHelper
    {
        public static readonly string ExistingItemId = "lets-benchmark";
        public static readonly string NonExistingItemId = "cant-see-me";

        public static readonly PartitionKey ExistingPartitionId = new PartitionKey(MockedItemBenchmarkHelper.ExistingItemId);
        private readonly bool IncludeDiagnosticsToString;

        internal ToDoActivity TestItem { get; }
        internal CosmosClient TestClient { get; }
        internal Container TestContainer { get; }

        internal byte[] TestItemBytes { get; }
        internal bool EnableBinaryEncoding { get; }
        internal JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockedItemBenchmarkHelper"/> class.
        /// </summary>
        internal MockedItemBenchmarkHelper(
            bool useCustomSerializer = false,
            bool includeDiagnosticsToString = false,
            bool useBulk = false,
            bool isDistributedTracingEnabled = false,
            bool isClientMetricsEnabled = false,
            JsonSerializationFormat serializationFormat = JsonSerializationFormat.Text)
        {
            this.TestClient = MockDocumentClient.CreateMockCosmosClient(
                useCustomSerializer,
                (builder) => builder
                                .WithBulkExecution(useBulk)
                                .WithClientTelemetryOptions(new CosmosClientTelemetryOptions()
                                {
                                    DisableDistributedTracing = !isDistributedTracingEnabled,
                                    IsClientMetricsEnabled = isClientMetricsEnabled
                                }));

            this.TestContainer = this.TestClient.GetDatabase("myDB").GetContainer("myColl");
            this.IncludeDiagnosticsToString = includeDiagnosticsToString;
            this.SerializationFormat = serializationFormat;

            // Load the test item from the JSON file
            string payloadContent = File.ReadAllText("samplepayload.json");
            this.TestItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);

            // Serialize TestItem into the requested format (Text or Binary)
            if (this.SerializationFormat == JsonSerializationFormat.Binary)
            {
                using (CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(JsonSerializationFormat.Binary))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                    serializer.Serialize(writer, this.TestItem);
                    this.TestItemBytes = writer.GetResult().ToArray();
                }
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                using (StreamWriter sw = new StreamWriter(ms, new UTF8Encoding(false, true), 1024, true))
                using (Newtonsoft.Json.JsonWriter writer = new Newtonsoft.Json.JsonTextWriter(sw))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                    serializer.Serialize(writer, this.TestItem);
                    writer.Flush();
                    sw.Flush();
                    this.TestItemBytes = ms.ToArray();
                }
            }
            this.EnableBinaryEncoding = serializationFormat == JsonSerializationFormat.Binary;
        }

        public void IncludeDiagnosticToStringHelper(
            CosmosDiagnostics cosmosDiagnostics)
        {
            if (!this.IncludeDiagnosticsToString)
            {
                return;
            }

            string diagnostics = cosmosDiagnostics.ToString();
            if (string.IsNullOrEmpty(diagnostics))
            {
                throw new Exception();
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