//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="LineString"/> class and serialization.
    /// </summary>
    [TestClass]
    public class LineStringTest
    {
        private JsonSerializerOptions restContractOptions;
        public LineStringTest()
        {
            this.restContractOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeRESTConverters(this.restContractOptions);
        }

        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestLineStringSerialization()
        {
            string json =
                @"{
                   ""type"":""LineString"",
                   ""coordinates"":[[20,30], [21,31]],
                   ""bbox"":[20, 20, 30, 30],
                   ""extra"":1,
                   ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}
                  }";
            var lineString = JsonSerializer.Deserialize<LineString>(json, this.restContractOptions);

            Assert.AreEqual(2, lineString.Positions.Count);
            Assert.AreEqual(new Position(20, 30), lineString.Positions[0]);
            Assert.AreEqual(new Position(21, 31), lineString.Positions[1]);

            Assert.AreEqual(new Position(20, 20), lineString.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), lineString.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)lineString.Crs).Name);
            Assert.AreEqual(1, lineString.AdditionalProperties.Count);
            Assert.AreEqual(1L, lineString.AdditionalProperties["extra"]);

            var geom = JsonSerializer.Deserialize<Geometry>(json, this.restContractOptions);
            Assert.AreEqual(GeometryType.LineString, geom.Type);

            Assert.AreEqual(geom, lineString);

            string json1 = JsonSerializer.Serialize(lineString, this.restContractOptions);
            var geom1 = JsonSerializer.Deserialize<Geometry>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestLineStringEqualsHashCode()
        {
            var lineString1 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var lineString2 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var lineString3 = new LineString(
                new[] { new Position(20, 30), new Position(30, 41) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var lineString4 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var lineString5 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    Crs = Crs.Named("SomeCrs")
                });

            var lineString6 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs1")
                });

            Assert.AreEqual(lineString1, lineString2);
            Assert.AreEqual(lineString1.GetHashCode(), lineString2.GetHashCode());

            Assert.AreNotEqual(lineString1, lineString3);
            Assert.AreNotEqual(lineString1.GetHashCode(), lineString3.GetHashCode());

            Assert.AreNotEqual(lineString1, lineString4);
            Assert.AreNotEqual(lineString1.GetHashCode(), lineString4.GetHashCode());

            Assert.AreNotEqual(lineString1, lineString5);
            Assert.AreNotEqual(lineString1.GetHashCode(), lineString5.GetHashCode());

            Assert.AreNotEqual(lineString1, lineString6);
            Assert.AreNotEqual(lineString1.GetHashCode(), lineString6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestLineStringConstructorNullException()
        {
            new LineString(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestLineStringConstructors()
        {
            var lineString = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Assert.AreEqual(new Position(20, 30), lineString.Positions[0]);
            Assert.AreEqual(new Position(30, 40), lineString.Positions[1]);

            Assert.AreEqual(new Position(0, 0), lineString.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), lineString.BoundingBox.Max);
            Assert.AreEqual("b", lineString.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)lineString.Crs).Name);
        }
    }
}
