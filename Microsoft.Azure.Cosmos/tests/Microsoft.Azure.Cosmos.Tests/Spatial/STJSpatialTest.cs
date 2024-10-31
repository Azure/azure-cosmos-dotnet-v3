namespace Microsoft.Azure.Cosmos.Tests.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    /// <summary>
    /// Tests <see cref="Point"/> class and serialization.
    /// </summary>
    [TestClass]
    public class STJSpatialTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestDictSerialization()
        {
            JsonSerializerOptions serializeOptions = new JsonSerializerOptions
            {
                Converters =
                {
                    new DictionarySTJConverter()
                }
            };
            Dictionary<string, object> input = new Dictionary<string, object>
            {
                ["battle"] = "a large abttle",
                ["cruise"] = "a new cruise"
            };

            string json = System.Text.Json.JsonSerializer.Serialize(input, serializeOptions);

            JsonSerializerOptions deserializeOptions = new JsonSerializerOptions();
            deserializeOptions.Converters.Add(new DictionarySTJConverter());


            Dictionary<string, object> result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json, serializeOptions);
            Assert.AreEqual(input, result);

            /*Assert.AreEqual(2, point.Position.Coordinates.Count);
            Assert.AreEqual(20.232323232323232, point.Position.Longitude);
            Assert.AreEqual(30.3, point.Position.Latitude);
            Assert.AreEqual(new Position(20, 20), point.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), point.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)point.Crs).Name);
            Assert.AreEqual(1, point.AdditionalProperties.Count);
            Assert.AreEqual(1L, point.AdditionalProperties["extra"]);

            var geom = JsonConvert.DeserializeObject<Geometry>(json);
            Assert.AreEqual(GeometryType.Point, geom.Type);

            Assert.AreEqual(geom, point);

            string json1 = JsonConvert.SerializeObject(point);
            var geom1 = JsonConvert.DeserializeObject<Geometry>(json1);
            Assert.AreEqual(geom1, geom);*/
        }
    }
}
