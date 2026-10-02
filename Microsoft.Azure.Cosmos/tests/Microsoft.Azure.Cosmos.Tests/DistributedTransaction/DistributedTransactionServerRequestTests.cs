// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.DistributedTransaction
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

    [TestClass]
    public class DistributedTransactionServerRequestTests
    {
        [TestMethod]
        [Description("Verifies that CreateBodyStream returns a new independent MemoryStream on each call, enabling safe retry — disposing one stream must not affect siblings, and all streams must contain identical serialized bytes.")]
        public async Task CreateBodyStream_CalledMultipleTimes_ReturnsIndependentStreams()
        {
            DistributedTransactionServerRequest request = await DistributedTransactionServerRequest.CreateAsync(
                CreateTestOperations(),
                MockCosmosUtil.Serializer,
                CancellationToken.None);

            using (MemoryStream stream1 = request.CreateBodyStream())
            using (MemoryStream stream2 = request.CreateBodyStream())
            {
                Assert.AreNotSame(stream1, stream2, "Each call must return a new stream instance.");
                Assert.AreEqual(0, stream1.Position, "stream1 must be positioned at offset 0.");
                Assert.AreEqual(0, stream2.Position, "stream2 must be positioned at offset 0.");
                CollectionAssert.AreEqual(
                    stream1.ToArray(),
                    stream2.ToArray(),
                    "Both streams must contain identical serialized bytes (also implies non-empty + equal length).");
            }

            // Obtain a third stream after the first two have been disposed.
            using (MemoryStream stream3 = request.CreateBodyStream())
            {
                Assert.IsTrue(stream3.CanRead, "A stream obtained after disposing siblings must still be readable.");
                Assert.AreEqual(0, stream3.Position, "stream3 must be positioned at offset 0.");
            }
        }

        [DataTestMethod]
        [DataRow("0:-1#123", "from c where c.value = 1")]
        [DataRow(null, null)]
        [DataRow("", "")]
        [DataRow("  ", "  ")]
        public async Task CreateAsync_OptionsChangedDuringMaterialization_UsesCapturedValues(
            string sessionToken,
            string predicate)
        {
            DistributedTransactionPatchItemRequestOptions options = new DistributedTransactionPatchItemRequestOptions
            {
                IfMatchEtag = "match",
                IfNoneMatchEtag = "nonmatch",
                SessionToken = sessionToken,
                FilterPredicate = predicate
            };
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using MemoryStream payload = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":\"stream\"}"));
            DistributedTransactionOperation[] operations =
            {
                new DistributedTransactionOperation<PatchSpec>(OperationType.Patch, 0, "db", "container", new PartitionKey("pk"), "patch-before",
                    new PatchSpec(new[] { PatchOperation.Set("/value", 1) }, new PatchItemRequestOptions()), options),
                new GatedStreamOperation(options, payload, entered, release),
                new DistributedTransactionOperation<PatchSpec>(OperationType.Patch, 2, "db", "container", new PartitionKey("pk"), "patch-after",
                    new PatchSpec(new[] { PatchOperation.Set("/value", 1) }, new PatchItemRequestOptions()), options),
                new DistributedTransactionOperation(OperationType.Read, 3, "db", "container", new PartitionKey("pk"), "read", options)
            };

            Task<DistributedTransactionServerRequest> creation = DistributedTransactionServerRequest.CreateAsync(
                operations, MockCosmosUtil.Serializer, CancellationToken.None);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                options.IfMatchEtag = "changed-match";
                options.IfNoneMatchEtag = "changed-nonmatch";
                options.SessionToken = "0:-1#456";
                options.FilterPredicate = "from c where c.value = 2";
            }
            finally
            {
                release.TrySetResult(true);
                await creation.WaitAsync(TimeSpan.FromSeconds(30));
            }

            DistributedTransactionServerRequest request = await creation;
            using MemoryStream stream = request.CreateBodyStream();
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement serializedOperations = document.RootElement.GetProperty("operations");
            Assert.AreEqual(operations.Length, serializedOperations.GetArrayLength());
            for (int i = 0; i < operations.Length; i++)
            {
                Assert.AreEqual(i, serializedOperations[i].GetProperty("index").GetInt32());
                Assert.AreEqual("match", serializedOperations[i].GetProperty("ifMatch").GetString());
                Assert.AreEqual("nonmatch", serializedOperations[i].GetProperty("ifNoneMatch").GetString());
                Assert.AreNotSame(options, operations[i].RequestOptions);
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    Assert.IsFalse(serializedOperations[i].TryGetProperty("sessionToken", out _));
                }
                else
                {
                    Assert.AreEqual(sessionToken, serializedOperations[i].GetProperty("sessionToken").GetString());
                }

                if (i == 0 || i == 2)
                {
                    JsonElement body = serializedOperations[i].GetProperty("resourceBody");
                    if (string.IsNullOrWhiteSpace(predicate))
                    {
                        Assert.IsFalse(body.TryGetProperty("condition", out _));
                    }
                    else
                    {
                        Assert.AreEqual(predicate, body.GetProperty("condition").GetString());
                    }
                }
            }
        }

        private sealed class GatedStreamOperation : DistributedTransactionOperation
        {
            private readonly TaskCompletionSource<bool> entered;
            private readonly TaskCompletionSource<bool> release;

            public GatedStreamOperation(
                DistributedTransactionRequestOptions options,
                Stream payload,
                TaskCompletionSource<bool> entered,
                TaskCompletionSource<bool> release)
                : base(OperationType.Create, 1, "db", "container", new PartitionKey("pk"), "stream", options)
            {
                this.ResourceStream = payload;
                this.entered = entered;
                this.release = release;
            }

            internal override async Task MaterializeResourceAsync(CosmosSerializerCore serializerCore, CancellationToken cancellationToken)
            {
                this.entered.TrySetResult(true);
                await this.release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                await base.MaterializeResourceAsync(serializerCore, cancellationToken);
            }
        }

        private static IReadOnlyList<DistributedTransactionOperation> CreateTestOperations()
        {
            return new List<DistributedTransactionOperation>
            {
                new DistributedTransactionOperation(
                    OperationType.Create,
                    operationIndex: 0,
                    database: "testDb",
                    container: "testContainer",
                    partitionKey: new PartitionKey("pk0")),
                new DistributedTransactionOperation(
                    OperationType.Upsert,
                    operationIndex: 1,
                    database: "testDb",
                    container: "testContainer",
                    partitionKey: new PartitionKey("pk1")),
            };
        }
    }
}
