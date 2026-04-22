//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Exhaustive behavioural tests for <see cref="ReadOnlyBufferWriterStream"/>. The wrapper
    /// is on the hot decrypt return path, so every Stream override — including ones that the
    /// production call site does not exercise — is covered here so that future callers can
    /// rely on the full Stream contract.
    /// </summary>
    [TestClass]
    public class ReadOnlyBufferWriterStreamTests
    {
        private static RentArrayBufferWriter CreateWriter(byte[] payload)
        {
            RentArrayBufferWriter writer = new (Math.Max(1, payload.Length));
            payload.AsSpan().CopyTo(writer.GetSpan(payload.Length));
            writer.Advance(payload.Length);
            return writer;
        }

        private static ReadOnlyBufferWriterStream CreateStream(byte[] payload)
        {
            return new ReadOnlyBufferWriterStream(CreateWriter(payload));
        }

        [TestMethod]
        public void Ctor_WhenBufferWriterNull_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new ReadOnlyBufferWriterStream(null));
        }

        [TestMethod]
        public void Capabilities_Undisposed_AreReadAndSeekButNotWrite()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.IsTrue(stream.CanRead);
            Assert.IsTrue(stream.CanSeek);
            Assert.IsFalse(stream.CanWrite);
        }

        [TestMethod]
        public void Capabilities_Disposed_AreAllFalse()
        {
            ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            stream.Dispose();
            Assert.IsFalse(stream.CanRead);
            Assert.IsFalse(stream.CanSeek);
            Assert.IsFalse(stream.CanWrite);
        }

        [TestMethod]
        public void Length_ReturnsBytesWritten()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3, 4 });
            Assert.AreEqual(4, stream.Length);
        }

        [TestMethod]
        public void Position_StartsAtZero()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3 });
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public void Position_SetWithinRange_Updates()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3 });
            stream.Position = 2;
            Assert.AreEqual(2, stream.Position);
        }

        [TestMethod]
        public void Position_SetNegative_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream.Position = -1);
        }

        [TestMethod]
        public void Position_SetAboveIntMaxValue_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
        }

        [TestMethod]
        public void Flush_DoesNothing_AndFlushAsync_CompletesSynchronously()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            stream.Flush();
            Task t = stream.FlushAsync(CancellationToken.None);
            Assert.IsTrue(t.IsCompletedSuccessfully);
        }

        [TestMethod]
        public void Read_ReadsAllBytes_AdvancesPosition_ReturnsZeroAtEnd()
        {
            byte[] payload = { 10, 20, 30, 40 };
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            byte[] dest = new byte[4];

            int n = stream.Read(dest, 0, dest.Length);
            Assert.AreEqual(4, n);
            CollectionAssert.AreEqual(payload, dest);
            Assert.AreEqual(4, stream.Position);

            Assert.AreEqual(0, stream.Read(dest, 0, dest.Length));
        }

        [TestMethod]
        public void Read_CountExceedsRemaining_ReturnsOnlyRemaining()
        {
            byte[] payload = { 1, 2, 3 };
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            stream.Position = 1;

            byte[] dest = new byte[16];
            int n = stream.Read(dest, 5, 10);
            Assert.AreEqual(2, n);
            Assert.AreEqual((byte)2, dest[5]);
            Assert.AreEqual((byte)3, dest[6]);
        }

        [TestMethod]
        public void Read_NullBuffer_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentNullException>(() => stream.Read(null, 0, 1));
        }

        [TestMethod]
        public void Read_NegativeOffset_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream.Read(new byte[2], -1, 1));
        }

        [TestMethod]
        public void Read_NegativeCount_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream.Read(new byte[2], 0, -1));
        }

        [TestMethod]
        public void Read_OffsetPlusCountExceedsBuffer_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentException>(() => stream.Read(new byte[4], 2, 10));
        }

        [TestMethod]
        public void ReadSpan_ReadsAvailableBytes()
        {
            byte[] payload = { 1, 2, 3, 4, 5 };
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            Span<byte> dest = stackalloc byte[3];

            int n = stream.Read(dest);
            Assert.AreEqual(3, n);
            Assert.AreEqual((byte)1, dest[0]);
            Assert.AreEqual((byte)2, dest[1]);
            Assert.AreEqual((byte)3, dest[2]);
            Assert.AreEqual(3, stream.Position);
        }

        [TestMethod]
        public void ReadSpan_AtEnd_ReturnsZero()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            stream.Position = stream.Length;
            Span<byte> dest = stackalloc byte[2];
            Assert.AreEqual(0, stream.Read(dest));
        }

        [TestMethod]
        public async Task ReadAsync_ByteArray_Succeeds()
        {
            byte[] payload = { 9, 8, 7 };
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            byte[] dest = new byte[3];

            int n = await stream.ReadAsync(dest, 0, 3, CancellationToken.None);
            Assert.AreEqual(3, n);
            CollectionAssert.AreEqual(payload, dest);
        }

        [TestMethod]
        public async Task ReadAsync_ByteArray_WhenCancelled_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            using CancellationTokenSource cts = new ();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await stream.ReadAsync(new byte[1], 0, 1, cts.Token));
        }

        [TestMethod]
        public async Task ReadAsync_ByteArray_InvalidArguments_PropagateAsFaultedTask()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                async () => await stream.ReadAsync(null, 0, 1, CancellationToken.None));
        }

        [TestMethod]
        public async Task ReadAsync_Memory_Succeeds()
        {
            byte[] payload = { 11, 12 };
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            byte[] dest = new byte[2];

            int n = await stream.ReadAsync(dest.AsMemory(), CancellationToken.None);
            Assert.AreEqual(2, n);
            CollectionAssert.AreEqual(payload, dest);
        }

        [TestMethod]
        public async Task ReadAsync_Memory_WhenCancelled_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            using CancellationTokenSource cts = new ();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await stream.ReadAsync(new byte[1].AsMemory(), cts.Token));
        }

        [TestMethod]
        public async Task ReadAsync_Memory_AfterDispose_FaultsWithObjectDisposed()
        {
            ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            stream.Dispose();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                async () => await stream.ReadAsync(new byte[1].AsMemory(), CancellationToken.None));
        }

        [TestMethod]
        public void Seek_Begin_Updates()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3, 4 });
            long p = stream.Seek(2, SeekOrigin.Begin);
            Assert.AreEqual(2, p);
            Assert.AreEqual(2, stream.Position);
        }

        [TestMethod]
        public void Seek_Current_Updates()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3, 4 });
            stream.Position = 1;
            long p = stream.Seek(2, SeekOrigin.Current);
            Assert.AreEqual(3, p);
        }

        [TestMethod]
        public void Seek_End_Updates()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3, 4 });
            long p = stream.Seek(-2, SeekOrigin.End);
            Assert.AreEqual(2, p);
        }

        [TestMethod]
        public void Seek_InvalidOrigin_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentException>(() => stream.Seek(0, (SeekOrigin)99));
        }

        [TestMethod]
        public void Seek_NegativeResult_ThrowsIO()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        }

        [TestMethod]
        public void Seek_PastIntMaxValue_ThrowsIO()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<IOException>(() => stream.Seek((long)int.MaxValue + 1, SeekOrigin.Begin));
        }

        [TestMethod]
        public void SetLength_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<NotSupportedException>(() => stream.SetLength(10));
        }

        [TestMethod]
        public void Write_ByteArray_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        }

        [TestMethod]
        public void Write_Span_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<NotSupportedException>(() => stream.Write(new byte[1].AsSpan()));
        }

        [TestMethod]
        public async Task WriteAsync_ByteArray_FaultsAsNotSupported()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await stream.WriteAsync(new byte[1], 0, 1, CancellationToken.None));
        }

        [TestMethod]
        public async Task WriteAsync_Memory_FaultsAsNotSupported()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            await Assert.ThrowsExceptionAsync<NotSupportedException>(
                async () => await stream.WriteAsync(new byte[1].AsMemory(), CancellationToken.None));
        }

        [TestMethod]
        public void CopyTo_WritesAllRemainingBytes_AndAdvancesPosition()
        {
            byte[] payload = Encoding.UTF8.GetBytes("hello world");
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            using MemoryStream dest = new ();

            stream.CopyTo(dest, bufferSize: 4096);

            CollectionAssert.AreEqual(payload, dest.ToArray());
            Assert.AreEqual(payload.Length, stream.Position);
        }

        [TestMethod]
        public void CopyTo_WhenAlreadyAtEnd_WritesNothing()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3 });
            stream.Position = stream.Length;
            using MemoryStream dest = new ();

            stream.CopyTo(dest, 4096);
            Assert.AreEqual(0, dest.Length);
        }

        [TestMethod]
        public void CopyTo_NullDestination_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            Assert.ThrowsException<ArgumentNullException>(() => stream.CopyTo(null, 1024));
        }

        [TestMethod]
        public void CopyTo_NonPositiveBufferSize_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            using MemoryStream dest = new ();
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream.CopyTo(dest, 0));
        }

        [TestMethod]
        public async Task CopyToAsync_WritesAllRemainingBytes()
        {
            byte[] payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            using ReadOnlyBufferWriterStream stream = CreateStream(payload);
            using MemoryStream dest = new ();

            await stream.CopyToAsync(dest, 4096, CancellationToken.None);

            CollectionAssert.AreEqual(payload, dest.ToArray());
            Assert.AreEqual(payload.Length, stream.Position);
        }

        [TestMethod]
        public async Task CopyToAsync_NullDestination_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                async () => await stream.CopyToAsync(null, 1024, CancellationToken.None));
        }

        [TestMethod]
        public async Task CopyToAsync_NonPositiveBufferSize_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            using MemoryStream dest = new ();
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await stream.CopyToAsync(dest, 0, CancellationToken.None));
        }

        [TestMethod]
        public async Task CopyToAsync_WhenCancelled_Throws()
        {
            using ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            using MemoryStream dest = new ();
            using CancellationTokenSource cts = new ();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await stream.CopyToAsync(dest, 1024, cts.Token));
        }

        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1 });
            stream.Dispose();
            stream.Dispose();
            Assert.IsFalse(stream.CanRead);
        }

        [TestMethod]
        public void MembersThatEnsureNotDisposed_AllThrowAfterDispose()
        {
            ReadOnlyBufferWriterStream stream = CreateStream(new byte[] { 1, 2, 3 });
            stream.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => { long _ = stream.Length; });
            Assert.ThrowsException<ObjectDisposedException>(() => { long _ = stream.Position; });
            Assert.ThrowsException<ObjectDisposedException>(() => stream.Position = 0);
            Assert.ThrowsException<ObjectDisposedException>(() => stream.Flush());
            Assert.ThrowsException<ObjectDisposedException>(() => stream.FlushAsync(CancellationToken.None));
            Assert.ThrowsException<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
            Assert.ThrowsException<ObjectDisposedException>(() => stream.Read(new byte[1].AsSpan()));
            Assert.ThrowsException<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.ThrowsException<ObjectDisposedException>(() =>
            {
                using MemoryStream d = new ();
                stream.CopyTo(d, 1024);
            });
        }
    }
}
#endif
