// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    [TestClass]
    public class StreamProcessor_EncryptionInfoTests
    {
        [TestMethod]
        public void WriteEncryptionInfo_WritesAllFields_AndMatchesJsonSerializer()
        {
            // Arrange
            CompressionOptions.CompressionAlgorithm alg =
#if NET8_0_OR_GREATER
                CompressionOptions.CompressionAlgorithm.Brotli;
#else
                (CompressionOptions.CompressionAlgorithm)1; // numeric equivalent without referencing undefined symbol on net6.0
#endif

            EncryptionProperties props = new EncryptionProperties(
                encryptionFormatVersion: 4,
                encryptionAlgorithm: "A256CBC-HS512",
                dataEncryptionKeyId: "dek-id-123",
                encryptedData: Array.Empty<byte>(),
                encryptedPaths: new[] { "/a", "/b", "/c" },
                compressionAlgorithm: alg,
                compressedEncryptedPaths: new Dictionary<string, int> { ["/a"] = 100, ["/c"] = 42 });

            using MemoryStream msManual = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(msManual, new JsonWriterOptions { Indented = false, SkipValidation = true });

            // Act: write root object with manual _ei
            writer.WriteStartObject();
            StreamProcessor.WriteEncryptionInfo(
                writer,
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                props.EncryptedPaths.ToList(),
                props.CompressionAlgorithm,
                props.CompressedEncryptedPaths as IReadOnlyDictionary<string, int>);
            writer.WriteEndObject();
            writer.Flush();
            string manualJson = System.Text.Encoding.UTF8.GetString(msManual.ToArray());

            // Serialize the DTO directly (short keys via attributes) and wrap it under the root _ei property
            string propsJson = JsonSerializer.Serialize(props);
            string serJson = $"{{\"{Constants.EncryptedInfo}\":{propsJson}}}";

            // Assert: structure equality by parsing to JsonDocument to ignore ordering nuances
            using JsonDocument d1 = JsonDocument.Parse(manualJson);
            using JsonDocument d2 = JsonDocument.Parse(serJson);

            Assert.IsTrue(JsonElementDeepEquals(d1.RootElement, d2.RootElement), $"Manual JSON: {manualJson}\nSerializer JSON: {serJson}");

            // Also assert fields exist and types are correct
            JsonElement ei = d1.RootElement.GetProperty(Constants.EncryptedInfo);
            Assert.AreEqual(props.EncryptionFormatVersion, ei.GetProperty(Constants.EncryptionFormatVersion).GetInt32());
            Assert.AreEqual(props.EncryptionAlgorithm, ei.GetProperty(Constants.EncryptionAlgorithm).GetString());
            Assert.AreEqual(props.DataEncryptionKeyId, ei.GetProperty(Constants.EncryptionDekId).GetString());
            Assert.AreEqual((int)props.CompressionAlgorithm, ei.GetProperty(Constants.CompressionAlgorithm).GetInt32());

            // ep array contents
            string[] ep = ei.GetProperty(Constants.EncryptedPaths).EnumerateArray().Select(e => e.GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { "/a", "/b", "/c" }, ep);

            // cep map
            Dictionary<string, int> cep = ei.GetProperty(Constants.CompressedEncryptedPaths).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
            Assert.AreEqual(2, cep.Count);
            Assert.AreEqual(100, cep["/a"]);
            Assert.AreEqual(42, cep["/c"]);
        }

        [TestMethod]
        public void WriteEncryptionInfo_OmitsCompressedEncryptedPaths_WhenNull()
        {
            using MemoryStream ms = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false, SkipValidation = true });

            writer.WriteStartObject();
            StreamProcessor.WriteEncryptionInfo(
                writer,
                formatVersion: 3,
                encryptionAlgorithm: "alg",
                dataEncryptionKeyId: "kid",
                encryptedPaths: new List<string> { "/p" },
                compressionAlgorithm: CompressionOptions.CompressionAlgorithm.None,
                compressedEncryptedPaths: null);
            writer.WriteEndObject();
            writer.Flush();

            using JsonDocument d = JsonDocument.Parse(ms.ToArray());
            JsonElement ei = d.RootElement.GetProperty(Constants.EncryptedInfo);
            Assert.IsFalse(ei.TryGetProperty(Constants.CompressedEncryptedPaths, out _));
            Assert.AreEqual(3, ei.GetProperty(Constants.EncryptionFormatVersion).GetInt32());
            Assert.AreEqual("alg", ei.GetProperty(Constants.EncryptionAlgorithm).GetString());
            Assert.AreEqual("kid", ei.GetProperty(Constants.EncryptionDekId).GetString());
            Assert.AreEqual(0, ei.GetProperty(Constants.CompressionAlgorithm).GetInt32());
        }

        [TestMethod]
        public void WriteEncryptionInfo_MatchesWrapperSerialization_WithExplicitOptions()
        {
            // Arrange
            EncryptionProperties props = new EncryptionProperties(
                encryptionFormatVersion: 4,
                encryptionAlgorithm: "A256CBC-HS512",
                dataEncryptionKeyId: "dek-id-123",
                encryptedData: Array.Empty<byte>(),
                encryptedPaths: new[] { "/a", "/b", "/c" },
                compressionAlgorithm: CompressionOptions.CompressionAlgorithm.Brotli,
                compressedEncryptedPaths: new Dictionary<string, int> { ["/a"] = 100, ["/c"] = 42 });

            using MemoryStream msManual = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(msManual, new JsonWriterOptions { Indented = false, SkipValidation = true });

            // Act: manual _ei under root
            writer.WriteStartObject();
            StreamProcessor.WriteEncryptionInfo(
                writer,
                props.EncryptionFormatVersion,
                props.EncryptionAlgorithm,
                props.DataEncryptionKeyId,
                props.EncryptedPaths.ToList(),
                props.CompressionAlgorithm,
                props.CompressedEncryptedPaths as IReadOnlyDictionary<string, int>);
            writer.WriteEndObject();
            writer.Flush();
            string manualJson = System.Text.Encoding.UTF8.GetString(msManual.ToArray());

            // Serialize wrapper with explicit options (no naming policy)
            EncryptionPropertiesWrapper wrapper = new EncryptionPropertiesWrapper(props);
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
            };
            string wrapperJson = JsonSerializer.Serialize(wrapper, options);

            // Assert equality ignoring ordering
            using JsonDocument d1 = JsonDocument.Parse(manualJson);
            using JsonDocument d2 = JsonDocument.Parse(wrapperJson);
            Assert.IsTrue(JsonElementDeepEquals(d1.RootElement, d2.RootElement), $"Manual JSON: {manualJson}\nWrapper JSON: {wrapperJson}");
        }

        private static bool JsonElementDeepEquals(JsonElement x, JsonElement y)
        {
            if (x.ValueKind != y.ValueKind)
            {
                return false;
            }

            switch (x.ValueKind)
            {
                case JsonValueKind.Object:
                    JsonProperty[] xProps = x.EnumerateObject().OrderBy(p => p.Name).ToArray();
                    JsonProperty[] yProps = y.EnumerateObject().OrderBy(p => p.Name).ToArray();
                    if (xProps.Length != yProps.Length) return false;
                    for (int i = 0; i < xProps.Length; i++)
                    {
                        if (xProps[i].Name != yProps[i].Name) return false;
                        if (!JsonElementDeepEquals(xProps[i].Value, yProps[i].Value)) return false;
                    }
                    return true;
                case JsonValueKind.Array:
                    JsonElement[] xArr = x.EnumerateArray().ToArray();
                    JsonElement[] yArr = y.EnumerateArray().ToArray();
                    if (xArr.Length != yArr.Length) return false;
                    for (int i = 0; i < xArr.Length; i++)
                    {
                        if (!JsonElementDeepEquals(xArr[i], yArr[i])) return false;
                    }
                    return true;
                case JsonValueKind.String:
                    return x.GetString() == y.GetString();
                case JsonValueKind.Number:
                    return x.GetDouble() == y.GetDouble();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return x.GetBoolean() == y.GetBoolean();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return true;
                default:
                    return false;
            }
        }
    }
}
#endif
