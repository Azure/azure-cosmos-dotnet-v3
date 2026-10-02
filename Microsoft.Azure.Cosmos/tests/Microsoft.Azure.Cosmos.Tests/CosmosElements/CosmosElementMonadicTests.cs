//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosElementMonadicTests
    {
        [TestMethod]
        [DataRow(new byte[] { 0x09 }, DisplayName = "Tab")]
        [DataRow(new byte[] { 0x20 }, DisplayName = "Space")]
        [DataRow(new byte[] { 0x0A }, DisplayName = "LineFeed")]
        [DataRow(new byte[] { 0x0D }, DisplayName = "CarriageReturn")]
        [DataRow(new byte[] { 0x20, 0x09, 0x0D, 0x0A, 0x20 }, DisplayName = "MixedWhitespace")]
        public void CreateFromBuffer_WhitespaceOnlyText_ReturnsFailed(byte[] buffer)
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(buffer);

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(JsonParseException));
            Assert.IsFalse(CosmosElement.TryCreateFromBuffer(buffer, out CosmosElement _));
        }

        [TestMethod]
        public void CreateFromBuffer_EmptyBuffer_ReturnsFailed()
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(ReadOnlyMemory<byte>.Empty);

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(ArgumentException));
        }

        [TestMethod]
        public void CreateFromBuffer_MalformedBinary_ReturnsFailed()
        {
            byte[] buffer = new byte[]
            {
                0x80, 0xff, 0x9e, 0x2b, 0x22, 0x7e, 0x5b, 0x31, 0xe4, 0x2c,
                0x32, 0x2c, 0x33, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x04, 0x61, 0x00, 0x00, 0x00, 0x00, 0x31, 0x84,
                0x2c, 0x00, 0x22, 0x00, 0x15, 0x00, 0x00, 0x00, 0x00, 0x68,
            };

            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(buffer);

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(JsonParseException));
            Assert.IsFalse(CosmosElement.TryCreateFromBuffer(buffer, out CosmosElement _));
        }

        [TestMethod]
        public void CreateFromBuffer_RandomBuffers_NeverThrow()
        {
            Random random = new Random(Seed: 42);
            for (int i = 0; i < 20000; i++)
            {
                byte[] buffer = new byte[random.Next(1, 48)];
                random.NextBytes(buffer);
                if (i % 2 == 0)
                {
                    buffer[0] = (byte)JsonSerializationFormat.Binary;
                }

                try
                {
                    CosmosElement.Monadic.CreateFromBuffer(buffer);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"CreateFromBuffer threw {ex.GetType().Name} for input {BitConverter.ToString(buffer)}: {ex}");
                }
            }
        }

        [TestMethod]
        public void CreateFromBuffer_TrailingTokens_ReturnsFailed()
        {
            TryCatch<CosmosElement> result = CosmosElement.Monadic.CreateFromBuffer(System.Text.Encoding.UTF8.GetBytes("1 2"));

            Assert.IsTrue(result.Failed);
            Assert.IsInstanceOfType(result.Exception.InnerException, typeof(JsonParseException));
        }

        [TestMethod]
        public void CreateFromBuffer_ValidText_StillSucceeds()
        {
            TryCatch<CosmosObject> result = CosmosElement.Monadic.CreateFromBuffer<CosmosObject>(
                System.Text.Encoding.UTF8.GetBytes("  {\"a\": [1, 2, 3]}  "));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(3, ((CosmosArray)result.Result["a"]).Count);
        }
    }
}
