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
    /// Tests <see cref="MultiLineString"/> class and serialization.
    /// </summary>
    [TestClass]
    public class MultiLineStringTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestMultiLineStringSerialization()
        {
            string json =
                @"{
                   ""type"":""MultiLineString"",
                   ""coordinates"":[[[20,30], [21,31]], [[40,50], [21,32]]],
                   ""bbox"":[20, 20, 30, 30],
                   ""extra"":1,
                   ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}
                  }";
            MultiLineString multiLineString = JsonConvert.DeserializeObject<MultiLineString>(json);

            Assert.AreEqual(2, multiLineString.LineStrings.Count);
            Assert.AreEqual(new Position(20, 30), multiLineString.LineStrings[0].Positions[0]);
            Assert.AreEqual(new Position(21, 32), multiLineString.LineStrings[1].Positions[1]);

            Assert.AreEqual(new Position(20, 20), multiLineString.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), multiLineString.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)multiLineString.Crs).Name);
            Assert.AreEqual(1, multiLineString.AdditionalProperties.Count);
            Assert.AreEqual(1L, multiLineString.AdditionalProperties["extra"]);

            Geometry geom = JsonConvert.DeserializeObject<Geometry>(json);
            Assert.AreEqual(GeometryType.MultiLineString, geom.Type);

            Assert.AreEqual(geom, multiLineString);

            string json1 = JsonConvert.SerializeObject(multiLineString);
            Geometry geom1 = JsonConvert.DeserializeObject<Geometry>(json1);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestMultiLineStringEqualsHashCode()
        {
            MultiLineString multiLineString1 = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            MultiLineString multiLineString2 = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            MultiLineString multiLineString3 = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 41) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            MultiLineString multiLineString4 = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            MultiLineString multiLineString5 = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    Crs = Crs.Named("SomeCrs")
                });

            MultiLineString multiLineString6 = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs1")
                });

            Assert.AreEqual(multiLineString1, multiLineString2);
            Assert.AreEqual(multiLineString1.GetHashCode(), multiLineString2.GetHashCode());

            Assert.AreNotEqual(multiLineString1, multiLineString3);
            Assert.AreNotEqual(multiLineString1.GetHashCode(), multiLineString3.GetHashCode());

            Assert.AreNotEqual(multiLineString1, multiLineString4);
            Assert.AreNotEqual(multiLineString1.GetHashCode(), multiLineString4.GetHashCode());

            Assert.AreNotEqual(multiLineString1, multiLineString5);
            Assert.AreNotEqual(multiLineString1.GetHashCode(), multiLineString5.GetHashCode());

            Assert.AreNotEqual(multiLineString1, multiLineString6);
            Assert.AreNotEqual(multiLineString1.GetHashCode(), multiLineString6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMultiLineStringConstructorNullException()
        {
            new MultiLineString(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestMultiLineStringConstructors()
        {
            MultiLineString multiLineString = new MultiLineString(
                new[]
                    {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                    },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Assert.AreEqual(new Position(20, 30), multiLineString.LineStrings[0].Positions[0]);

            Assert.AreEqual(new Position(0, 0), multiLineString.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), multiLineString.BoundingBox.Max);
            Assert.AreEqual("b", multiLineString.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)multiLineString.Crs).Name);
        }
    }
}