// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.IO;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RntbdStreamReaderTests
    {
        [Owner("visridha")]
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestReadWithBuffer(bool readIntoStream)
        {
            Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 124_000));
            RntbdStreamReader streamReader = new RntbdStreamReader(pipe.Reader.AsStream());

            // now write multiple requests, each 9k (mutually prime with the buffer).
            int requestSize = 9_000;
            await this.RunReadRequestsAsync(pipe, requestSize, streamReader, readIntoStream);
        }

        [Owner("visridha")]
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestReadWithBufferLargerThanCache(bool readIntoStream)
        {
            Pipe pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 124_000));
            RntbdStreamReader streamReader = new RntbdStreamReader(pipe.Reader.AsStream());

            // now write multiple requests, each 9k (mutually prime with the buffer).
            int requestSize = 18_000;
            await this.RunReadRequestsAsync(pipe, requestSize, streamReader, readIntoStream);
        }

        private async Task RunReadRequestsAsync(Pipe pipe, int requestSize, RntbdStreamReader streamReader, bool readIntoStream)
        {
            for (char requestChar = 'A'; requestChar < 'G'; requestChar++)
            {
                Memory<byte> writeMemory = pipe.Writer.GetMemory(requestSize);
                writeMemory.Span.Slice(0, requestSize).Fill((byte)requestChar);
                pipe.Writer.Advance(requestSize);
                await pipe.Writer.FlushAsync();
            }

            // now read the requests back.
            for (char requestChar = 'A'; requestChar < 'G'; requestChar++)
            {
                byte[] header = new byte[49];
                await this.ReadAsync(streamReader, header, readIntoStream);
                Assert.IsTrue(header.All(x => x == (byte)requestChar));
                if (requestChar == 'A')
                {
                    Assert.IsTrue(streamReader.AvailableByteCount >= Math.Min(requestSize - 49, 16000), $"Available {streamReader.AvailableByteCount} for char {requestChar}");
                }

                byte[] header2 = new byte[570];
                await this.ReadAsync(streamReader, header2, readIntoStream);
                Assert.IsTrue(header2.All(x => x == (byte)requestChar));

                byte[] bodyHeader = new byte[4];
                await this.ReadAsync(streamReader, bodyHeader, readIntoStream);
                Assert.IsTrue(bodyHeader.All(x => x == (byte)requestChar));

                byte[] remaining = new byte[requestSize - bodyHeader.Length - header2.Length - header.Length];
                await this.ReadAsync(streamReader, remaining, readIntoStream);
                Assert.IsTrue(remaining.All(x => x == (byte)requestChar));
            }
        }

        private Task ReadAsync(RntbdStreamReader streamReader, byte[] buffer, bool readIntoStream)
        {
            if (readIntoStream)
            {
                return this.ReadIntoStreamAsync(streamReader, new MemoryStream(buffer));
            }
            else
            {
                return this.ReadIntoStreamAsync(streamReader, buffer);
            }
        }

        private async Task ReadIntoStreamAsync(RntbdStreamReader streamReader, byte[] header)
        {
            int bytesRead = 0;
            do
            {
                int read = await streamReader.ReadAsync(header, bytesRead, header.Length - bytesRead);
                Assert.AreNotEqual(read, 0);
                bytesRead += read;
            }
            while (bytesRead < header.Length);

            Assert.AreEqual(bytesRead, header.Length);
        }

        private async Task ReadIntoStreamAsync(RntbdStreamReader streamReader, MemoryStream header)
        {
            int bytesRead = 0;
            do
            {
                int read = await streamReader.ReadAsync(header, (int)(header.Length - bytesRead));
                Assert.AreNotEqual(read, 0);
                bytesRead += read;
            }
            while (bytesRead < header.Length);

            Assert.AreEqual(bytesRead, header.Length);
        }
    }
}