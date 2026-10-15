//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    [TestClass]
    [DoNotParallelize]
    public class DirectJsonSerializableTests
    {
        [TestMethod]
        public void TestPOCOSerializationOnly()
        {
            bool originalValue = JsonSerializable.JustPocoSerialization;
            try
            {
                JsonSerializable.JustPocoSerialization = false;
                MyPOCO document = new MyPOCO
                {
                    NameToCountMapping = new Dictionary<string, int> { { "test", 123 } },
                };
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    ContractResolver = new JsonContractResolver(),
                };
                Document typedDocument = Document.FromObject(document, settings);
                using MemoryStream stream = new MemoryStream();
                Assert.ThrowsException<JsonWriterException>(
                    () => typedDocument.SaveTo(stream, SerializationFormattingPolicy.None, settings));

                JsonSerializable.JustPocoSerialization = true;
                stream.SetLength(0);
                typedDocument.SaveTo(stream, SerializationFormattingPolicy.None, settings);
                stream.Position = 0;
                using StreamReader reader = new StreamReader(stream);
                JObject actual = JObject.Parse(reader.ReadToEnd());
                Assert.AreEqual("1234", actual["id"].Value<string>());
                Assert.AreEqual("test", actual["NameToCountMapping"][0]["Key"].Value<string>());
                Assert.AreEqual(123, actual["NameToCountMapping"][0]["Value"].Value<int>());
            }
            finally
            {
                JsonSerializable.JustPocoSerialization = originalValue;
            }
        }

        private sealed class MyPOCO
        {
            public Dictionary<string, int> NameToCountMapping;

            [JsonProperty("id")]
            public string Id { get; private set; } = "1234";
        }

        private sealed class JsonContractResolver : DefaultContractResolver
        {
            protected override JsonContract CreateContract(Type type)
            {
                return type.GetInterfaces().Prepend(type).Any(
                    candidate => candidate.IsGenericType
                        ? candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                        : candidate == typeof(IDictionary))
                    ? this.CreateArrayContract(type)
                    : base.CreateContract(type);
            }
        }
    }
}