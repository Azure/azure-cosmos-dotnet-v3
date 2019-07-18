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
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchSchemaTests
    {
        [TestMethod]
        [Owner("abpai")]
        public async Task BatchRequestSerializationAsync()
        {
            const int maxBodySize = 5 * 1024;
            const int maxOperationCount = 10;
            const string partitionKey1 = "pk1";

            ItemBatchOperation[] operations = new ItemBatchOperation[]
            {
                new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: 0)
                {
                    ResourceBody = new byte[] { 0x41, 0x42 }
                },
                new ItemBatchOperation(
                    id: "id2",
                    operationType: OperationType.Replace,
                    operationIndex: 1,
                    requestOptions: new BatchItemRequestOptions()
                    {
                        IfMatchEtag = "theCondition"
                    })
            };

            ServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                new Cosmos.PartitionKey(partitionKey1),
                new ArraySegment<ItemBatchOperation>(operations),
                maxBodySize,
                maxOperationCount,
                serializer: new CosmosJsonDotNetSerializer(),
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
        public async Task BatchRequestSerializationFillAsync()
        {
            const int maxBodySize = 5 * 1024;
            const int maxOperationCount = 10;
            const int operationBodySize = 2 * 1024;
            const string partitionKey1 = "pk1";
            const string id = "random";
            ItemBatchOperation[] operations = new ItemBatchOperation[]
            {
                new ItemBatchOperation(
                    operationType: OperationType.Replace,
                    id: id,
                    operationIndex: 0)
                {
                    ResourceBody = Encoding.UTF8.GetBytes(new string('w', operationBodySize))
                },
                new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: 1)
                {
                    ResourceBody = Encoding.UTF8.GetBytes(new string('x', operationBodySize))
                },
                new ItemBatchOperation(
                    operationType: OperationType.Upsert,
                    operationIndex: 2)
                {
                    ResourceBody = Encoding.UTF8.GetBytes(new string('y', operationBodySize))
                },
                new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: 3)
                {
                    ResourceBody = Encoding.UTF8.GetBytes(new string('z', operationBodySize))
                }
            };
            ServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                new Cosmos.PartitionKey(partitionKey1), 
                new ArraySegment<ItemBatchOperation>(operations), 
                maxBodySize,
                maxOperationCount,
                serializer: new CosmosJsonDotNetSerializer(),
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
           List<BatchOperationResult> results = new List<BatchOperationResult>();

            results.Add(new BatchOperationResult(HttpStatusCode.Conflict));

            results.Add(
                new BatchOperationResult(HttpStatusCode.OK)
                {
                    ResourceStream = new MemoryStream(new byte[] { 0x41, 0x42 }, index: 0, count: 2, writable: false, publiclyVisible: true),
                    ETag = "1234"
                });

            results.Add(
                new BatchOperationResult((HttpStatusCode)StatusCodes.TooManyRequests)
                {
                    RetryAfter = TimeSpan.FromMilliseconds(360)
                });

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            CosmosSerializer serializer = new CosmosJsonDotNetSerializer();
            SinglePartitionKeyServerBatchRequest batchResponse = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: Cosmos.PartitionKey.None,
                operations: new ArraySegment<ItemBatchOperation>(
                    new ItemBatchOperation[]
                    {
                        new ItemBatchOperation(OperationType.Read, operationIndex: 0, id: "someId")
                    }),
                maxBodyLength: 100,
                maxOperationCount: 1,
                serializer: serializer,
                cancellationToken: CancellationToken.None);
            BatchResponse batchresponse = await BatchResponse.PopulateFromContentAsync(
                new ResponseMessage(HttpStatusCode.OK) { Content = responseContent },
                batchResponse,
                serializer);

            Assert.IsNotNull(batchresponse);
            Assert.IsTrue(batchresponse.IsSuccessStatusCode);
            Assert.AreEqual(3, batchresponse.Count);

            CosmosBatchOperationResultEqualityComparer comparer = new CosmosBatchOperationResultEqualityComparer();
            Assert.IsTrue(comparer.Equals(results[0], batchresponse[0]));
            Assert.IsTrue(comparer.Equals(results[1], batchresponse[1]));
            Assert.IsTrue(comparer.Equals(results[2], batchresponse[2]));
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

            private bool Equals(BatchItemRequestOptions x, BatchItemRequestOptions y)
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
                hashCode = (hashCode * -1521134295) + EqualityComparer<BatchItemRequestOptions>.Default.GetHashCode(obj.RequestOptions);
                hashCode = (hashCode * -1521134295) + obj.OperationIndex.GetHashCode();
                hashCode = (hashCode * -1521134295) + EqualityComparer<Memory<byte>>.Default.GetHashCode(obj.ResourceBody);
                return hashCode;
            }
        }

        private class CosmosBatchOperationResultEqualityComparer : IEqualityComparer<BatchOperationResult>
        {
            public bool Equals(BatchOperationResult x, BatchOperationResult y)
            {
                return x.StatusCode == y.StatusCode
                    && x.SubStatusCode == y.SubStatusCode
                    && x.ETag == y.ETag
                    && x.RetryAfter == y.RetryAfter
                    && this.Equals(x.ResourceStream, y.ResourceStream);
            }

            private bool Equals(MemoryStream x, MemoryStream y)
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

                    return x.GetBuffer().SequenceEqual(y.GetBuffer());
                }

                return false;
            }

            public int GetHashCode(BatchOperationResult obj)
            {
                int hashCode = 1176625765;
                hashCode = (hashCode * -1521134295) + obj.StatusCode.GetHashCode();
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(obj.ETag);
                hashCode = (hashCode * -1521134295) + EqualityComparer<TimeSpan>.Default.GetHashCode(obj.RetryAfter);
                hashCode = (hashCode * -1521134295) + EqualityComparer<SubStatusCodes>.Default.GetHashCode(obj.SubStatusCode);
                return hashCode;
            }
        }
    }
}
