namespace Microsoft.Azure.Cosmos.Test.Spatial
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Point = Cosmos.Spatial.Point;

    /// <summary>
    /// Spatial STJ serialization/deserialization tests
    /// </summary>
    [TestClass]
    public class STJSpatialTest
    {
        /// <summary>
        /// Tests serialization/deserialization of Point class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(PointData))]
        public void TestPointSerialization(Point input)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                Converters =
                {
                    new DictionarySTJConverter()
                }
            };
            string json = JsonSerializer.Serialize(input, options);
            Point result = JsonSerializer.Deserialize<Point>(json, options);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// Tests serialization/deserialization of MultiPoint class.
        /// </summary>
        [TestMethod]
        public void TestMultiPointSerialization()
        {

            MultiPoint input = new MultiPoint(
                    new[] { new Position(20, 30), new Position(30, 40) },
                    new GeometryParams
                    {
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    });
                    
            string json = JsonSerializer.Serialize(input);
            MultiPoint result = JsonSerializer.Deserialize<MultiPoint>(json);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// Tests serialization/deserialization of LineString class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(LineStringData))]
        public void TestLineStringSerialization(LineString input)
        {

            string json = JsonSerializer.Serialize(input);
            LineString result = JsonSerializer.Deserialize<LineString>(json);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// Tests serialization/deserialization of MultiLineString class.
        /// </summary>
        [TestMethod]
        public void TestMultiLineStringSerialization()
        {
            MultiLineString input = new MultiLineString(
               new[]
                   {
                        new LineStringCoordinates(new[] { new Position(20, 30), new Position(30, 40) }),
                        new LineStringCoordinates(new[] { new Position(40, 50), new Position(60, 60) })
                   },
               new GeometryParams
               {
                   BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                   Crs = Crs.Named("SomeCrs")
               });

            string json = JsonSerializer.Serialize(input);
            MultiLineString result = JsonSerializer.Deserialize<MultiLineString>(json);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// Tests serialization/deserialization of Polygon class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(PolygonData))]
        public void TestPolygonSerialization(Polygon input)
        {

            string json = JsonSerializer.Serialize(input);
            Polygon result = JsonSerializer.Deserialize<Polygon>(json);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// Tests serialization/deserialization of MultiPolygon class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(MultiPolygonData))]
        public void TestMultiPolygonSerialization(MultiPolygon input)
        {

            string json = JsonSerializer.Serialize(input);
            MultiPolygon result = JsonSerializer.Deserialize<MultiPolygon>(json);
            Assert.AreEqual(input, result);
        }

        /// <summary>
        /// Tests serialization/deserialization of GeometryCollection class.
        /// </summary>
        [TestMethod]
        public void TestGeometricCollectionSerialization()
        {
            GeometryCollection input = new GeometryCollection(
                 new[] { new Point(20, 30), new Point(30, 40) },
                 new GeometryParams
                 {
                     BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                     Crs = Crs.Named("SomeCrs")
                 });
                

            string json = JsonSerializer.Serialize(input);
            GeometryCollection result = JsonSerializer.Deserialize<GeometryCollection>(json);
            Assert.AreEqual(input, result);
        }

        public static IEnumerable<object[]> PointData => new[]
        {
              new object[] { 
                  new Point(new Position(20.4, 30.6))
              },
              new object[] {
                  new Point(
                    new Position(20.4, 30.1),
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> {
                            ["one"] = 1,
                            ["two"] = 2
                        },
                    })
              },
              new object[] {
                  new Point(
                    new Position(20.2, 30.9),
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> {
                            ["one"] = 1.2,
                            ["two"] = 3.4
                        },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = new UnspecifiedCrs()
                    })
              },
              new object[] {
                  new Point(
                    new Position(20.5, 30.4),
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> {
                            ["one"] = "a large one",
                            ["two"] = "a new two"
                        },
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    })
              },
              new object[] {
                  new Point(
                    new Position(20, 30),
                    new GeometryParams
                    {
                        AdditionalProperties = new Dictionary<string, object> {
                            ["one"] = "a large one",
                            ["two"] = "a new two"
                        },
                        Crs = new UnspecifiedCrs()
                    })
              }


        };

        public static IEnumerable<object[]> LineStringData => new[]
        {
              new object[] {
                  new LineString(
                    new[] { new Position(20, 30), new Position(30, 40) }
                )
              },
              new object[] {
                 new LineString(
                    new[] { new Position(20, 30), new Position(30, 40) },
                    new GeometryParams
                    {
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    })
              },
              new object[] {
                 new LineString(
                    new[] { new Position(20, 30), new Position(30, 40) },
                    new GeometryParams
                    {
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                        Crs = Crs.Linked("http://foo.com", "link")
                    })
              },

        };

        public static IEnumerable<object[]> PolygonData => new[]
       {
              new object[] {
                  new Polygon(
                     new[]{
                        new LinearRing(
                            new[]{
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                })
              },
              new object[] {
                  new Polygon(
                         new[]{
                         new LinearRing(
                            new[]{
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                         },
                         new GeometryParams
                         {
                             BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                             Crs = Crs.Named("SomeCrs")
                         })
              },
               new object[] {
                  new Polygon(
                         new[]{
                         new LinearRing(
                            new[]{
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
                         },
                         new GeometryParams
                         {
                             Crs = Crs.Named("SomeCrs")
                         })
              },
        };

        public static IEnumerable<object[]> MultiPolygonData => new[]
        {
              new object[] {
                        new MultiPolygon(
                        new[]{
                            new PolygonCoordinates(
                                new[]{
                                    new LinearRing(
                                        new[]
                                            {
                                                new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                new Position(21, 20), new Position(20, 20)
                                            })
                                })
                        })
              },

              new object[] {
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
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                        Crs = Crs.Named("SomeCrs")
                    })
              },

        };





    }
}
