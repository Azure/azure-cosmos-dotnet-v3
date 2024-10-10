#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class JsonNodeSqlSerializerTests
    {
        private static ArrayPoolManager _poolManager;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _ = context;
            _poolManager = new ArrayPoolManager();
        }

        [TestMethod]
        [DynamicData(nameof(SerializationSamples))]
        public void Serialize_SupportedValue(JsonNode testNode, byte expectedType, byte[] expectedBytes, int expectedLength)
        {
            JsonNodeSqlSerializer serializer = new();

            (TypeMarker serializedType, byte[] serializedBytes, int serializedBytesCount) = serializer.Serialize(testNode, _poolManager);

            Assert.AreEqual((TypeMarker)expectedType, serializedType);
            Assert.AreEqual(expectedLength, serializedBytesCount);
            if (expectedLength == -1)
            {
                Assert.IsTrue(serializedBytes == null);
            }
            else
            {
                Assert.IsTrue(expectedBytes.SequenceEqual(serializedBytes.AsSpan(0, serializedBytesCount).ToArray()));
            }
        }

        public static IEnumerable<object[]> SerializationSamples
        {
            get
            {
                List<object[]> values = new()
                {
                    new object[] {JsonValue.Create((string)null), (byte)TypeMarker.Null, null, -1 },
                    new object[] {JsonValue.Create(true), (byte)TypeMarker.Boolean, GetNewtonsoftValueEquivalent(true), 8},
                    new object[] {JsonValue.Create(false), (byte)TypeMarker.Boolean, GetNewtonsoftValueEquivalent(false), 8},
                    new object[] {JsonValue.Create(192), (byte)TypeMarker.Long, GetNewtonsoftValueEquivalent(192), 8},
                    new object[] {JsonValue.Create(192.5), (byte)TypeMarker.Double, GetNewtonsoftValueEquivalent(192.5), 8},
                    new object[] {JsonValue.Create(testString), (byte)TypeMarker.String, GetNewtonsoftValueEquivalent(testString), 11},
                    new object[] {JsonValue.Create(testArray), (byte)TypeMarker.Array, GetNewtonsoftValueEquivalent(testArray), 10},
                    new object[] {JsonValue.Create(testClass), (byte)TypeMarker.Object, GetNewtonsoftValueEquivalent(testClass), 33}
                };

                return values;
            }
        }

        private static readonly string testString = "Hello world";
        private static readonly int[] testArray = new[] {10, 18, 19};
        private static readonly TestClass testClass = new() { SomeInt = 1, SomeString = "asdf" };

        private class TestClass
        {
            public int SomeInt { get; set; }
            public string SomeString { get; set; }
        }

        private static byte[] GetNewtonsoftValueEquivalent<T>(T value)
        {
            JObjectSqlSerializer serializer = new ();
            JToken token = value switch
            {
                int[] => new JArray(value),
                TestClass => JObject.FromObject(value),
                _ => new JValue(value),
            };
            (TypeMarker _, byte[] bytes, int lenght) = serializer.Serialize(token, _poolManager);
            return bytes.AsSpan(0, lenght).ToArray();
        }

    }
}

#endif