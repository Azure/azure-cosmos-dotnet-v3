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
    public sealed class FeatureCollectionTests
    {
        [TestMethod]
        public void TestSerializationBasic()
        {
            string json =
                @"{
                   ""type"": ""FeatureCollection"",
                   ""features"": [
	                   {
		                   ""type"": ""Feature"",
		                   ""geometry"": {
			                   ""type"": ""Point"",
			                   ""coordinates"": [102.0, 0.5]
		                   },
		                   ""properties"": {
			                   ""prop0"": ""value0""
		                   }
	                   }, 
	                   {
		                   ""type"": ""Feature"",
		                   ""geometry"": {
			                   ""type"": ""LineString"",
			                   ""coordinates"": [
				                   [102.0, 0.0],
				                   [103.0, 1.0],
				                   [104.0, 0.0],
				                   [105.0, 1.0]
			                   ]
		                   },
		                   ""properties"": {
			                   ""prop0"": ""value0"",
			                   ""prop1"": 0.0
		                   }
	                   }, 
	                   {
		                   ""type"": ""Feature"",
		                   ""geometry"": {
			                   ""type"": ""Polygon"",
			                   ""coordinates"": [
				                   [
					                   [100.0, 0.0],
					                   [101.0, 0.0],
					                   [101.0, 1.0],
					                   [100.0, 1.0],
					                   [100.0, 0.0]
				                   ]
			                   ]
		                   },
		                   ""properties"": {
			                   ""prop0"": ""value0"",
			                   ""prop1"": {
				                   ""this"": ""that""
			                   }
		                   }
	                   }
                   ]
                }";

            FeatureCollection featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(json);
            Assert.IsNotNull(featureCollection);
            Assert.AreEqual(GeoJsonType.FeatureCollection, featureCollection.Type);
            Assert.IsNotNull(featureCollection.Features);
            Assert.AreEqual(3, featureCollection.Features.Count);

            string serializedFeatureCollection = JsonConvert.SerializeObject(featureCollection);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeatureCollection).ToString());
        }

        [TestMethod]
        public void TestSerializationEmptyFeatures()
        {
            string json =
                @"{
                   ""type"": ""FeatureCollection"",
                   ""features"": [
                   ]
                }";

            FeatureCollection featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(json);
            Assert.IsNotNull(featureCollection);
            Assert.AreEqual(GeoJsonType.FeatureCollection, featureCollection.Type);
            Assert.IsNotNull(featureCollection.Features);
            Assert.AreEqual(0, featureCollection.Features.Count);

            string serializedFeatureCollection = JsonConvert.SerializeObject(featureCollection);
            Assert.AreEqual(JToken.Parse(json).ToString(), JToken.Parse(serializedFeatureCollection).ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestSerializationNullFeatures()
        {
            string json =
                @"{
                   ""type"": ""FeatureCollection"",
                   ""features"": null
                }";

            _ = JsonConvert.DeserializeObject<FeatureCollection>(json);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestSerializationUndefinedFeatures()
        {
            string json =
                @"{
                   ""type"": ""FeatureCollection"",
                   ""features"": null
                }";

            _ = JsonConvert.DeserializeObject<FeatureCollection>(json);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestSerializationNullFeatureInFeatures()
        {
            string json =
                @"{
                   ""type"": ""FeatureCollection"",
                   ""features"": [null]
                }";

            _ = JsonConvert.DeserializeObject<FeatureCollection>(json);
        }

        [TestMethod]
        [ExpectedException(typeof(JsonSerializationException))]
        public void TestSerializationNonFeatureInFeatures()
        {
            string json =
                @"{
                   ""type"": ""FeatureCollection"",
                   ""features"": [42]
                }";

            _ = JsonConvert.DeserializeObject<FeatureCollection>(json);
        }

        [TestMethod]
        [ExpectedException(typeof(JsonSerializationException))]
        public void TestSerializationWrongType()
        {
            string json =
                @"{
                   ""type"": ""WrongType"",
                   ""features"": []
                }";

            _ = JsonConvert.DeserializeObject<FeatureCollection>(json);
        }

        [TestMethod]
        [ExpectedException(typeof(JsonSerializationException))]
        public void TestSerializationMissingType()
        {
            string json =
                @"{
                   ""features"": []
                }";

            _ = JsonConvert.DeserializeObject<FeatureCollection>(json);
        }
    }
}
