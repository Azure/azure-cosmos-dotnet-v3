//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Net;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests for <see cref="DocumentAnalyzer"/>.
    /// </summary>
    [TestClass]
    public class DocumentAnalyzerTest
    {
        /// <summary>
        /// Tests extracting partition keys from documents.
        /// </summary>
        [TestMethod]
        public void TestExtractEmptyPartitionKey()
        {
            Document document = new Document();

            PartitionKeyInternal partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, null);
            Assert.AreEqual(PartitionKeyInternal.Empty, partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), null);
            Assert.AreEqual(PartitionKeyInternal.Empty, partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, new PartitionKeyDefinition());
            Assert.AreEqual(PartitionKeyInternal.Empty, partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), new PartitionKeyDefinition());
            Assert.AreEqual(PartitionKeyInternal.Empty, partitionKeyValue);
        }

        /// <summary>
        /// Tests extracting partition keys from documents.
        /// </summary>
        [TestMethod]
        public void TestExtractUndefinedPartitionKey()
        {
            dynamic document = new Document();

            PartitionKeyDefinition[] partitionKeyDefinitions = new PartitionKeyDefinition[]
            {
                new PartitionKeyDefinition {
                    Paths = new Collection<string> { "/address/Country", },
                    Kind = PartitionKind.Range
                },
                new PartitionKeyDefinition {
                    Paths = new Collection<string> { "/address/Country/something", },
                    Kind = PartitionKind.Range
                },
                new PartitionKeyDefinition {
                    Paths = new Collection<string> { "/address", },
                    Kind = PartitionKind.Range
                },
            };

            foreach (PartitionKeyDefinition definition in partitionKeyDefinitions)
            {
                PartitionKeyInternal partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, definition);
                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true),
                    partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), definition);
                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true),
                    partitionKeyValue);

                document = new Document();
                document.address = new JObject();

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, definition);
                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true),
                    partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), definition);
                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true),
                    partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new Entity { Address = new Address { Country = new object() } }), definition);
                Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true), partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new Entity { Address = new Address { Country = new object() } })), definition);
                Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true), partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new DocumentEntity { Address = new Address { Country = new object() } }), definition);
                Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true), partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new DocumentEntity { Address = new Address { Country = new object() } })), definition);
                Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true), partitionKeyValue);

                dynamic address = new JObject();
                address.Country = new JObject();

                document = new Document();
                document.address = address;

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, definition);
                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true),
                    partitionKeyValue);

                partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), definition);
                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { Undefined.Value }, true),
                    partitionKeyValue);
            }
        }

        /// <summary>
        /// Extract valid string Partition Key value.
        /// </summary>
        [TestMethod]
        public void TestExtractValidStringPartitionKey()
        {
            dynamic address = new JObject();
            address.Country = "USA";

            dynamic document = new Document();
            document.address = address;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Kind = PartitionKind.Hash, Paths = new Collection<string> { "/address/Country" } };

            PartitionKeyInternal partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "USA" }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "USA" }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new Entity { Address = new Address { Country = "USA" } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "USA" }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new Entity { Address = new Address { Country = "USA" } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "USA" }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new DocumentEntity { Address = new Address { Country = "USA" } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "USA" }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new DocumentEntity { Address = new Address { Country = "USA" } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "USA" }, true), partitionKeyValue);

            dynamic country = new JObject();
            country.something = "foo";

            address = new JObject();
            address.Country = country;

            document = new Document();
            document.address = address;

            partitionKeyDefinition = new PartitionKeyDefinition { Kind = PartitionKind.Hash, Paths = new Collection<string> { "/address/Country/something" } };

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "foo" }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { "foo" }, true), partitionKeyValue);
        }

        /// <summary>
        /// Extract valid null Partition Key value.
        /// </summary>
        [TestMethod]
        public void TestExtractValidNullPartitionKey()
        {
            dynamic address = new JObject();
            address.Country = null;

            dynamic document = new Document();
            document.address = address;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Kind = PartitionKind.Hash, Paths = new Collection<string> { "/address/Country" } };

            PartitionKeyInternal partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { null }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { null }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new Entity { Address = new Address { Country = null } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { null }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new Entity { Address = new Address { Country = null } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { null }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new DocumentEntity { Address = new Address { Country = null } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { null }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new DocumentEntity { Address = new Address { Country = null } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { null }, true), partitionKeyValue);
        }

        /// <summary>
        /// Extract valid bool Partition Key value.
        /// </summary>
        [TestMethod]
        public void TestExtractValidBoolPartitionKey()
        {
            dynamic address = new JObject();
            address.Country = true;

            dynamic document = new Document();
            document.address = address;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Kind = PartitionKind.Hash, Paths = new Collection<string> { "/address/Country" } };

            PartitionKeyInternal partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { true }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { true }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new Entity { Address = new Address { Country = true } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { true }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new Entity { Address = new Address { Country = true } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { true }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new DocumentEntity { Address = new Address { Country = true } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { true }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new DocumentEntity { Address = new Address { Country = true } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { true }, true), partitionKeyValue);
        }

        /// <summary>
        /// Extract valid bool Partition Key value.
        /// </summary>
        [TestMethod]
        public void TestExtractValidNumberPartitionKey()
        {
            dynamic address = new JObject();
            address.Country = 5.5;

            dynamic document = new Document();
            document.address = address;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Kind = PartitionKind.Hash, Paths = new Collection<string> { "/address/Country" } };

            PartitionKeyInternal partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { 5.5 }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(document), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { 5.5 }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new Entity { Address = new Address { Country = 5.5 } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { 5.5 }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new Entity { Address = new Address { Country = 5.5 } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { 5.5 }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(JsonConvert.SerializeObject(Document.FromObject(new DocumentEntity { Address = new Address { Country = 5.5 } })), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { 5.5 }, true), partitionKeyValue);

            partitionKeyValue = DocumentAnalyzer.ExtractPartitionKeyValue(Document.FromObject(new DocumentEntity { Address = new Address { Country = 5.5 } }), partitionKeyDefinition);
            Assert.AreEqual(PartitionKeyInternal.FromObjectArray(new object[] { 5.5 }, true), partitionKeyValue);
        }

        [TestMethod]
        public void TestExtractSpecialTypePartitionKey()
        {
            foreach ((string fieldName, object value, Func<object, object> toJsonValue) in new (string, object, Func<object, object>)[]
            {
                ("Guid", Guid.NewGuid(), val => val.ToString()),
                ("DateTime", DateTime.Now, val =>
                    {
                        string str = JsonConvert.SerializeObject(
                            val,
                            new JsonSerializerSettings()
                            {
                                Converters = new List<JsonConverter>
                                {
                                    new IsoDateTimeConverter()
                                }
                            });
                        return str[1..^1];
                    }),
                ("Enum", HttpStatusCode.OK, val => (int)val),
                ("CustomEnum", HttpStatusCode.OK, val => val.ToString()),
                ("ResourceId", "testid", val => val),
                ("CustomDateTime", new DateTime(2016, 11, 14), val => EpochDateTimeConverter.DateTimeToEpoch((DateTime)val)),
            })
            {
                SpecialPropertyDocument sd = new SpecialPropertyDocument();
                sd.GetType().GetProperty(fieldName).SetValue(sd, value);

                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
                {
                    Kind = PartitionKind.Hash,
                    Paths = new Collection<string>
                    {
                        "/" + fieldName
                    }
                };

                PartitionKeyInternal partitionKeyValue = DocumentAnalyzer
                    .ExtractPartitionKeyValue(
                        Document.FromObject(
                            sd,
                            new JsonSerializerSettings()
                            {

                            }),
                        partitionKeyDefinition);

                Assert.AreEqual(
                    PartitionKeyInternal.FromObjectArray(new object[] { toJsonValue(value) }, strict: true),
                    partitionKeyValue,
                    message: $"fieldName: {fieldName}, value: {value}, valueToJson: {toJsonValue(value)}.");
            }
        }

        private class Address
        {
            [JsonProperty("Country")]
            public object Country { get; set; }
        }

        private class Entity
        {
            [JsonProperty("address")]
            public Address Address { get; set; }
        }

        private class DocumentEntity : Document
        {
            [JsonProperty("address")]
            public Address Address { get; set; }
        }

        private sealed class SpecialPropertyDocument
        {
            public Guid Guid
            {
                get;
                set;
            }

            [JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime DateTime
            {
                get;
                set;
            }

            [JsonConverter(typeof(EpochDateTimeConverter))]
            public DateTime CustomDateTime
            {
                get;
                set;
            }

            public HttpStatusCode Enum
            {
                get;
                set;
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public HttpStatusCode CustomEnum
            {
                get;
                set;
            }

            public string ResourceId
            {
                get;
                set;
            }
        }

        private sealed class EpochDateTimeConverter : JsonConverter
        {
            public static int DateTimeToEpoch(DateTime dt)
            {
                if (!dt.Equals(DateTime.MinValue))
                {
                    DateTime epoch = new DateTime(1970, 1, 1);
                    TimeSpan epochTimeSpan = dt - epoch;
                    return (int)epochTimeSpan.TotalSeconds;

                }
                else
                {
                    return int.MinValue;
                }
            }

            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.Null)
                    return null;

                if (reader.TokenType != JsonToken.Integer)
                {
                    throw new Exception(
                        string.Format(
                        CultureInfo.InvariantCulture,
                        "Unexpected token parsing date. Expected Integer, got {0}.",
                        reader.TokenType));
                }

                int seconds = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                return new DateTime(1970, 1, 1).AddSeconds(seconds);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                int seconds = value is DateTime time ? DateTimeToEpoch(time) : throw new Exception("Expected date object value.");
                writer.WriteValue(seconds);
            }
        }
    }
}