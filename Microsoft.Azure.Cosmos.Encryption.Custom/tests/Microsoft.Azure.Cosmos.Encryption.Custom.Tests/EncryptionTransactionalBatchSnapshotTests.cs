//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class EncryptionTransactionalBatchSnapshotTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteAsync_DeferredSdkCaptureKeepsAdditionInNextExecution(bool useRequestOptions)
        {
            DeferredBatchFactory factory = new ();
            List<string> processors = new ();
            using ActivityListener listener = new ()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity =>
                {
                    if (activity.DisplayName.StartsWith(CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix, StringComparison.Ordinal))
                    {
                        processors.Add(activity.DisplayName);
                    }
                },
            };
            ActivitySource.AddActivityListener(listener);
            TransactionalBatch batch = CreateBatch(factory.Create);
            batch.ReadItem("A", CreateFirstItemOptions());
            SynchronizationContext originalContext = SynchronizationContext.Current;
            Task<TransactionalBatchResponse> firstTask;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                firstTask = ExecuteAsync(batch, useRequestOptions);
                Assert.IsFalse(firstTask.IsCompleted);
                batch.ReadItem("B");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(originalContext);
                factory.AllowCapture.TrySetResult(true);
            }

            Assert.IsTrue(factory.UsedSynchronizationContext);
            Exception firstFailure = null;
            Exception secondFailure = null;
            string[] firstIds = null;
            string[] secondIds = null;
            try
            {
                firstIds = await ReadIdsAsync(firstTask);
            }
            catch (Exception exception)
            {
                firstFailure = exception;
            }

            // Exercise reuse even if the first response failed validation: B must not have been consumed.
            try
            {
                secondIds = await ReadIdsAsync(ExecuteAsync(batch, useRequestOptions));
            }
            catch (Exception exception)
            {
                secondFailure = exception;
            }

            this.TestContext.WriteLine(
                $"Captured: {string.Join("; ", factory.Captures.Select(ids => $"[{string.Join(",", ids)}]"))}");
            this.TestContext.WriteLine($"First failure: {firstFailure}; next failure: {secondFailure}");
            Assert.IsNull(firstFailure, firstFailure?.Message);
            Assert.IsNull(secondFailure, secondFailure?.Message);
            CollectionAssert.AreEqual(new[] { "A" }, firstIds);
            CollectionAssert.AreEqual(new[] { "B" }, secondIds);
            Assert.AreEqual(2, factory.Captures.Count);
            CollectionAssert.AreEqual(firstIds, factory.Captures[0]);
            CollectionAssert.AreEqual(secondIds, factory.Captures[1]);
            CollectionAssert.AreEqual(
                new[]
                {
#if NET8_0_OR_GREATER
                    CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
#else
                    CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft,
#endif
                    CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Newtonsoft,
                },
                processors);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task ExecuteAsync_ReplacementFactoryFailureRetainsQueuedOperations(
            bool useRequestOptions,
            bool returnNull)
        {
            DeferredBatchFactory factory = new ();
            factory.AllowCapture.SetResult(true);
            int factoryCalls = 0;
            TransactionalBatch batch = CreateBatch(() =>
            {
                if (++factoryCalls == 2)
                {
                    return returnNull ? null : throw new InvalidOperationException("Factory failed.");
                }

                return factory.Create();
            });
            batch.ReadItem("A", CreateFirstItemOptions());

            InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => ExecuteAsync(batch, useRequestOptions));

            Assert.AreEqual(returnNull ? "Transactional batch factory returned null." : "Factory failed.", failure.Message);
            Assert.AreEqual(0, factory.ExecutionCount);
            batch.ReadItem("B");
            CollectionAssert.AreEqual(new[] { "A", "B" }, await ReadIdsAsync(ExecuteAsync(batch, useRequestOptions)));
            batch.ReadItem("C");
            CollectionAssert.AreEqual(new[] { "C" }, await ReadIdsAsync(ExecuteAsync(batch, useRequestOptions)));
            Assert.AreEqual(2, factory.ExecutionCount);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CreateTransactionalBatch_FactoryFailurePropagates(bool returnNull)
        {
            InvalidOperationException failure = Assert.ThrowsException<InvalidOperationException>(
                () => CreateBatch(() => returnNull ? null : throw new InvalidOperationException("Factory failed.")));

            Assert.AreEqual(returnNull ? "Transactional batch factory returned null." : "Factory failed.", failure.Message);
        }

        [DataTestMethod]
        [DataRow(false, "Cancel")]
        [DataRow(true, "Cancel")]
        [DataRow(false, "Throw")]
        [DataRow(true, "Throw")]
        [DataRow(false, "Service")]
        [DataRow(true, "Service")]
        public async Task ExecuteAsync_DelegatedFailureDoesNotLeakOperationsIntoReuse(bool useRequestOptions, string failureMode)
        {
            DeferredBatchFactory factory = new ();
            factory.AllowCapture.SetResult(true);
            TransactionalBatch batch = CreateBatch(factory.Create);
            batch.ReadItem("A", CreateFirstItemOptions());
            switch (failureMode)
            {
                case "Cancel":
                    try
                    {
                        using TransactionalBatchResponse unexpected = await ExecuteAsync(
                            batch, useRequestOptions, new CancellationToken(canceled: true));
                        Assert.Fail("Expected cancellation.");
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    break;
                case "Throw":
                    factory.ExecutionException = new InvalidOperationException("Execution failed before capture.");
                    InvalidOperationException failure = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                        () => ExecuteAsync(batch, useRequestOptions));
                    Assert.AreEqual("Execution failed before capture.", failure.Message);
                    break;
                case "Service":
                    factory.FailNextResponse = true;
                    using (TransactionalBatchResponse response = await ExecuteAsync(batch, useRequestOptions))
                    {
                        Assert.IsFalse(response.IsSuccessStatusCode);
                        Assert.AreEqual(1, response.Count);
                    }

                    break;
            }

            batch.ReadItem("B");
            CollectionAssert.AreEqual(new[] { "B" }, await ReadIdsAsync(ExecuteAsync(batch, useRequestOptions)));
            CollectionAssert.AreEqual(new[] { "B" }, factory.Captures.Last());
            Assert.AreEqual(2, factory.ExecutionCount);
        }

        private static TransactionalBatch CreateBatch(Func<TransactionalBatch> createBatch)
        {
            Mock<CosmosClient> client = new ();
            client.SetupGet(c => c.ClientOptions).Returns(new CosmosClientOptions
            {
                Serializer = Mock.Of<CosmosSerializer>(),
            });
            Mock<Database> database = new ();
            database.SetupGet(d => d.Client).Returns(client.Object);
            Mock<Container> container = new ();
            container.SetupGet(c => c.Database).Returns(database.Object);
            PartitionKey partitionKey = new ("partition");
            container.Setup(c => c.CreateTransactionalBatch(partitionKey)).Returns(createBatch);
            EncryptionContainer encryptionContainer = new (container.Object, Mock.Of<Encryptor>());
            return encryptionContainer.CreateTransactionalBatch(partitionKey);
        }

        private static TransactionalBatchItemRequestOptions CreateFirstItemOptions()
        {
            return new TransactionalBatchItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    {
                        JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey,
#if NET8_0_OR_GREATER
                        JsonProcessor.Stream
#else
                        JsonProcessor.Newtonsoft
#endif
                    },
                },
            };
        }

        private static Task<TransactionalBatchResponse> ExecuteAsync(
            TransactionalBatch batch,
            bool useRequestOptions,
            CancellationToken cancellationToken = default)
        {
            return useRequestOptions
                ? batch.ExecuteAsync(new TransactionalBatchRequestOptions(), cancellationToken)
                : batch.ExecuteAsync(cancellationToken);
        }

        private static async Task<string[]> ReadIdsAsync(Task<TransactionalBatchResponse> executeTask)
        {
            using TransactionalBatchResponse response = await executeTask;
            List<string> ids = new ();
            foreach (TransactionalBatchOperationResult result in response)
            {
                using StreamReader reader = new (result.ResourceStream);
                ids.Add((string)JObject.Parse(await reader.ReadToEndAsync())["id"]);
            }

            Assert.AreEqual(ids.Count, response.Count);
            return ids.ToArray();
        }

        private sealed class DeferredBatchFactory
        {
            public TaskCompletionSource<bool> AllowCapture { get; } = new (
                TaskCreationOptions.RunContinuationsAsynchronously);

            public List<string[]> Captures { get; } = new ();

            public bool UsedSynchronizationContext { get; private set; }

            public int ExecutionCount { get; private set; }

            public Exception ExecutionException { get; set; }

            public bool FailNextResponse { get; set; }

            public TransactionalBatch Create()
            {
                List<string> operations = new ();
                Mock<TransactionalBatch> inner = new ();
                inner.Setup(b => b.ReadItem(It.IsAny<string>(), It.IsAny<TransactionalBatchItemRequestOptions>()))
                    .Callback<string, TransactionalBatchItemRequestOptions>((id, _) => operations.Add(id))
                    .Returns(inner.Object);
                inner.Setup(b => b.ExecuteAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(ExecuteAsync);
                inner.Setup(b => b.ExecuteAsync(
                        It.IsAny<TransactionalBatchRequestOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<TransactionalBatchRequestOptions, CancellationToken>((_, token) => ExecuteAsync(token));
                return inner.Object;

                Task<TransactionalBatchResponse> ExecuteAsync(CancellationToken cancellationToken)
                {
                    this.ExecutionCount++;
                    if (this.ExecutionException != null)
                    {
                        Exception exception = this.ExecutionException;
                        this.ExecutionException = null;
                        throw exception;
                    }

                    // Cosmos 3.41.0-preview.0 defers BatchCore's capture through Task.Run
                    // in ClientContextCore.OperationHelperAsync when a context is installed.
                    if (SynchronizationContext.Current != null)
                    {
                        this.UsedSynchronizationContext = true;
                        return Task.Run(() => CaptureAsync(cancellationToken));
                    }

                    return CaptureAsync(cancellationToken);
                }

                async Task<TransactionalBatchResponse> CaptureAsync(CancellationToken cancellationToken)
                {
                    await this.AllowCapture.Task.ConfigureAwait(false);
                    List<string> captured = operations;
                    operations = new List<string>();
                    this.Captures.Add(captured.ToArray());
                    cancellationToken.ThrowIfCancellationRequested();
                    bool success = !this.FailNextResponse;
                    this.FailNextResponse = false;
                    List<TransactionalBatchOperationResult> results = captured.Select(id =>
                    {
                        Mock<TransactionalBatchOperationResult> result = new ();
                        result.SetupGet(r => r.ResourceStream)
                            .Returns(new MemoryStream(Encoding.UTF8.GetBytes($"{{\"id\":\"{id}\"}}")));
                        result.SetupGet(r => r.StatusCode).Returns(success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                        return result.Object;
                    }).ToList();
                    Mock<TransactionalBatchResponse> response = new ();
                    response.SetupGet(r => r.Count).Returns(results.Count);
                    response.SetupGet(r => r.IsSuccessStatusCode).Returns(success);
                    response.SetupGet(r => r.StatusCode).Returns(success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
                    response.Setup(r => r.GetEnumerator()).Returns(() => results.GetEnumerator());
                    response.Protected().Setup("Dispose", true).Callback(() =>
                    {
                        foreach (TransactionalBatchOperationResult result in results)
                        {
                            result.ResourceStream.Dispose();
                        }
                    });
                    return response.Object;
                }
            }
        }
    }
}
