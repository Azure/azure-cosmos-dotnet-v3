//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test class for <see cref="CosmosSerializationUtil"/>
    /// </summary>
    [TestClass]
    public class CosmosSerializationUtilTests
    {
        [TestMethod]
        [DataRow((int)SupportedSerializationFormats.JsonText, "JsonText")]
        [DataRow((int)SupportedSerializationFormats.HybridRow, "HybridRow")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, "CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary), "JsonText, CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), "CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        [DataRow((int)(SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), "JsonText, CosmosBinary+Base64Strings+NumberArrays+UInt64, HybridRow")]
        public void GetSupportedSerializationFormatsAndFeatures_AdvertisesBinaryFeaturesOnlyForBinary(
            int formats,
            string expected)
        {
            Assert.AreEqual(expected, CosmosSerializationUtil.GetSupportedSerializationFormatsAndFeatures((SupportedSerializationFormats)formats));
        }

        [TestMethod]
        [DataRow(null, null, "JsonText,CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow(null, (int)SupportedSerializationFeatures.None, "JsonText,CosmosBinary")]
        [DataRow(null, (int)SupportedSerializationFeatures.NumberArrays, "JsonText,CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.None, "CosmosBinary")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.Base64Strings, "CosmosBinary+Base64Strings")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.NumberArrays, "CosmosBinary+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)SupportedSerializationFeatures.UInt64, "CosmosBinary+UInt64")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.NumberArrays), "CosmosBinary+Base64Strings+NumberArrays")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.UInt64), "CosmosBinary+Base64Strings+UInt64")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.NumberArrays | SupportedSerializationFeatures.UInt64), "CosmosBinary+NumberArrays+UInt64")]
        [DataRow((int)SupportedSerializationFormats.CosmosBinary, (int)(SupportedSerializationFeatures.Base64Strings | SupportedSerializationFeatures.NumberArrays | SupportedSerializationFeatures.UInt64), "CosmosBinary+Base64Strings+NumberArrays+UInt64")]
        [DataRow((int)(SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow), (int)SupportedSerializationFeatures.None, "CosmosBinary, HybridRow")]
        [DataRow((int)SupportedSerializationFormats.JsonText, (int)SupportedSerializationFeatures.UInt64, "JsonText")]
        [DataRow((int)SupportedSerializationFormats.HybridRow, (int)SupportedSerializationFeatures.Base64Strings, "HybridRow")]
        [DataRow((int)SupportedSerializationFormats.None, (int)SupportedSerializationFeatures.NumberArrays, "None")]
        public void GetSupportedSerializationFormatsAndFeatures_HonorsFeatureSelection(
            int? formats,
            int? features,
            string expected)
        {
            Assert.AreEqual(expected, CosmosSerializationUtil.GetSupportedSerializationFormatsAndFeatures(
                (SupportedSerializationFormats?)formats,
                (SupportedSerializationFeatures?)features));
        }

        [TestMethod]
        [DataRow("{\"value\":\"SGVsbG8gV29ybGQhSGVsbG8gV29ybGQ=\"}", (int)JsonWriteOptions.EnableBase64Strings)]
        [DataRow("[1000,1001,1002,1003,1004,1005,1006,1007]", (int)JsonWriteOptions.EnableNumberArrays)]
        [DataRow("{\"value\":18446744073709551615}", (int)JsonWriteOptions.EnableUInt64Values)]
        [DataRow("{\"base64\":\"SGVsbG8gV29ybGQhSGVsbG8gV29ybGQ=\",\"numbers\":[1000,1001,1002,1003,1004,1005,1006,1007],\"uint64\":18446744073709551615}",
            (int)(JsonWriteOptions.EnableBase64Strings | JsonWriteOptions.EnableNumberArrays | JsonWriteOptions.EnableUInt64Values))]
        public void ConvertToText_PreservesBinaryFeatureValues(string json, int writeOptions)
        {
            byte[] text = Encoding.UTF8.GetBytes(json);
            IJsonWriter binaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary, (JsonWriteOptions)writeOptions);
            JsonReader.Create(text).WriteAll(binaryWriter);

            IJsonWriter legacyWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            JsonReader.Create(text).WriteAll(legacyWriter);
            CollectionAssert.AreNotEqual(legacyWriter.GetResult().ToArray(), binaryWriter.GetResult().ToArray());

            using Stream converted = CosmosSerializationUtil.ConvertToStreamUsingJsonSerializationFormat(
                binaryWriter.GetResult(),
                JsonSerializationFormat.Text);
            using StreamReader reader = new StreamReader(converted);
            Assert.AreEqual(json, reader.ReadToEnd());
        }

        [TestMethod]
        public void GetStringWithPropertyNamingPolicy_CamelCase()
        {
            // Arrange
            CosmosLinqSerializerOptions options = new() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase };
            string propertyName = "TestProperty";

            // Act
            string result = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(options, propertyName);

            // Assert
            Assert.AreEqual("testProperty", result);
        }

        [TestMethod]
        public void GetStringWithPropertyNamingPolicy_Default()
        {
            // Arrange
            CosmosLinqSerializerOptions options = new() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.Default };
            string propertyName = "TestProperty";

            // Act
            string result = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(options, propertyName);

            // Assert
            Assert.AreEqual("TestProperty", result);
        }

        [TestMethod]
        public void IsBinaryFormat_True()
        {
            // Arrange
            int firstByte = (int)JsonSerializationFormat.Binary;
            JsonSerializationFormat format = JsonSerializationFormat.Binary;

            // Act
            bool result = CosmosSerializerUtils.IsBinaryFormat(firstByte, format);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsBinaryFormat_False()
        {
            // Arrange
            int firstByte = (int)JsonSerializationFormat.Text;
            JsonSerializationFormat format = JsonSerializationFormat.Binary;

            // Act
            bool result = CosmosSerializerUtils.IsBinaryFormat(firstByte, format);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsTextFormat_True()
        {
            // Arrange
            int firstByte = (int)JsonSerializationFormat.Text;
            JsonSerializationFormat format = JsonSerializationFormat.Text;

            // Act
            bool result = CosmosSerializerUtils.IsTextFormat(firstByte, format);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsTextFormat_False()
        {
            // Arrange
            int firstByte = (int)JsonSerializationFormat.Binary;
            JsonSerializationFormat format = JsonSerializationFormat.Text;

            // Act
            bool result = CosmosSerializerUtils.IsTextFormat(firstByte, format);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [DataRow("text", "binary", DisplayName = "Validate Text to Binary Conversation.")]
        [DataRow("binary", "text", DisplayName = "Validate Binary to Text Conversation.")]
        public async Task TrySerializeStreamToTargetFormat_Success(string expected, string target)
        {
            // Arrange
            JsonSerializationFormat expectedFormat = expected.Equals("text") ? JsonSerializationFormat.Text : JsonSerializationFormat.Binary;
            JsonSerializationFormat targetFormat = target.Equals("text") ? JsonSerializationFormat.Text : JsonSerializationFormat.Binary;
            string json = "{\"name\":\"test\"}";

            Stream inputStream = JsonSerializationFormat.Text.Equals(expectedFormat)
                ? CosmosSerializerUtils.ConvertInputToTextStream(json, Newtonsoft.Json.JsonSerializer.Create())
                : CosmosSerializerUtils.ConvertInputToBinaryStream(json, Newtonsoft.Json.JsonSerializer.Create());

            // Act
            CloneableStream cloneableStream = await StreamExtension.AsClonableStreamAsync(inputStream);
            Stream outputStream = CosmosSerializationUtil.TrySerializeStreamToTargetFormat(targetFormat, cloneableStream);

            // Assert
            Assert.IsNotNull(outputStream);
            Assert.IsTrue(CosmosSerializerUtils.CheckFirstBufferByte(outputStream, targetFormat, out byte[] binBytes));
            Assert.IsNotNull(binBytes);
            Assert.IsTrue(binBytes.Length > 0);
        }

        [TestMethod]
        public async Task TrySerializeStreamToTargetFormat_Failure()
        {
            // Arrange
            string json = "{\"name\":\"test\"}";
            Stream inputStream = CosmosSerializerUtils.ConvertInputToTextStream(json, Newtonsoft.Json.JsonSerializer.Create());
            JsonSerializationFormat targetFormat = JsonSerializationFormat.Text;

            // Act
            CloneableStream cloneableStream = await StreamExtension.AsClonableStreamAsync(inputStream);
            Stream outputStream = CosmosSerializationUtil.TrySerializeStreamToTargetFormat(targetFormat, cloneableStream);

            // Assert
            Assert.IsNotNull(outputStream);
            Assert.AreEqual(cloneableStream, outputStream);
        }
    }
}