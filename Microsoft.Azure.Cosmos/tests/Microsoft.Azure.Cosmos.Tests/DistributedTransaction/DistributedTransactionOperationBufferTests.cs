// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;

    [TestClass]
    public class DistributedTransactionOperationBufferTests
    {
        private const string StartedMessage = "This transaction has already started.";
        private const string EmptyMessage = "This transaction has no operations.";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        private static readonly PartitionKey TestPartitionKey = new PartitionKey("partition");

        [DataTestMethod]
        [DataRow(null, EmptyMessage, "executionStartedMessage")]
        [DataRow(StartedMessage, null, "emptyTransactionMessage")]
        public void Constructor_NullErrorContext_ThrowsArgumentNullException(string started, string empty, string parameter)
        {
            ArgumentNullException error = Assert.ThrowsException<ArgumentNullException>(
                () => new DistributedTransactionOperationBuffer(started, empty));
            Assert.AreEqual(parameter, error.ParamName);
        }

        [DataTestMethod]
        [DataRow(DistributedReadTransactionCore.CommitAlreadyCalledMessage, "Cannot execute an empty read transaction.")]
        [DataRow(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, "Cannot execute an empty write transaction.")]
        public void FreezeForExecution_Empty_ConsumesBufferAndPreservesErrorContext(string started, string empty)
        {
            DistributedTransactionOperationBuffer buffer = new DistributedTransactionOperationBuffer(started, empty);
            buffer.ThrowIfExecutionStarted();
            Assert.AreEqual(empty, Assert.ThrowsException<InvalidOperationException>(() => buffer.FreezeForExecution()).Message);
            Assert.AreEqual(started, Assert.ThrowsException<InvalidOperationException>(() => buffer.ThrowIfExecutionStarted()).Message);
            Assert.AreEqual(started, Assert.ThrowsException<InvalidOperationException>(() => buffer.FreezeForExecution()).Message);
            Assert.AreEqual(started, Assert.ThrowsException<InvalidOperationException>(() => Add(buffer, false, "late")).Message);
            Assert.AreEqual(started, Assert.ThrowsException<InvalidOperationException>(() => Add(buffer, true, "late")).Message);
        }

        [TestMethod]
        public void AddOperation_MixedOverloads_PreserveMetadataResourcesAndIndices()
        {
            DistributedTransactionOperationBuffer buffer = NewBuffer();
            DistributedTransactionRequestOptions options = new DistributedTransactionRequestOptions
            {
                IfMatchEtag = "match",
                IfNoneMatchEtag = "nonmatch",
                SessionToken = "0:-1#123",
            };
            object resource = new object();
            using MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3 });
            stream.Position = 1;
            buffer.AddOperation(OperationType.Read, "readDb", "readContainer", TestPartitionKey, "read");
            buffer.AddOperation(OperationType.Delete, "deleteDb", "deleteContainer", TestPartitionKey, "delete", options);
            buffer.AddOperation(OperationType.Create, "streamDb", "streamContainer", TestPartitionKey, "stream", options, stream);
            buffer.AddOperation(OperationType.Upsert, "typedDb", "typedContainer", TestPartitionKey, "typed", resource, options);
            buffer.ThrowIfExecutionStarted();

            IReadOnlyList<DistributedTransactionOperation> operations = buffer.FreezeForExecution();
            Assert.AreEqual(4, operations.Count);
            string[] names = { "read", "delete", "stream", "typed" };
            OperationType[] types = { OperationType.Read, OperationType.Delete, OperationType.Create, OperationType.Upsert };
            for (int i = 0; i < operations.Count; i++)
            {
                DistributedTransactionOperation operation = operations[i];
                Assert.AreEqual(i, operation.OperationIndex);
                Assert.AreEqual(names[i], operation.Id);
                Assert.AreEqual(names[i] + "Db", operation.Database);
                Assert.AreEqual(names[i] + "Container", operation.Container);
                Assert.AreEqual(types[i], operation.OperationType);
                Assert.AreEqual(TestPartitionKey, operation.PartitionKey);
                Assert.IsTrue(operation.ResourceBody.IsEmpty, "Appending must not materialize caller payloads.");
                if (i == 0)
                {
                    Assert.IsNull(operation.RequestOptions);
                    Assert.IsNull(operation.SessionToken);
                }
                else
                {
                    Assert.AreSame(options, operation.RequestOptions);
                    Assert.AreEqual(options.SessionToken, operation.SessionToken);
                    Assert.AreEqual(options.IfMatchEtag, operation.IfMatch);
                    Assert.AreEqual(options.IfNoneMatchEtag, operation.IfNoneMatch);
                }
            }

            Assert.IsNull(operations[0].ResourceStream);
            Assert.IsNull(operations[1].ResourceStream);
            Assert.AreSame(stream, operations[2].ResourceStream);
            Assert.AreEqual(1L, stream.Position);
            Assert.IsTrue(stream.CanRead);
            Assert.IsInstanceOfType(operations[3], typeof(DistributedTransactionOperation<object>));
            Assert.AreSame(resource, ((DistributedTransactionOperation<object>)operations[3]).Resource);
            Assert.IsNull(operations[3].ResourceStream);
        }

        [TestMethod]
        public void FreezeForExecution_SyncRootDoesNotExposeMutableOperations()
        {
            DistributedTransactionOperationBuffer buffer = NewBuffer();
            Add(buffer, false, "original");
            IReadOnlyList<DistributedTransactionOperation> operations = buffer.FreezeForExecution();
            DistributedTransactionOperation original = operations[0];
            ICollection collection = (ICollection)operations;
            object syncRoot = collection.SyncRoot;

            if (syncRoot is IList<DistributedTransactionOperation> generic)
            {
                Assert.ThrowsException<NotSupportedException>(() => generic.Clear());
            }

            if (syncRoot is IList untyped)
            {
                Assert.ThrowsException<NotSupportedException>(() => untyped.RemoveAt(0));
            }

            Assert.IsFalse(syncRoot is List<DistributedTransactionOperation>);
            Assert.AreSame(syncRoot, collection.SyncRoot);
            Assert.IsFalse(collection.IsSynchronized);
            Assert.AreEqual(1, collection.Count);
            Assert.AreSame(original, operations[0]);

            object[] copied = new object[1];
            collection.CopyTo(copied, 0);
            Assert.AreSame(original, copied[0]);
        }

        [TestMethod]
        public void FreezeForExecution_ReturnsStableReadOnlyView()
        {
            DistributedTransactionOperationBuffer buffer = NewBuffer();
            Add(buffer, false, "original");
            IReadOnlyList<DistributedTransactionOperation> operations = buffer.FreezeForExecution();
            Assert.IsFalse(operations is List<DistributedTransactionOperation>);
            DistributedTransactionOperation original = operations[0];
            IList<DistributedTransactionOperation> generic = (IList<DistributedTransactionOperation>)operations;
            Assert.IsTrue(generic.IsReadOnly);
            Assert.ThrowsException<NotSupportedException>(() => generic.Add(original));
            Assert.ThrowsException<NotSupportedException>(() => generic.Insert(0, original));
            Assert.ThrowsException<NotSupportedException>(() => generic[0] = original);
            Assert.ThrowsException<NotSupportedException>(() => generic.Remove(original));
            Assert.ThrowsException<NotSupportedException>(() => generic.RemoveAt(0));
            Assert.ThrowsException<NotSupportedException>(() => generic.Clear());
            IList untyped = (IList)operations;
            Assert.IsTrue(untyped.IsReadOnly);
            Assert.ThrowsException<NotSupportedException>(() => untyped.Add(original));
            Assert.ThrowsException<NotSupportedException>(() => untyped.Insert(0, original));
            Assert.ThrowsException<NotSupportedException>(() => untyped[0] = original);
            Assert.ThrowsException<NotSupportedException>(() => untyped.Remove(original));
            Assert.ThrowsException<NotSupportedException>(() => untyped.RemoveAt(0));
            Assert.ThrowsException<NotSupportedException>(() => untyped.Clear());
            Assert.AreEqual(StartedMessage, Assert.ThrowsException<InvalidOperationException>(() => buffer.FreezeForExecution()).Message);
            Assert.AreEqual(StartedMessage, Assert.ThrowsException<InvalidOperationException>(() => buffer.ThrowIfExecutionStarted()).Message);
            Assert.AreEqual(StartedMessage, Assert.ThrowsException<InvalidOperationException>(() => Add(buffer, false, "late")).Message);
            Assert.AreEqual(StartedMessage, Assert.ThrowsException<InvalidOperationException>(() => Add(buffer, true, "late")).Message);
            using MemoryStream payload = new MemoryStream(new byte[] { 1, 2, 3 });
            Assert.AreEqual(StartedMessage, Assert.ThrowsException<InvalidOperationException>(() =>
                buffer.AddOperation(OperationType.Create, "db", "container", TestPartitionKey, "late", streamPayload: payload)).Message);
            Assert.AreEqual(0L, payload.Position);
            Assert.IsTrue(payload.CanRead);
            Assert.AreEqual(1, operations.Count);
            Assert.AreSame(original, operations[0]);
            Assert.AreEqual(0, operations[0].OperationIndex);

            // Membership is frozen, but metadata resolution still updates the operation itself.
            original.CollectionResourceId = "resolved";
            Assert.AreEqual("resolved", operations[0].CollectionResourceId);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task AddOperation_ConcurrentAppends_AssignsUniqueContiguousIndices(bool typed)
        {
            DistributedTransactionOperationBuffer buffer = NewBuffer();
            TaskCompletionSource<bool> start = NewGate();
            TaskCompletionSource<bool> ready = NewGate();
            int producersReady = 0;
            const int ProducerCount = 4;
            const int OperationsPerProducer = 25;
            Task[] producers = new Task[ProducerCount];
            for (int i = 0; i < producers.Length; i++)
            {
                int producer = i;
                producers[i] = Task.Run(async () =>
                {
                    if (Interlocked.Increment(ref producersReady) == ProducerCount)
                    {
                        ready.TrySetResult(true);
                    }

                    await start.Task.WaitAsync(Timeout);
                    for (int j = 0; j < OperationsPerProducer; j++)
                    {
                        Add(buffer, typed, $"{producer}:{j}");
                    }
                });
            }

            try
            {
                await ready.Task.WaitAsync(Timeout);
            }
            finally
            {
                start.TrySetResult(true);
                await Task.WhenAll(producers).WaitAsync(Timeout);
            }

            IReadOnlyList<DistributedTransactionOperation> operations = buffer.FreezeForExecution();
            Assert.AreEqual(ProducerCount * OperationsPerProducer, operations.Count);
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < operations.Count; i++)
            {
                Assert.AreEqual(i, operations[i].OperationIndex);
                Assert.IsTrue(ids.Add(operations[i].Id), "Every accepted append must appear exactly once.");
                if (typed)
                {
                    Assert.AreEqual(operations[i].Id, ((DistributedTransactionOperation<string>)operations[i]).Resource);
                }
            }

            for (int i = 0; i < ProducerCount; i++)
            {
                for (int j = 0; j < OperationsPerProducer; j++)
                {
                    Assert.IsTrue(ids.Contains($"{i}:{j}"));
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task AddOperation_RacingFreeze_IsIncludedOrRejected(bool typed)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                DistributedTransactionOperationBuffer buffer = NewBuffer();
                Add(buffer, typed, "original");
                TaskCompletionSource<bool> start = NewGate();
                TaskCompletionSource<bool> ready = NewGate();
                int racersReady = 0;
                async Task WaitForStartAsync()
                {
                    if (Interlocked.Increment(ref racersReady) == 2)
                    {
                        ready.TrySetResult(true);
                    }

                    await start.Task.WaitAsync(Timeout);
                }

                Task<bool> append = Task.Run(async () =>
                {
                    await WaitForStartAsync();
                    try
                    {
                        Add(buffer, typed, "racing");
                        return true;
                    }
                    catch (InvalidOperationException error)
                    {
                        Assert.AreEqual(StartedMessage, error.Message);
                        return false;
                    }
                });
                Task<(IReadOnlyList<DistributedTransactionOperation> Operations, DistributedTransactionOperation[] Snapshot)> freeze = Task.Run(async () =>
                {
                    await WaitForStartAsync();
                    IReadOnlyList<DistributedTransactionOperation> frozen = buffer.FreezeForExecution();
                    return (Operations: frozen, Snapshot: frozen.ToArray());
                });
                try
                {
                    await ready.Task.WaitAsync(Timeout);
                }
                finally
                {
                    start.TrySetResult(true);
                    await Task.WhenAll(append, freeze).WaitAsync(Timeout);
                }

                (IReadOnlyList<DistributedTransactionOperation> operations, DistributedTransactionOperation[] snapshot) = await freeze;
                Assert.AreEqual(await append ? 2 : 1, snapshot.Length);
                Assert.AreEqual(snapshot.Length, operations.Count, "The execution view changed after freeze returned.");
                Assert.AreEqual("original", operations[0].Id);
                for (int i = 0; i < snapshot.Length; i++)
                {
                    Assert.AreSame(snapshot[i], operations[i]);
                    Assert.AreEqual(i, operations[i].OperationIndex);
                    if (i == 1)
                    {
                        Assert.AreEqual("racing", operations[i].Id);
                    }
                }

                Assert.AreEqual(StartedMessage, Assert.ThrowsException<InvalidOperationException>(() => Add(buffer, typed, "late")).Message);
                Assert.AreEqual(snapshot.Length, operations.Count);
            }
        }

        private static DistributedTransactionOperationBuffer NewBuffer() => new DistributedTransactionOperationBuffer(StartedMessage, EmptyMessage);

        private static TaskCompletionSource<bool> NewGate() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static void Add(DistributedTransactionOperationBuffer buffer, bool typed, string id)
        {
            if (typed)
            {
                buffer.AddOperation(OperationType.Create, "db", "container", TestPartitionKey, id, resource: id);
            }
            else
            {
                buffer.AddOperation(OperationType.Read, "db", "container", TestPartitionKey, id);
            }
        }
    }
}
