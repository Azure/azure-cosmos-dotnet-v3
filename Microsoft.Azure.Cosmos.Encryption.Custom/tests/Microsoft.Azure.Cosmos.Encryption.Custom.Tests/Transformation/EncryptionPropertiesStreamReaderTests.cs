//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EncryptionPropertiesStreamReaderTests
    {
        private static readonly JsonSerializerOptions Options = PooledJsonSerializer.SerializerOptions;

        [TestMethod]
        public async Task ReadAsync_WhenEiPresentAtEnd_ReturnsProperties()
        {
            string json = /*lang=json,strict*/ @"{""id"":""a"",""val"":42,""_ei"":{""_ef"":3,""_ea"":""AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED"",""_en"":""dekId"",""_ep"":[""/p1"",""/p2""]}}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.EncryptionFormatVersion);
            Assert.AreEqual("dekId", result.DataEncryptionKeyId);
            CollectionAssert.AreEqual(new [] { "/p1", "/p2" }, new System.Collections.Generic.List<string>(result.EncryptedPaths));
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task ReadAsync_WhenEiPresentAtStart_ReturnsProperties()
        {
            string json = /*lang=json,strict*/ @"{""_ei"":{""_ef"":3,""_ea"":""AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED"",""_en"":""k"",""_ep"":[""/a""]},""big"":""" + new string('x', 8000) + @"""}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("k", result.DataEncryptionKeyId);
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task ReadAsync_WhenEiAbsent_ReturnsNull()
        {
            string json = /*lang=json,strict*/ @"{""id"":""a"",""val"":42}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNull(result);
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task ReadAsync_WhenDocumentHasLargeValueBeforeEi_GrowsBufferAndReturnsProperties()
        {
            // A property value that is larger than the 4 KB initial buffer forces the reader
            // to double the buffer before it can TrySkip past the value.
            string largeValue = new ('y', 20_000);
            string json = /*lang=json,strict*/ "{\"big\":\"" + largeValue + "\",\"_ei\":{\"_ef\":3,\"_ea\":\"AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED\",\"_en\":\"k2\",\"_ep\":[]}}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("k2", result.DataEncryptionKeyId);
        }

        [TestMethod]
        public async Task ReadAsync_WhenEiIsLastAndVeryLarge_Succeeds()
        {
            // _ei that itself spans more than the initial buffer size must still
            // materialize correctly via the grow path.
            System.Collections.Generic.List<string> paths = new ();
            for (int i = 0; i < 500; i++)
            {
                paths.Add("/path_with_a_long_prefix_" + i);
            }

            string pathsJson = "[" + string.Join(",", paths.ConvertAll(p => "\"" + p + "\"")) + "]";
            string json = "{\"id\":\"z\",\"_ei\":{\"_ef\":3,\"_ea\":\"AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED\",\"_en\":\"k3\",\"_ep\":" + pathsJson + "}}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("k3", result.DataEncryptionKeyId);
            Assert.AreEqual(500, new System.Collections.Generic.List<string>(result.EncryptedPaths).Count);
        }

        [TestMethod]
        public async Task ReadAsync_WhenEiIsNull_ReturnsNull()
        {
            string json = /*lang=json,strict*/ @"{""_ei"":null,""id"":""a""}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            // _ei:null is a "found but no properties" signal and must map to null to avoid
            // NullReferenceExceptions downstream.
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReadAsync_WhenEiIsNotObject_Throws()
        {
            string json = /*lang=json,strict*/ @"{""_ei"":""notAnObject""}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None));
        }

        [TestMethod]
        public async Task ReadAsync_WhenInputNull_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                async () => await EncryptionPropertiesStreamReader.ReadAsync(null, Options, CancellationToken.None));
        }

        [TestMethod]
        public async Task ReadAsync_WhenStreamIsEmpty_ThrowsJsonException()
        {
            // Utf8JsonReader with isFinalBlock:true on an empty span rejects the input with
            // a JsonReaderException ("The input does not contain any JSON tokens") which
            // derives from JsonException. This matches the behaviour of
            // System.Text.Json.JsonSerializer.DeserializeAsync on an empty stream,
            // preserving pre-existing contract.
            await using MemoryStream stream = new (Array.Empty<byte>());

            await ExpectJsonExceptionAsync(async () =>
                await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None));
        }

        [TestMethod]
        public async Task ReadAsync_WhenStreamIsOpenBraceOnly_ThrowsJsonException()
        {
            // A truncated JSON (just `{`) hits isFinalBlock:true with an incomplete object
            // and throws. Confirms we do not silently return null on malformed input.
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes("{"));

            await ExpectJsonExceptionAsync(async () =>
                await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None));
        }

        private static async Task ExpectJsonExceptionAsync(Func<Task> action)
        {
            try
            {
                await action();
                Assert.Fail("Expected a JsonException.");
            }
            catch (System.Text.Json.JsonException)
            {
                // Utf8JsonReader throws JsonReaderException (internal sealed) which derives
                // from JsonException; accept any JsonException subtype.
            }
        }

        [TestMethod]
        public async Task ReadAsync_WhenRootIsArray_ReturnsNull()
        {
            // A valid JSON document whose root is an array (not an object) is syntactically
            // well-formed but has no `_ei` property and no root-level `EndObject` token.
            // The scanner's loop exits cleanly on isFinalBlock:true and falls through to
            // the post-loop return-null path.
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes("[1,2,3]"));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNull(result);
            Assert.AreEqual(0, stream.Position);
        }

        [TestMethod]
        public async Task ReadAsync_WhenRootIsNumber_ReturnsNull()
        {
            // Same as above but with a primitive root.
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes("123"));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReadAsync_WhenRootIsString_ReturnsNull()
        {
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes("\"hello\""));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReadAsync_WhenInputIsNonSeekable_ThrowsArgumentException()
        {
            // The reader rewinds the input so the downstream decrypt loop can re-read the
            // document from the start. A non-seekable stream cannot support that contract;
            // the reader fails fast rather than silently producing partial output.
            string json = @"{""_ei"":{""_ef"":3,""_ea"":""AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED"",""_en"":""k"",""_ep"":[]},""id"":""x""}";
            await using Stream stream = new NonSeekableReadOnlyStream(Encoding.UTF8.GetBytes(json));

            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None));
        }

        [TestMethod]
        public async Task ReadAsync_WhenCancelled_PropagatesCancellation()
        {
            // The reader does ReadAsync on the input stream inside a loop. A pre-cancelled
            // token must propagate out (TaskCanceledException derives from
            // OperationCanceledException) rather than completing with partial data.
            // TrickleReadStream honours the token so ReadAsync returns FromCanceled.
            byte[] payload = Encoding.UTF8.GetBytes(@"{""id"":""a"",""_ei"":{""_ef"":3,""_ea"":""AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED"",""_en"":""k"",""_ep"":[]}}");
            await using TrickleReadStream stream = new (payload);
            using CancellationTokenSource cts = new ();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, cts.Token));
        }

        private sealed class NonSeekableReadOnlyStream : Stream
        {
            private readonly byte[] data;
            private int position;

            public NonSeekableReadOnlyStream(byte[] data) { this.data = data; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.position >= this.data.Length)
                {
                    return 0;
                }

                int n = Math.Min(count, this.data.Length - this.position);
                Array.Copy(this.data, this.position, buffer, offset, n);
                this.position += n;
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [TestMethod]
        public async Task ReadAsync_WhenStreamTricklesBytes_DoesNotGrowBufferPathologically()
        {
            // Guard against regressions to partial-read handling: a stream that returns one
            // byte at a time must not cause exponential buffer growth. A 10 KB document with
            // no individual value over 4 KB should resolve without ever growing the initial
            // 4 KB rent.
            System.Text.StringBuilder sb = new ();
            sb.Append("{\"filler\":\"");
            sb.Append('x', 500);
            sb.Append("\",\"_ei\":{\"_ef\":3,\"_ea\":\"AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED\",\"_en\":\"k\",\"_ep\":[]}}");
            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());

            await using TrickleReadStream stream = new (payload);
            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, Options, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("k", result.DataEncryptionKeyId);
        }

        /// <summary>
        /// A Stream that returns at most 1 byte per read, to exercise the partial-read
        /// (trickle) path of the streaming reader.
        /// </summary>
        private sealed class TrickleReadStream : Stream
        {
            private readonly byte[] data;
            private int position;

            public TrickleReadStream(byte[] data) { this.data = data; }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => this.data.Length;

            public override long Position
            {
                get => this.position;
                set => this.position = (int)value;
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (this.position >= this.data.Length || count == 0)
                {
                    return 0;
                }

                buffer[offset] = this.data[this.position++];
                return 1;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ValueTask.FromCanceled<int>(cancellationToken);
                }

                if (this.position >= this.data.Length || buffer.Length == 0)
                {
                    return new ValueTask<int>(0);
                }

                buffer.Span[0] = this.data[this.position++];
                return new ValueTask<int>(1);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                this.position = origin switch
                {
                    SeekOrigin.Begin => (int)offset,
                    SeekOrigin.Current => this.position + (int)offset,
                    SeekOrigin.End => this.data.Length + (int)offset,
                    _ => throw new ArgumentException("origin"),
                };
                return this.position;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
#endif
