//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchSchemaTests
    {
        [TestMethod]
        [Owner("abpai")]
        public async Task BatchRequestSerializationAsync()
        {
            const string partitionKey1 = "pk1";
            using CosmosClient cosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            ContainerInternal containerCore = (ContainerInlineCore)cosmosClient.GetDatabase("db").GetContainer("cont");

            ItemBatchOperation[] operations = new ItemBatchOperation[]
            {
                new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: 0,
                    containerCore:containerCore)
                {
                    ResourceBody = new byte[] { 0x41, 0x42 }
                },
                new ItemBatchOperation(
                    id: "id2",
                    operationType: OperationType.Replace,
                    operationIndex: 1,
                    containerCore:containerCore,
                    requestOptions: new TransactionalBatchItemRequestOptions()
                    {
                        IfMatchEtag = "theCondition"
                    })
            };

            ServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                new Cosmos.PartitionKey(partitionKey1),
                new ArraySegment<ItemBatchOperation>(operations),
                serializerCore: MockCosmosUtil.Serializer,
                trace: NoOpTrace.Singleton,
                cancellationToken: CancellationToken.None);

            Assert.AreEqual(2, batchRequest.Operations.Count);

            using (MemoryStream payload = batchRequest.TransferBodyStream())
            {
                Assert.IsNotNull(payload);

                List<ItemBatchOperation> readOperations = await new BatchRequestPayloadReader().ReadPayloadAsync(payload);
                Assert.AreEqual(2, readOperations.Count);
                ItemBatchOperationEqualityComparer comparer = new ItemBatchOperationEqualityComparer();
                Assert.IsTrue(comparer.Equals(operations[0], readOperations[0]));
                Assert.IsTrue(comparer.Equals(operations[1], readOperations[1]));
            }
        }

        [TestMethod]
        [Owner("abpai")]
        public async Task BatchResponseDeserializationAsync()
        {
            using CosmosClient cosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            ContainerInternal containerCore = (ContainerInlineCore)cosmosClient.GetDatabase("db").GetContainer("cont");
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>
            {
                new TransactionalBatchOperationResult(HttpStatusCode.Conflict),
                new TransactionalBatchOperationResult(HttpStatusCode.OK)
                {
                    ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                    RequestCharge = 2.5,
                    ETag = "1234",
                    RetryAfter = TimeSpan.FromMilliseconds(360)
                }
            };

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            CosmosSerializer serializer = new CosmosJsonDotNetSerializer();
            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: Cosmos.PartitionKey.None,
                operations: new ArraySegment<ItemBatchOperation>(
                    new ItemBatchOperation[]
                    {
                        new ItemBatchOperation(OperationType.Read, operationIndex: 0, id: "someId", containerCore: containerCore),
                        new ItemBatchOperation(OperationType.Read, operationIndex: 0, id: "someId", containerCore: containerCore)
                    }),
                serializerCore: MockCosmosUtil.Serializer,
                trace: NoOpTrace.Singleton,
                cancellationToken: CancellationToken.None);
            TransactionalBatchResponse batchResponse = await TransactionalBatchResponse.FromResponseMessageAsync(
                new ResponseMessage((HttpStatusCode)StatusCodes.MultiStatus) { Content = responseContent },
                batchRequest,
                MockCosmosUtil.Serializer,
                true,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNotNull(batchRequest);
            Assert.AreEqual(HttpStatusCode.Conflict, batchResponse.StatusCode);
            Assert.AreEqual(2, batchResponse.Count);

            CosmosBatchOperationResultEqualityComparer comparer = new CosmosBatchOperationResultEqualityComparer();
            Assert.IsTrue(comparer.Equals(results[0], batchResponse[0]));
            Assert.IsTrue(comparer.Equals(results[1], batchResponse[1]));
        }

        private class ItemBatchOperationEqualityComparer : IEqualityComparer<ItemBatchOperation>
        {
            public bool Equals(ItemBatchOperation x, ItemBatchOperation y)
            {
                return x.Id == y.Id
                    && x.OperationType == y.OperationType
                    && x.OperationIndex == y.OperationIndex
                    && this.Equals(x.RequestOptions, y.RequestOptions)
                    && x.ResourceBody.Span.SequenceEqual(y.ResourceBody.Span);
            }

            private bool Equals(TransactionalBatchItemRequestOptions x, TransactionalBatchItemRequestOptions y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                else if (x != null && y != null)
                {
                    RequestMessage xMessage = new RequestMessage();
                    RequestMessage yMessage = new RequestMessage();
                    x.PopulateRequestOptions(xMessage);
                    y.PopulateRequestOptions(yMessage);

                    foreach (string headerName in xMessage.Headers)
                    {
                        if (xMessage.Headers[headerName] != yMessage.Headers[headerName])
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return false;
            }

            public int GetHashCode(ItemBatchOperation obj)
            {
                int hashCode = 1660235553;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.Id);
                hashCode = (hashCode * -1521134295) + obj.OperationType.GetHashCode();
                hashCode = (hashCode * -1521134295) + EqualityComparer<TransactionalBatchItemRequestOptions>.Default.GetHashCode(obj.RequestOptions);
                hashCode = (hashCode * -1521134295) + obj.OperationIndex.GetHashCode();
                hashCode = (hashCode * -1521134295) + EqualityComparer<Memory<byte>>.Default.GetHashCode(obj.ResourceBody);
                return hashCode;
            }
        }

        private class CosmosBatchOperationResultEqualityComparer : IEqualityComparer<TransactionalBatchOperationResult>
        {
            public bool Equals(TransactionalBatchOperationResult x, TransactionalBatchOperationResult y)
            {
                return x.StatusCode == y.StatusCode
                    && x.SubStatusCode == y.SubStatusCode
                    && x.ETag == y.ETag
                    && x.RequestCharge == y.RequestCharge
                    && x.RetryAfter == y.RetryAfter
                    && this.Equals(x.ResourceStream, y.ResourceStream);
            }

            private bool Equals(Stream x, Stream y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                else if (x != null && y != null)
                {
                    if (x.Length != y.Length)
                    {
                        return false;
                    }

                    return ((MemoryStream)x).GetBuffer().SequenceEqual(((MemoryStream)y).GetBuffer());
                }

                return false;
            }

            public int GetHashCode(TransactionalBatchOperationResult obj)
            {
                int hashCode = 1176625765;
                hashCode = (hashCode * -1521134295) + obj.StatusCode.GetHashCode();
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.ETag);
                hashCode = (hashCode * -1521134295) + EqualityComparer<double>.Default.GetHashCode(obj.RequestCharge);
                hashCode = (hashCode * -1521134295) + EqualityComparer<TimeSpan>.Default.GetHashCode(obj.RetryAfter);
                hashCode = (hashCode * -1521134295) + EqualityComparer<SubStatusCodes>.Default.GetHashCode(obj.SubStatusCode);
                return hashCode;
            }
        }
    }
}
