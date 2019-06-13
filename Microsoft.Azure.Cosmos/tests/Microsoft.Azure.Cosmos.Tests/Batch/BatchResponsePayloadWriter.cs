//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal class BatchResponsePayloadWriter
    {
        private List<CosmosBatchOperationResult> results;

        public BatchResponsePayloadWriter(List<CosmosBatchOperationResult> results)
        {
            this.results = results;
        }

        internal async Task<MemoryStream> GeneratePayloadAsync()
        {
            MemoryStream responseStream = new MemoryStream();
            Assert.AreEqual(Result.Success, await responseStream.WriteRecordIOAsync(default(Segment), this.WriteOperationResult));
            responseStream.Position = 0;
            return responseStream;
        }

        private Result WriteOperationResult(long index, out ReadOnlyMemory<byte> buffer)
        {
            if (index >= this.results.Count)
            {
                buffer = ReadOnlyMemory<byte>.Empty;
                return Result.Success;
            }

            RowBuffer row = new RowBuffer(2 * 1024);
            row.InitLayout(HybridRowVersion.V1, BatchSchemaProvider.BatchResultLayout, BatchSchemaProvider.BatchLayoutResolver);
            Result r = RowWriter.WriteBuffer(ref row, this.results[(int)index], BatchResponsePayloadWriter.WriteResult);
            if (r != Result.Success)
            {
                buffer = null;
                return r;
            }

            MemoryStream output = new MemoryStream(row.Length);
            row.WriteTo(output);
            buffer = new Memory<byte>(output.GetBuffer(), 0, (int)output.Length);
            return r;
        }

        private static Result WriteResult(ref RowWriter writer, TypeArgument typeArg, CosmosBatchOperationResult result)
        {
            Result r = writer.WriteInt32("statusCode", (int)result.StatusCode);
            if (r != Result.Success)
            {
                return r;
            }

            if (result.SubStatusCode != SubStatusCodes.Unknown)
            {
                r = writer.WriteInt32("subStatusCode", (int)result.SubStatusCode);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (result.ETag != null)
            {
                r = writer.WriteString("eTag", result.ETag);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (result.ResourceStream != null)
            {
                r = writer.WriteBinary("resourceBody", BatchResponsePayloadWriter.StreamToBytes(result.ResourceStream));
                if (r != Result.Success)
                {
                    return r;
                }
            }

            if (result.RetryAfter != null)
            {
                r = writer.WriteUInt32("retryAfterMilliseconds", (uint)result.RetryAfter.TotalMilliseconds);
                if (r != Result.Success)
                {
                    return r;
                }
            }

            return Result.Success;
        }

        private static byte[] StreamToBytes(MemoryStream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
