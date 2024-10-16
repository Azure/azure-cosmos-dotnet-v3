#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
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

        [TestMethod]
        [DynamicData(nameof(DeserializationSamples))]
        public void Deserialize_SupportedValue(byte typeMarkerByte, byte[] serializedBytes, JsonNode expectedNode)
        {
            JsonNodeSqlSerializer serializer = new();
            TypeMarker typeMarker = (TypeMarker)typeMarkerByte;
            JsonNode deserializedNode = serializer.Deserialize(typeMarker, serializedBytes);

            if ((expectedNode as JsonValue) != null)
            {
                AssertValueNodeEquality(expectedNode, deserializedNode);
                return;
            }

            if ((expectedNode as JsonArray) != null)
            {
                Assert.IsNotNull(deserializedNode as JsonArray);

                JsonArray expectedArray = expectedNode.AsArray();
                JsonArray deserializedArray = deserializedNode.AsArray();

                Assert.AreEqual(expectedArray.Count, deserializedArray.Count);

                for (int i = 0; i < deserializedNode.AsArray().Count; i++)
                {
                    AssertValueNodeEquality(expectedArray[i], deserializedArray[i]);
                }
                return;
            }

            if ((expectedNode as JsonObject) != null)
            {
                Assert.IsNotNull(deserializedNode as JsonObject);

                JsonObject expectedObject = expectedNode.AsObject();
                JsonObject deserializedObject = deserializedNode.AsObject();

                Assert.AreEqual(expectedObject.Count, deserializedObject.Count);

                foreach (KeyValuePair<string, JsonNode> expected in expectedObject)
                {
                    Assert.IsTrue(deserializedObject.ContainsKey(expected.Key));
                    AssertValueNodeEquality(expected.Value, deserializedObject[expected.Key]);
                }
                return;
            }

            Assert.Fail("Attempt to validate unsupported JsonNode type");
        }

        private static void AssertValueNodeEquality(JsonNode expectedNode, JsonNode actualNode)
        {
            JsonValue expectedValueNode = expectedNode.AsValue();
            JsonValue actualValueNode = actualNode.AsValue();

            Assert.AreEqual(expectedValueNode.GetValueKind(), actualValueNode.GetValueKind());
            Assert.AreEqual(expectedValueNode.ToString(), actualValueNode.ToString());
        }

        public static IEnumerable<object[]> DeserializationSamples
        {
            get
            {
                yield return new object[] { (byte)TypeMarker.Boolean, GetNewtonsoftValueEquivalent(true), JsonValue.Create(true) };
                yield return new object[] { (byte)TypeMarker.Boolean, GetNewtonsoftValueEquivalent(false), JsonValue.Create(false) };
                yield return new object[] { (byte)TypeMarker.Long, GetNewtonsoftValueEquivalent(192), JsonValue.Create(192) };
                yield return new object[] { (byte)TypeMarker.Double, GetNewtonsoftValueEquivalent(192.5), JsonValue.Create(192.5) };
                yield return new object[] { (byte)TypeMarker.String, GetNewtonsoftValueEquivalent(testString), JsonValue.Create(testString) };
                yield return new object[] { (byte)TypeMarker.Array, GetNewtonsoftValueEquivalent(testArray), JsonNode.Parse("[10,18,19]") };
                yield return new object[] { (byte)TypeMarker.Object, GetNewtonsoftValueEquivalent(testClass), JsonNode.Parse(testClass.ToJson()) };
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

            public string ToJson()
            {
                return JsonSerializer.Serialize(this);
            }
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