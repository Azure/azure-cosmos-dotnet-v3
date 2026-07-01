//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Drives a real Change Feed Processor (push model) via
    /// <see cref="Container.GetChangeFeedProcessorBuilder{T}(string, Container.ChangeFeedHandler{T})"/>
    /// and a dedicated lease container. This is the .NET analogue of the Java CFP workload and,
    /// unlike the pull-model <see cref="ReadFeedRangesV3BenchmarkOperation"/>, exercises the lease
    /// acquisition / load-balancing / estimator path — the same path that surfaces the Direct-mode
    /// memory behavior the Java side tracks (watch the dashboard GC-heap / memory panels on this op).
    ///
    /// Harness fit: the benchmark executor measures one <see cref="ExecuteOnceAsync"/> per op. A single
    /// process-wide processor (this class is instantiated once per --pl task, so the processor + queue
    /// are static) delivers batches through its delegate; each delivered change is pushed onto a channel
    /// and each <see cref="ExecuteOnceAsync"/> dequeues one change (op count == changes processed,
    /// RuCharges == that change's share of its batch's request charge). When no changes arrive within
    /// <see cref="IdleTimeout"/> (e.g. writes paused during a failover), an idle zero-RU result is
    /// returned so windows keep emitting instead of the executor blocking indefinitely.
    /// </summary>
    internal class ChangeFeedProcessorV3BenchmarkOperation : IBenchmarkOperation
    {
        private const string ProcessorName = "cosmosbench-cfp";
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(15);

        // Shared across every executor task in the process: one processor, one output queue.
        private static readonly Channel<double> ChangeQueue = Channel.CreateUnbounded<double>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        private static readonly SemaphoreSlim InitLock = new SemaphoreSlim(1, 1);
        private static volatile bool started;
        private static ChangeFeedProcessor processor;

        private readonly CosmosClient cosmosClient;
        private readonly string databaseName;
        private readonly string containerName;
        private readonly string leaseContainerName;

        public ChangeFeedProcessorV3BenchmarkOperation(
            CosmosClient cosmosClient,
            string dbName,
            string containerName,
            string partitionKeyPath,
            string sampleJson)
        {
            this.cosmosClient = cosmosClient;
            this.databaseName = dbName;
            this.containerName = containerName;
            this.leaseContainerName = containerName + "-leases";
            _ = partitionKeyPath;
            _ = sampleJson;
        }

        public BenchmarkOperationType OperationType => BenchmarkOperationType.Read;

        public async Task PrepareAsync()
        {
            if (started)
            {
                return;
            }

            await InitLock.WaitAsync();
            try
            {
                if (started)
                {
                    return;
                }

                Database database = this.cosmosClient.GetDatabase(this.databaseName);

                // Dedicated lease container (partitioned on /id, the CFP lease requirement).
                Container leaseContainer = await database.CreateContainerIfNotExistsAsync(
                    new ContainerProperties(this.leaseContainerName, "/id"),
                    throughput: 400);

                Container monitored = this.cosmosClient.GetContainer(this.databaseName, this.containerName);

                processor = monitored
                    .GetChangeFeedProcessorBuilder<Dictionary<string, object>>(
                        processorName: ProcessorName,
                        onChangesDelegate: HandleChangesAsync)
                    .WithInstanceName($"{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 40))
                    .WithLeaseContainer(leaseContainer)
                    .Build();

                await processor.StartAsync();
                started = true;
                Utility.TeeTraceInformation(
                    $"ChangeFeedProcessor started (monitored={this.containerName}, leases={this.leaseContainerName}).");
            }
            finally
            {
                InitLock.Release();
            }
        }

        public async Task<OperationResult> ExecuteOnceAsync()
        {
            using CancellationTokenSource cts = new CancellationTokenSource(IdleTimeout);
            try
            {
                double ru = await ChangeQueue.Reader.ReadAsync(cts.Token);
                return new OperationResult
                {
                    DatabseName = this.databaseName,
                    ContainerName = this.containerName,
                    OperationType = this.OperationType,
                    RuCharges = ru,
                    LazyDiagnostics = () => "change-feed-processor: delivered change",
                };
            }
            catch (OperationCanceledException)
            {
                // No changes within the idle window (e.g. writes paused during a failover). Emit an
                // idle poll so the metrics window still reports instead of blocking the executor.
                return new OperationResult
                {
                    DatabseName = this.databaseName,
                    ContainerName = this.containerName,
                    OperationType = this.OperationType,
                    RuCharges = 0,
                    LazyDiagnostics = () => "change-feed-processor: idle (no changes)",
                };
            }
        }

        /// <summary>
        /// CFP delegate: fan each delivered change out onto the shared queue, splitting the batch's
        /// request charge evenly so summed per-change RU ≈ the batch RU.
        /// </summary>
        private static Task HandleChangesAsync(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<Dictionary<string, object>> changes,
            CancellationToken cancellationToken)
        {
            if (changes == null || changes.Count == 0)
            {
                return Task.CompletedTask;
            }

            double perChangeRu = 0;
            try
            {
                perChangeRu = context.Headers.RequestCharge / changes.Count;
            }
            catch
            {
                perChangeRu = 0;
            }

            foreach (Dictionary<string, object> _ in changes)
            {
                ChangeQueue.Writer.TryWrite(perChangeRu);
            }

            return Task.CompletedTask;
        }
    }
}
