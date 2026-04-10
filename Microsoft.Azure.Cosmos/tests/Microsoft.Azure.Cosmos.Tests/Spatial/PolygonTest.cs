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
    /// Tests <see cref="Polygon"/> class and serialization.
    /// </summary>
    [TestClass]
    public class PolygonTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestPolygonSerialization()
        {
            string json = @"{
                    ""type"":""Polygon"",
                    ""coordinates"":[[[20,30], [30,30], [30,20], [20,20], [20, 30]], [[25,28], [25,25], [25, 28], [28,28], [25, 28]]],
                    ""bbox"":[20, 20, 30, 30],
                    ""extra"":1,
                    ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}}";

            Polygon polygon = JsonConvert.DeserializeObject<Polygon>(json);

            Assert.AreEqual(2, polygon.Rings.Count);
            Assert.AreEqual(5, polygon.Rings[0].Positions.Count);
            Assert.AreEqual(new Position(20, 30), polygon.Rings[0].Positions[0]);
            Assert.AreEqual(new Position(30, 20), polygon.Rings[0].Positions[2]);

            Assert.AreEqual(new Position(20, 20), polygon.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), polygon.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)polygon.Crs).Name);
            Assert.AreEqual(1, polygon.AdditionalProperties.Count);
            Assert.AreEqual(1L, polygon.AdditionalProperties["extra"]);

            Geometry geom = JsonConvert.DeserializeObject<Geometry>(json);
            Assert.AreEqual(GeometryType.Polygon, geom.Type);

            Assert.AreEqual(geom, polygon);

            string json1 = JsonConvert.SerializeObject(polygon);
            Geometry geom1 = JsonConvert.DeserializeObject<Geometry>(json1);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestPolygonEqualsHashCode()
        {
            Polygon polygon1 =
                new Polygon(
                    new[]
                        {
                            new LinearRing(
                                new[]
                                    {
                                        new Position(20, 20),
                                        new Position(20, 21),
                                        new Position(21, 21),
                                        new Position(21, 20),
                                        new Position(22, 20)
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });

            Polygon polygon2 = new Polygon(
                new[]
                    {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Polygon polygon3 = new Polygon(
                new[]
                    {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 22),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Polygon polygon4 = new Polygon(
                new[]
                    {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Polygon polygon5 = new Polygon(
                new[]
                    {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    Crs = Crs.Named("SomeCrs")
                });

            Polygon polygon6 = new Polygon(
                new[]
                    {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs1")
                });

            Assert.AreEqual(polygon1, polygon2);
            Assert.AreEqual(polygon1.GetHashCode(), polygon2.GetHashCode());

            Assert.AreNotEqual(polygon1, polygon3);
            Assert.AreNotEqual(polygon1.GetHashCode(), polygon3.GetHashCode());

            Assert.AreNotEqual(polygon1, polygon4);
            Assert.AreNotEqual(polygon1.GetHashCode(), polygon4.GetHashCode());

            Assert.AreNotEqual(polygon1, polygon5);
            Assert.AreNotEqual(polygon1.GetHashCode(), polygon5.GetHashCode());

            Assert.AreNotEqual(polygon1, polygon6);
            Assert.AreNotEqual(polygon1.GetHashCode(), polygon6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestPolygonConstructorNullException()
        {
            new Polygon((IList<Position>)null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestPolygonConstructors()
        {
            Polygon polygon = new Polygon(
                new[]
                    {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Assert.AreEqual(new Position(20, 20), polygon.Rings[0].Positions[0]);

            Assert.AreEqual(new Position(0, 0), polygon.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), polygon.BoundingBox.Max);
            Assert.AreEqual("b", polygon.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)polygon.Crs).Name);
        }
    }
}