//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Tests <see cref="GeometryCollection"/> class and serialization.
    /// </summary>
    [TestClass]
    public class GeometryCollectionTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionSerialization()
        {
            string json =
                @"{
                   ""type"":""GeometryCollection"",
                   ""geometries"":[{""type"":""Point"", ""coordinates"":[20, 20]}],
                   ""bbox"":[20, 20, 30, 30],
                   ""extra"":1,
                   ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}
                  }";

            GeometryCollection geometryCollection = JsonConvert.DeserializeObject<GeometryCollection>(json);

            Assert.AreEqual(1, geometryCollection.Geometries.Count);
            Assert.IsInstanceOfType(geometryCollection.Geometries[0], typeof(Point));
            Assert.AreEqual(new Position(20, 20), (geometryCollection.Geometries[0] as Point).Position);

            Assert.AreEqual("hello", ((NamedCrs)geometryCollection.Crs).Name);
            Assert.AreEqual(1, geometryCollection.AdditionalProperties.Count);
            Assert.AreEqual(1L, geometryCollection.AdditionalProperties["extra"]);

            Geometry geom = JsonConvert.DeserializeObject<Geometry>(json);
            Assert.AreEqual(GeometryType.GeometryCollection, geom.Type);

            Assert.AreEqual(geom, geometryCollection);

            string json1 = JsonConvert.SerializeObject(geometryCollection);
            Geometry geom1 = JsonConvert.DeserializeObject<Geometry>(json1);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionEqualsHashCode()
        {
            GeometryCollection geometryCollection1 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            GeometryCollection geometryCollection2 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            GeometryCollection geometryCollection3 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 41) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            GeometryCollection geometryCollection4 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            GeometryCollection geometryCollection5 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    Crs = Crs.Named("SomeCrs")
                });

            GeometryCollection geometryCollection6 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs1")
                });

            Assert.AreEqual(geometryCollection1, geometryCollection2);
            Assert.AreEqual(geometryCollection1.GetHashCode(), geometryCollection2.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection3);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection3.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection4);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection4.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection5);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection5.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection6);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor arguments.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestGeometryCollectionConstructorNullException()
        {
            new GeometryCollection(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionConstructors()
        {
            GeometryCollection geometryCollection = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Assert.AreEqual(new Point(20, 30), geometryCollection.Geometries[0]);
            Assert.AreEqual(new Point(30, 40), geometryCollection.Geometries[1]);

            Assert.AreEqual(new Position(0, 0), geometryCollection.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), geometryCollection.BoundingBox.Max);
            Assert.AreEqual("b", geometryCollection.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)geometryCollection.Crs).Name);
        }
    }
}