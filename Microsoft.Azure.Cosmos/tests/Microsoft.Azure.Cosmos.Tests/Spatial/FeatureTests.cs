//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Spatial
{
    using System;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class FeatureTests
    {
        [TestMethod]
        public void TestSerializationBasic()
        {
            string json =
                @"{
                    ""type"":""Feature"",
                    ""geometry"":{
                        ""type"":""Polygon"",
                        ""coordinates"":[
                            [
                                [
                                    100.0,
                                    0.0
                                ],
                                [
                                    101.0,
                                    0.0
                                ],
                                [
                                    101.0,
                                    1.0
                                ],
                                [
                                    100.0,
                                    1.0
                                ],
                                [
                                    100.0,
                                    0.0
                                ]
                            ]
                        ]
                    },
                    ""properties"":{
                        ""prop0"":""value0"",
                        ""prop1"":{
                            ""this"":""that""
                        }
                    }
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.AreEqual(GeoJsonType.Feature, feature.Type);
            Assert.IsNotNull(feature.Geometry);
            Assert.AreEqual(GeometryType.Polygon, feature.Geometry.Type);
            Assert.IsNotNull(feature.Properties);
            Assert.AreEqual("value0", feature.Properties["prop0"].Value<string>());
            Assert.AreEqual("that", ((JObject)feature.Properties["prop1"])["this"].Value<string>());

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestSerializationMissingType()
        {
            string json =
                @"{
                    ""geometry"":null,
                    ""properties"":null
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.IsNotNull(feature.Id);
            Assert.AreEqual(JTokenType.Integer, feature.Id.Type);
            Assert.AreEqual(42, feature.Id.Value<int>());

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestSerializationWrongType()
        {
            string json =
                @"{
                    ""type"":""FeatureCollection"",
                    ""geometry"":null,
                    ""properties"":null
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.IsNotNull(feature.Id);
            Assert.AreEqual(JTokenType.Integer, feature.Id.Type);
            Assert.AreEqual(42, feature.Id.Value<int>());

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        public void TestSerializationNullGeometry()
        {
            string json =
                @"{
                    ""type"":""Feature"",
                    ""geometry"":null,
                    ""properties"":{
                        ""prop0"":""value0"",
                        ""prop1"":{
                            ""this"":""that""
                        }
                    }
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.IsNull(feature.Geometry);

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(JsonSerializationException))]
        public void TestSerializationUndefinedGeometry()
        {
            string json =
                @"{
                    ""type"":""Feature"",
                    ""properties"":{
                        ""prop0"":""value0"",
                        ""prop1"":{
                            ""this"":""that""
                        }
                    }
                }";

            _ = JsonConvert.DeserializeObject<Feature>(json);
        }

        [TestMethod]
        public void TestSerializationNullProperties()
        {
            string json =
                @"{
                    ""type"":""Feature"",
                    ""geometry"":{
                        ""type"":""Polygon"",
                        ""coordinates"":[
                            [
                                [
                                    100.0,
                                    0.0
                                ],
                                [
                                    101.0,
                                    0.0
                                ],
                                [
                                    101.0,
                                    1.0
                                ],
                                [
                                    100.0,
                                    1.0
                                ],
                                [
                                    100.0,
                                    0.0
                                ]
                            ]
                        ]
                    },
                    ""properties"":null
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.IsNull(feature.Properties);

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(JsonSerializationException))]
        public void TestSerializationUndefinedProperties()
        {
            string json =
                @"{
                    ""type"":""Feature"",
                    ""geometry"":{
                        ""type"":""Polygon"",
                        ""coordinates"":[
                            [
                                [
                                    100.0,
                                    0.0
                                ],
                                [
                                    101.0,
                                    0.0
                                ],
                                [
                                    101.0,
                                    1.0
                                ],
                                [
                                    100.0,
                                    1.0
                                ],
                                [
                                    100.0,
                                    0.0
                                ]
                            ]
                        ]
                    }
                }";

            _ = JsonConvert.DeserializeObject<Feature>(json);
        }

        [TestMethod]
        public void TestSerializationStringId()
        {
            string json =
                @"{
                    ""id"":""This is a string"",
                    ""type"":""Feature"",
                    ""geometry"":null,
                    ""properties"":null
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.IsNotNull(feature.Id);
            Assert.AreEqual(JTokenType.String, feature.Id.Type);
            Assert.AreEqual("This is a string", feature.Id.Value<string>());

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        public void TestSerializationNumberId()
        {
            string json =
                @"{
                    ""id"":42,
                    ""type"":""Feature"",
                    ""geometry"":null,
                    ""properties"":null
                }";

            Feature feature = JsonConvert.DeserializeObject<Feature>(json);
            Assert.IsNotNull(feature.Id);
            Assert.AreEqual(JTokenType.Integer, feature.Id.Type);
            Assert.AreEqual(42, feature.Id.Value<int>());

            string serializedFeature = JsonConvert.SerializeObject(feature);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeature).ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestSerializationInvalidId()
        {
            string json =
                @"{
                    ""id"":{},
                    ""type"":""Feature"",
                    ""geometry"":null,
                    ""properties"":null
                }";

            _ = JsonConvert.DeserializeObject<Feature>(json);
        }

        [TestMethod]
        public void TestContstructor()
        {
            Feature feature = new Feature(geometry: null, properties: null);
            Assert.IsNull(feature.Geometry);
            Assert.IsNull(feature.Properties);
            Assert.IsNull(feature.Id);
            Assert.AreEqual(GeoJsonType.Feature, feature.Type);
        }
    }
}
