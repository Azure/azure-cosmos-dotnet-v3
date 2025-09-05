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
                props.CompressedEncryptedPaths as IReadOnlyDictionary<string, int>,
                props.EncryptedData);
            writer.WriteEndObject();
            writer.Flush();
            string manualJson = System.Text.Encoding.UTF8.GetString(msManual.ToArray());

            // Serialize the DTO directly (short keys via attributes) and wrap it under the root _ei property
            string propsJson = JsonSerializer.Serialize(props);
            string serJson = $"{{\"{Constants.EncryptedInfo}\":{propsJson}}}";

            // Assert: structure equality by parsing to JsonDocument to ignore ordering nuances
            using JsonDocument d1 = JsonDocument.Parse(manualJson);
            using JsonDocument d2 = JsonDocument.Parse(serJson);

            JsonElementAssertEqual(d1.RootElement, d2.RootElement, $"Manual JSON: {manualJson}\nSerializer JSON: {serJson}");

            // Also assert fields exist and types are correct
            JsonElement ei = d1.RootElement.GetProperty(Constants.EncryptedInfo);
            Assert.AreEqual(props.EncryptionFormatVersion, ei.GetProperty(Constants.EncryptionFormatVersion).GetInt32());
            Assert.AreEqual(props.EncryptionAlgorithm, ei.GetProperty(Constants.EncryptionAlgorithm).GetString());
            Assert.AreEqual(props.DataEncryptionKeyId, ei.GetProperty(Constants.EncryptionDekId).GetString());
            Assert.AreEqual((int)props.CompressionAlgorithm, ei.GetProperty(Constants.CompressionAlgorithm).GetInt32());

            // _ed must always be present; empty byte[] serializes to empty base64 string
            Assert.IsTrue(ei.TryGetProperty(Constants.EncryptedData, out JsonElement edProp), "Missing _ed property in manual JSON");
            Assert.AreEqual(string.Empty, edProp.GetString());

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
                compressedEncryptedPaths: null,
                encryptedData: Array.Empty<byte>());
            writer.WriteEndObject();
            writer.Flush();

            using JsonDocument d = JsonDocument.Parse(ms.ToArray());
            JsonElement ei = d.RootElement.GetProperty(Constants.EncryptedInfo);
            Assert.IsFalse(ei.TryGetProperty(Constants.CompressedEncryptedPaths, out _));
            Assert.AreEqual(3, ei.GetProperty(Constants.EncryptionFormatVersion).GetInt32());
            Assert.AreEqual("alg", ei.GetProperty(Constants.EncryptionAlgorithm).GetString());
            Assert.AreEqual("kid", ei.GetProperty(Constants.EncryptionDekId).GetString());
            Assert.AreEqual(0, ei.GetProperty(Constants.CompressionAlgorithm).GetInt32());

            // _ed should still be present even when cep is omitted
            Assert.IsTrue(ei.TryGetProperty(Constants.EncryptedData, out JsonElement ed2));
            Assert.AreEqual(string.Empty, ed2.GetString());
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
                props.CompressedEncryptedPaths as IReadOnlyDictionary<string, int>,
                props.EncryptedData);
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
            JsonElementAssertEqual(d1.RootElement, d2.RootElement, $"Manual JSON: {manualJson}\nWrapper JSON: {wrapperJson}");
        }

        private static void JsonElementAssertEqual(JsonElement x, JsonElement y, string context = null, string path = "$")
        {
            string Msg(string detail)
            {
                if (context == null)
                {
                    return $"{detail} at path '{path}'.";
                }
                else
                {
                    return $"{detail} at path '{path}'. Context:\n{context}";
                }
            }

            if (x.ValueKind != y.ValueKind)
            {
                Assert.AreEqual(x.ValueKind, y.ValueKind, Msg($"ValueKind mismatch: {x.ValueKind} != {y.ValueKind}"));
                return; // will not reach due to Assert failure
            }

            switch (x.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    JsonProperty[] xProps = x.EnumerateObject().OrderBy(p => p.Name).ToArray();
                    JsonProperty[] yProps = y.EnumerateObject().OrderBy(p => p.Name).ToArray();

                    if (xProps.Length != yProps.Length)
                    {
                        string xNames = string.Join(", ", xProps.Select(p => p.Name));
                        string yNames = string.Join(", ", yProps.Select(p => p.Name));
                        Assert.AreEqual(xProps.Length, yProps.Length, Msg($"Object property count mismatch: {xProps.Length} != {yProps.Length}.\nLeft: [{xNames}]\nRight: [{yNames}]"));
                    }

                    for (int i = 0; i < xProps.Length; i++)
                    {
                        if (!string.Equals(xProps[i].Name, yProps[i].Name, StringComparison.Ordinal))
                        {
                            Assert.AreEqual(xProps[i].Name, yProps[i].Name, Msg($"Property name mismatch at index {i}: '{xProps[i].Name}' != '{yProps[i].Name}'"));
                        }

                        string childPath = path == "$" ? $"$.{xProps[i].Name}" : $"{path}.{xProps[i].Name}";
                        JsonElementAssertEqual(xProps[i].Value, yProps[i].Value, context, childPath);
                    }
                    break;
                }

                case JsonValueKind.Array:
                {
                    JsonElement[] xArr = x.EnumerateArray().ToArray();
                    JsonElement[] yArr = y.EnumerateArray().ToArray();

                    if (xArr.Length != yArr.Length)
                    {
                        Assert.AreEqual(xArr.Length, yArr.Length, Msg($"Array length mismatch: {xArr.Length} != {yArr.Length}"));
                    }

                    for (int i = 0; i < xArr.Length; i++)
                    {
                        JsonElementAssertEqual(xArr[i], yArr[i], context, $"{path}[{i}]");
                    }
                    break;
                }

                case JsonValueKind.String:
                {
                    string xs = x.GetString();
                    string ys = y.GetString();
                    Assert.AreEqual(xs, ys, Msg($"String mismatch: '{xs}' != '{ys}'"));
                    break;
                }

                case JsonValueKind.Number:
                {
                    if (x.TryGetDecimal(out decimal dx) && y.TryGetDecimal(out decimal dy))
                    {
                        Assert.AreEqual(dx, dy, Msg($"Number mismatch: {dx} != {dy}"));
                    }
                    else
                    {
                        double xd = x.GetDouble();
                        double yd = y.GetDouble();
                        Assert.AreEqual(xd, yd, Msg($"Number mismatch: {xd} != {yd}"));
                    }
                    break;
                }

                case JsonValueKind.True:
                case JsonValueKind.False:
                {
                    bool xb = x.GetBoolean();
                    bool yb = y.GetBoolean();
                    Assert.AreEqual(xb, yb, Msg($"Boolean mismatch: {xb} != {yb}"));
                    break;
                }

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    // Both are null/undefined, nothing to assert further
                    break;

                default:
                    Assert.Fail(Msg($"Unsupported JsonValueKind: {x.ValueKind}"));
                    break;
            }
        }
    }
}
#endif
