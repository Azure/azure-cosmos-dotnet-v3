//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="MultiPolygon"/> class and serialization.
    /// </summary>
    [TestClass]
    public class MultiPolygonTest : CommonSerializationTest
    {
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
                    ""bbox"":[20, 20, 30, 30]
            }";

            MultiPolygon multiPolygon = JsonSerializer.Deserialize<MultiPolygon>(json, this.restContractOptions);

            Assert.AreEqual(2, multiPolygon.Coordinates.Count);
            Assert.AreEqual(2, multiPolygon.Coordinates[0].Count);

            Assert.AreEqual(new Position(20, 30), multiPolygon.Coordinates[0][0][0]);

            Assert.AreEqual(new Position(20, 20), multiPolygon.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual(new Position(30, 30), multiPolygon.BoundingBox.NortheasertlyPoint);

            Geometry geom = JsonSerializer.Deserialize<Geometry>(json, this.restContractOptions);
            Assert.AreEqual(GeometryType.MultiPolygon, geom.Type);

            Assert.AreEqual(geom, multiPolygon);

            string json1 = JsonSerializer.Serialize(multiPolygon, this.restContractOptions);
            Geometry geom1 = JsonSerializer.Deserialize<Geometry>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestMultiPolygonEqualsHashCode()
        {
            MultiPolygon multiPolygon1 =
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
                    new BoundingBox((0, 0), (40, 40)));

            MultiPolygon multiPolygon2 =
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
                    new BoundingBox((0, 0), (40, 40)));

            MultiPolygon polygon3 =
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
                    new BoundingBox((0, 0), (40, 40)));

            MultiPolygon polygon4 =
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
                    new BoundingBox((0, 0), (40, 40)));

            MultiPolygon polygon5 =
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
                    new BoundingBox((0, 0), (40, 41)));

            MultiPolygon polygon6 =
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
                    new BoundingBox((0, 0), (40, 40)));

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
            MultiPolygon multiPolygon =
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
                    new BoundingBox((0, 0), (40, 40)));

            Assert.AreEqual(new Position(20, 20), multiPolygon.Coordinates[0][0][0]);

            Assert.AreEqual(new Position(0, 0), multiPolygon.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual(new Position(40, 40), multiPolygon.BoundingBox.NortheasertlyPoint);
        }
    }
}
