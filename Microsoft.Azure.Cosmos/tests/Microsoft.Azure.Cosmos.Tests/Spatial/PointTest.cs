//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Spatial.Converters;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    /// <summary>
    /// Tests <see cref="Point"/> class and serialization.
    /// </summary>
    [TestClass]
    public class PointTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestPointSerialization()
        {
            string json =
                @"{
                    ""type"":""Point"",
                    ""coordinates"":[20.232323232323232,30.3],
                    ""bbox"":[20, 20, 30, 30],
                    ""extra"":1, 
                    ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}}";
            Point point = JsonConvert.DeserializeObject<Point>(json);

            Assert.AreEqual(2, point.Position.Coordinates.Count);
            Assert.AreEqual(20.232323232323232, point.Position.Longitude);
            Assert.AreEqual(30.3, point.Position.Latitude);
            Assert.AreEqual(new Position(20, 20), point.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), point.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)point.Crs).Name);
            Assert.AreEqual(1, point.AdditionalProperties.Count);
            Assert.AreEqual(1L, point.AdditionalProperties["extra"]);

            Geometry geom = JsonConvert.DeserializeObject<Geometry>(json);
            Assert.AreEqual(GeometryType.Point, geom.Type);

            Assert.AreEqual(geom, point);

            string json1 = JsonConvert.SerializeObject(point);
            Geometry geom1 = JsonConvert.DeserializeObject<Geometry>(json1);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestPointEqualsHashCode()
        {
            Point point1 = new Point(
                new Position(20, 30),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Point point2 = new Point(
                new Position(20, 30),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Point point3 = new Point(
                new Position(20, 31),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Point point4 = new Point(
                new Position(20, 31),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Point point5 = new Point(
                new Position(20, 30),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    Crs = Crs.Named("SomeCrs")
                });

            Point point6 = new Point(
                new Position(20, 30),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs1")
                });

            Assert.AreEqual(point1, point2);
            Assert.AreEqual(point1.GetHashCode(), point2.GetHashCode());

            Assert.AreNotEqual(point1, point3);
            Assert.AreNotEqual(point1.GetHashCode(), point3.GetHashCode());

            Assert.AreNotEqual(point1, point4);
            Assert.AreNotEqual(point1.GetHashCode(), point4.GetHashCode());

            Assert.AreNotEqual(point1, point5);
            Assert.AreNotEqual(point1.GetHashCode(), point5.GetHashCode());

            Assert.AreNotEqual(point1, point6);
            Assert.AreNotEqual(point1.GetHashCode(), point6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestPointConstructorNullException()
        {
            new Point(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestPointConstructors()
        {
            Point point = new Point(
                new Position(20, 30),
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Assert.AreEqual(20, point.Position.Longitude);
            Assert.AreEqual(30, point.Position.Latitude);
            Assert.AreEqual(new Position(0, 0), point.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), point.BoundingBox.Max);
            Assert.AreEqual("b", point.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)point.Crs).Name);
        }
    }
}