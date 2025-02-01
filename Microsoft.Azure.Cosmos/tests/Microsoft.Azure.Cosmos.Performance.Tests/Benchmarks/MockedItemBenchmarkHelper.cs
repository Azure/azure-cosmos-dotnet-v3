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
                                .WithClientTelemetryOptions(new CosmosClientTelemetryOptions
                                {
                                    DisableDistributedTracing = !isDistributedTracingEnabled,
                                    IsClientMetricsEnabled = isClientMetricsEnabled
                                }));

            this.TestContainer = this.TestClient.GetDatabase("myDB").GetContainer("myColl");
            this.IncludeDiagnosticsToString = includeDiagnosticsToString;
            this.SerializationFormat = serializationFormat;

            string payloadContent = File.ReadAllText("samplepayload.json");

            this.TestItem = JsonConvert.DeserializeObject<ToDoActivity>(payloadContent);

            if (this.SerializationFormat == JsonSerializationFormat.Binary)
            {
                // Binary serialization path unchanged
                using (CosmosDBToNewtonsoftWriter writer = new CosmosDBToNewtonsoftWriter(JsonSerializationFormat.Binary))
                {
                    writer.Formatting = Formatting.None;
                    Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                    serializer.Serialize(writer, this.TestItem);
                    this.TestItemBytes = writer.GetResult().ToArray();
                }
            }
            else
            {
                using (MemoryStream ms = (MemoryStream)this.ConvertInputToTextStream(this.TestItem, new Newtonsoft.Json.JsonSerializer()))
                {
                    this.TestItemBytes = ms.ToArray();
                }
            }

            this.EnableBinaryEncoding = serializationFormat == JsonSerializationFormat.Binary;
        }

        public void IncludeDiagnosticToStringHelper(CosmosDiagnostics cosmosDiagnostics)
        {
            if (!this.IncludeDiagnosticsToString)
            {
                return;
            }

            string diagnostics = cosmosDiagnostics.ToString();
            if (string.IsNullOrEmpty(diagnostics))
            {
                throw new Exception("Diagnostics were unexpectedly empty.");
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

        internal Stream ConvertInputToTextStream<T>(T input, Newtonsoft.Json.JsonSerializer serializer)
        {
            MemoryStream streamPayload = new();
            using (StreamWriter streamWriter = new(
                streamPayload,
                encoding: new UTF8Encoding(false, true),
                bufferSize: 1024,
                leaveOpen: true))
            {
                using (Newtonsoft.Json.JsonWriter writer = new JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Formatting.None;
                    serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            // Reset position so the caller can read from the beginning
            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}