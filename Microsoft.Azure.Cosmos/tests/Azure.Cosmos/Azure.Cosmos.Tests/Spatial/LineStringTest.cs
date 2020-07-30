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
    public class LineStringTest : CommonSerializationTest
    {
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
                   ""bbox"":[20, 20, 30, 30]
                  }";
            LineString lineString = JsonSerializer.Deserialize<LineString>(json, this.restContractOptions);

            Assert.AreEqual(2, lineString.Coordinates.Count);
            Assert.AreEqual(new Position(20, 30), lineString.Coordinates[0]);
            Assert.AreEqual(new Position(21, 31), lineString.Coordinates[1]);

            Assert.AreEqual((20, 20), lineString.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual((30, 30), lineString.BoundingBox.NortheasertlyPoint);

            GeoJson geom = JsonSerializer.Deserialize<GeoJson>(json, this.restContractOptions);
            Assert.AreEqual(GeoJsonType.LineString, geom.Type);

            Assert.AreEqual(geom, lineString);

            string json1 = JsonSerializer.Serialize(lineString, this.restContractOptions);
            GeoJson geom1 = JsonSerializer.Deserialize<GeoJson>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestLineStringEqualsHashCode()
        {
            LineString lineString1 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox((0, 0), (40, 40)));

            LineString lineString2 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox((0, 0), (40, 40)));

            LineString lineString3 = new LineString(
                new[] { new Position(20, 30), new Position(30, 41) },
                new BoundingBox((0, 0), (40, 40)));

            LineString lineString4 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox((0, 0), (40, 40)));

            LineString lineString5 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox((0, 0), (40, 41)));

            LineString lineString6 = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox((0, 0), (40, 40)));

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
            LineString lineString = new LineString(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox((0, 0), (40, 40)));

            Assert.AreEqual(new Position(20, 30), lineString.Coordinates[0]);
            Assert.AreEqual(new Position(30, 40), lineString.Coordinates[1]);

            Assert.AreEqual((0, 0), lineString.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual((40, 40), lineString.BoundingBox.NortheasertlyPoint);
        }
    }
}
