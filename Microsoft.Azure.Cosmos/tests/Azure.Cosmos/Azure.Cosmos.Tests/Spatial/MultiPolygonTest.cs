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
    /// Tests <see cref="MultiPolygon"/> class and serialization.
    /// </summary>
    [TestClass]
    public class MultiPolygonTest
    {
        private JsonSerializerOptions restContractOptions;
        public MultiPolygonTest()
        {
            this.restContractOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeRESTConverters(this.restContractOptions);
        }

        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestMultiPolygonSerialization()
        {
            string json = @"{
                    ""type"":""MultiPolygon"",
                    ""coordinates"":[
                        [[[20,30], [30,30], [30,20], [20,20], [20, 30]], [[25,28], [25,25], [25, 28], [28,28], [25, 28]]],
                        [[[0,0], [0, 1], [1, 0], [0, 0], [0, 10]]]
                    ],
                    ""bbox"":[20, 20, 30, 30],
                    ""extra"":1,
                    ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}}";

            var multiPolygon = JsonSerializer.Deserialize<MultiPolygon>(json, this.restContractOptions);

            Assert.AreEqual(2, multiPolygon.Polygons.Count);
            Assert.AreEqual(2, multiPolygon.Polygons[0].Rings.Count);

            Assert.AreEqual(new Position(20, 30), multiPolygon.Polygons[0].Rings[0].Positions[0]);

            Assert.AreEqual(new Position(20, 20), multiPolygon.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), multiPolygon.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)multiPolygon.Crs).Name);
            Assert.AreEqual(1, multiPolygon.AdditionalProperties.Count);
            Assert.AreEqual(1L, multiPolygon.AdditionalProperties["extra"]);

            var geom = JsonSerializer.Deserialize<Geometry>(json, this.restContractOptions);
            Assert.AreEqual(GeometryType.MultiPolygon, geom.Type);

            Assert.AreEqual(geom, multiPolygon);

            string json1 = JsonSerializer.Serialize(multiPolygon, this.restContractOptions);
            var geom1 = JsonSerializer.Deserialize<Geometry>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestMultiPolygonEqualsHashCode()
        {
            var multiPolygon1 =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });

            var multiPolygon2 =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });

            var polygon3 =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 21), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });

            var polygon4 =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });

            var polygon5 =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                        Crs = Crs.Named("SomeCrs")
                    });

            var polygon6 =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs1")
                    });

            Assert.AreEqual(multiPolygon1, multiPolygon2);
            Assert.AreEqual(multiPolygon1.GetHashCode(), multiPolygon2.GetHashCode());

            Assert.AreNotEqual(multiPolygon1, polygon3);
            Assert.AreNotEqual(multiPolygon1.GetHashCode(), polygon3.GetHashCode());

            Assert.AreNotEqual(multiPolygon1, polygon4);
            Assert.AreNotEqual(multiPolygon1.GetHashCode(), polygon4.GetHashCode());

            Assert.AreNotEqual(multiPolygon1, polygon5);
            Assert.AreNotEqual(multiPolygon1.GetHashCode(), polygon5.GetHashCode());

            Assert.AreNotEqual(multiPolygon1, polygon6);
            Assert.AreNotEqual(multiPolygon1.GetHashCode(), polygon6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMultiPolygonConstructorNullException()
        {
            new MultiPolygon(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestMultiPolygonConstructors()
        {
            var multiPolygon =
                new MultiPolygon(
                    new[]
                        {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
                        },
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });

            Assert.AreEqual(new Position(20, 20), multiPolygon.Polygons[0].Rings[0].Positions[0]);

            Assert.AreEqual(new Position(0, 0), multiPolygon.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), multiPolygon.BoundingBox.Max);
            Assert.AreEqual("b", multiPolygon.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)multiPolygon.Crs).Name);
        }
    }
}
