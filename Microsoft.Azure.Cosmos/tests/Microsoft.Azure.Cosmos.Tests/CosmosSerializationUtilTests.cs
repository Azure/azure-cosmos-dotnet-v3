//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
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
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void ToStreamForTextJson_PreservesSettingsAndBinarySerializerConfiguration(int wrapperCount)
        {
            CosmosSerializer serializer = new CosmosJsonDotNetSerializer(
                new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                    Converters = { new CosmosSerializerHelper.FormatNumbersAsTextConverter() },
                },
                binaryEncodingEnabled: true);
            for (int i = 0; i < wrapperCount; i++)
            {
                serializer = new CosmosJsonSerializerWrapper(serializer);
            }

            var value = new { OriginalName = 42 };
            using (Stream binary = serializer.ToStream(value))
            {
                Assert.AreEqual((int)JsonSerializationFormat.Binary, binary.ReadByte());
            }

            using (Stream text = CosmosSerializationUtil.ToStreamForTextJson(serializer, value))
            using (StreamReader reader = new StreamReader(text))
            {
                Assert.AreEqual("{\"originalName\":\"42\"}", reader.ReadToEnd());
            }

            using (Stream binary = serializer.ToStream(value))
            {
                Assert.AreEqual((int)JsonSerializationFormat.Binary, binary.ReadByte(), "Text preference must not mutate the configured serializer.");
            }
        }

        [TestMethod]
        public void ToStreamForTextJson_RejectsNullSerializer()
        {
            Assert.ThrowsException<ArgumentNullException>(() => CosmosSerializationUtil.ToStreamForTextJson<object>(null, null));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ReadStreamAsTextJson_PreservesOwnershipAndReadsFromCurrentPosition(bool binaryInput)
        {
            const string json = "\"value\"";
            using Stream encoded = CosmosSerializationUtil.ConvertToStreamUsingJsonSerializationFormat(
                Encoding.UTF8.GetBytes(json),
                binaryInput ? JsonSerializationFormat.Binary : JsonSerializationFormat.Text);
            using MemoryStream stream = new MemoryStream();
            stream.WriteByte(0);
            encoded.CopyTo(stream);
            stream.Position = 1;

            Assert.AreEqual(json, CosmosSerializationUtil.ReadStreamAsTextJson(stream));
            Assert.IsTrue(stream.CanRead);
            Assert.AreEqual(stream.Length, stream.Position);
        }

        [TestMethod]
        public void ReadStreamAsTextJson_RejectsNullStream()
        {
            Assert.ThrowsException<ArgumentNullException>(() => CosmosSerializationUtil.ReadStreamAsTextJson(null));
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