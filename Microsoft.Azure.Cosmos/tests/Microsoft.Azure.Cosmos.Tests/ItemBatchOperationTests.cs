namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Utf8;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for <see cref="ItemBatchOperation"/>.
    /// </summary>
    [TestClass]
    public class ItemBatchOperationTests
    {
        [TestMethod]
        public void TestWriteOperationWithBinaryIdReadOnlyMemory()
        {
            ISpanResizer<byte> resizer = new MemorySpanResizer<byte>(100);
            RowBuffer row = new RowBuffer(capacity: 100, resizer: resizer);
            row.InitLayout(HybridRowVersion.V1, BatchSchemaProvider.BatchOperationLayout, BatchSchemaProvider.BatchLayoutResolver);

            byte[] testBinaryId = new byte[] { 1, 2, 3, 4, };
            ItemRequestOptions requestOptions = new();
            requestOptions.Properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.BinaryId, new ReadOnlyMemory<byte>(testBinaryId) },
            };
            TransactionalBatchItemRequestOptions transactionalBatchItemRequestOptions =
                TransactionalBatchItemRequestOptions.FromItemRequestOptions(requestOptions);
            ItemBatchOperation operation = new ItemBatchOperation(
                operationType: OperationType.Patch,
                operationIndex: 0,
                partitionKey: Cosmos.PartitionKey.Null,
                requestOptions: transactionalBatchItemRequestOptions);

            int length = operation.GetApproximateSerializedLength();
            Assert.AreEqual(testBinaryId.Length, length);

            Result r = RowWriter.WriteBuffer(ref row, operation, ItemBatchOperation.WriteOperation);
            if (r != Result.Success)
            {
                Assert.Fail(r.ToString());
            }

            bool foundBinaryId = false;
            RowReader reader = new RowReader(ref row);
            while(reader.Read())
            {
                if (reader.PathSpan == Utf8String.TranscodeUtf16("binaryId"))
                {
                    foundBinaryId = true;
                    reader.ReadBinary(out byte[] binaryId);
                    CollectionAssert.AreEqual(testBinaryId, binaryId);
                }
            }

            Assert.IsTrue(foundBinaryId);
        }
    }
}
