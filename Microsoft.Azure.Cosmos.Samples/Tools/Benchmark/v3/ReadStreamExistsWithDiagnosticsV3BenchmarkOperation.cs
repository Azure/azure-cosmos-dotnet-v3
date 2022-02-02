//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal class ReadStreamExistsWithDiagnosticsV3BenchmarkOperation : IBenchmarkOperation
    {
        private readonly Container container;
        private readonly string partitionKeyPath;
        private readonly Dictionary<string, object> sampleJObject;

        private readonly string databsaeName;
        private readonly string containerName;

        private string nextExecutionItemPartitionKey;
        private string nextExecutionItemId;

        private static readonly TimeSpan LoggingThresholdSpan = TimeSpan.FromSeconds(1);
        private static readonly OptimisticLimiter optimisticLimiter = new OptimisticLimiter();

        public ReadStreamExistsWithDiagnosticsV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.databsaeName = dbName;
            this.containerName = containerName;

            this.container = cosmosClient.GetContainer(this.databsaeName, this.containerName);
            this.partitionKeyPath = partitionKeyPath.Replace("/", "");

            this.sampleJObject = JsonHelper.Deserialize<Dictionary<string, object>>(sampleJson);
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            using (ResponseMessage itemResponse = await this.container.ReadItemStreamAsync(
                        this.nextExecutionItemId,
                        new PartitionKey(this.nextExecutionItemPartitionKey)))
            {
                if (itemResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"ReadItem failed wth {itemResponse.StatusCode}");
                }

                if (itemResponse.Diagnostics.GetClientElapsedTime() > LoggingThresholdSpan)
                {
                    string diagnostics = itemResponse.Diagnostics.ToString();
                    optimisticLimiter.TryLogMessage(diagnostics);
                }

                return new OperationResult()
                {
                    DatabseName = databsaeName,
                    ContainerName = containerName,
                    RuCharges = itemResponse.Headers.RequestCharge,
                    CosmosDiagnostics = itemResponse.Diagnostics,
                    LazyDiagnostics = () => itemResponse.Diagnostics.ToString(),
                };
            }
        }

        public async Task PrepareAsync()
        {
            if (string.IsNullOrEmpty(this.nextExecutionItemId) ||
                string.IsNullOrEmpty(this.nextExecutionItemPartitionKey))
            {
                this.nextExecutionItemId = Guid.NewGuid().ToString();
                this.nextExecutionItemPartitionKey = Guid.NewGuid().ToString();

                this.sampleJObject["id"] = this.nextExecutionItemId;
                this.sampleJObject[this.partitionKeyPath] = this.nextExecutionItemPartitionKey;

                using (MemoryStream inputStream = JsonHelper.ToStream(this.sampleJObject))
                {
                    using (ResponseMessage itemResponse = await this.container.CreateItemStreamAsync(
                            inputStream,
                            new PartitionKey(this.nextExecutionItemPartitionKey)))
                    {

                        System.Buffers.ArrayPool<byte>.Shared.Return(inputStream.GetBuffer());

                        if (itemResponse.StatusCode != HttpStatusCode.Created)
                        {
                            throw new Exception($"Create failed with statuscode: {itemResponse.StatusCode}");
                        }
                    }
                }
            }
        }

        public class OptimisticLimiter 
        {
            private long counter = 0;
            private const long MaxCocncurrentLogs = 100;

            public void TryLogMessage(string message)
            {
                long counterValue = Interlocked.Read(ref this.counter);
                if (counterValue < OptimisticLimiter.MaxCocncurrentLogs)
                {
                    counterValue = Interlocked.Increment(ref this.counter);

                    try
                    {
                        if(counterValue < OptimisticLimiter.MaxCocncurrentLogs)
                        {
                            HighLatencyEventSource.Instance.LogMessage(message);
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref this.counter);
                    }
                }
            }
        }

        public class HighLatencyEventSource : EventSource
        {
            public static HighLatencyEventSource Instance = new HighLatencyEventSource();

            public void LogMessage(string message) 
            { 
                this.WriteEvent(1, message); 
            }
        }
    }
}
