namespace Microsoft.Azure.Cosmos.Test.Spatial
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Point = Cosmos.Spatial.Point;

    /// <summary>
    /// Spatial STJ serialization/deserialization tests
    /// </summary>
    [TestClass]
    public class STJSpatialTest
    {

        /// <summary>
        /// Tests serialization/deserialization of BoundingBox class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(BoundingBoxData))]
        public void TestBoundingBoxSerialization(BoundingBox input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            BoundingBox newtonsoftResult = JsonConvert.DeserializeObject<BoundingBox>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            BoundingBox stjResult = System.Text.Json.JsonSerializer.Deserialize<BoundingBox>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(stjJson, newtonsoftJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of Position class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(PositionData))]
        public void TestPositionSerialization(Position input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            Position newtonsoftResult = JsonConvert.DeserializeObject<Position>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            Position stjResult = System.Text.Json.JsonSerializer.Deserialize<Position>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of Crs class and its variants.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(CrsData))]
        public void TestCrsSerialization(Crs input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            Crs newtonsoftResult = JsonConvert.DeserializeObject<Crs>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            Crs stjResult = System.Text.Json.JsonSerializer.Deserialize<Crs>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of LinearRing class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(LinearRingData))]
        public void TestLinearRingSerialization(LinearRing input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            LinearRing newtonsoftResult = JsonConvert.DeserializeObject<LinearRing>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            LinearRing stjResult = System.Text.Json.JsonSerializer.Deserialize<LinearRing>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of Point class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(PointData))]
        public void TestPointSerialization(Point input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            Point newtonsoftResult = JsonConvert.DeserializeObject<Point>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            Point stjResult = System.Text.Json.JsonSerializer.Deserialize<Point>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
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

            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            MultiPoint newtonsoftResult = JsonConvert.DeserializeObject<MultiPoint>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            MultiPoint stjResult = System.Text.Json.JsonSerializer.Deserialize<MultiPoint>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of LineString class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(LineStringData))]
        public void TestLineStringSerialization(LineString input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            LineString newtonsoftResult = JsonConvert.DeserializeObject<LineString>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            LineString stjResult = System.Text.Json.JsonSerializer.Deserialize<LineString>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
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

            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            MultiLineString newtonsoftResult = JsonConvert.DeserializeObject<MultiLineString>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            MultiLineString stjResult = System.Text.Json.JsonSerializer.Deserialize<MultiLineString>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of Polygon class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(PolygonData))]
        public void TestPolygonSerialization(Polygon input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            Polygon newtonsoftResult = JsonConvert.DeserializeObject<Polygon>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            Polygon stjResult = System.Text.Json.JsonSerializer.Deserialize<Polygon>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of MultiPolygon class.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(MultiPolygonData))]
        public void TestMultiPolygonSerialization(MultiPolygon input)
        {
            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            MultiPolygon newtonsoftResult = JsonConvert.DeserializeObject<MultiPolygon>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            MultiPolygon stjResult = System.Text.Json.JsonSerializer.Deserialize<MultiPolygon>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of a GeometryCollection with mixed types.
        /// </summary>
        [TestMethod]
        public void TestGeometricCollectionMixedTypeSerialization()
        {
            GeometryCollection input = new GeometryCollection(
                 new Geometry[]
                 {
                     new Point(20, 30),
                     new Polygon(new[]
                     {
                         new LinearRing(new[]
                         {
                             new Position(40, 40),
                             new Position(45, 40),
                             new Position(45, 45),
                             new Position(40, 45),
                             new Position(40, 40)
                         })
                     })
                 },
                 new GeometryParams
                 {
                     BoundingBox = new BoundingBox(new Position(0, 0), new Position(50, 50)),
                     Crs = Crs.Named("SomeCrs"),
                     AdditionalProperties = new Dictionary<string, object>
                     {
                         ["one"] = "a large one",
                         ["two"] = "a new two"
                     },
                 });


            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            GeometryCollection newtonsoftResult = JsonConvert.DeserializeObject<GeometryCollection>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            GeometryCollection stjResult = System.Text.Json.JsonSerializer.Deserialize<GeometryCollection>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of GeometryCollection class with Point geometries.
        /// </summary>
        [TestMethod]
        public void TestGeometricCollectionPointSerialization()
        {
            GeometryCollection input = new GeometryCollection(
                 new[] { new Point(20, 30), new Point(30, 40) },
                 new GeometryParams
                 {
                     BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                     Crs = Crs.Named("SomeCrs"),
                     AdditionalProperties = new Dictionary<string, object>
                     {
                         ["one"] = "a large one",
                         ["two"] = "a new two"
                     },
                 });


            // Newtonsoft
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            GeometryCollection newtonsoftResult = JsonConvert.DeserializeObject<GeometryCollection>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult);

            // STJ
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            GeometryCollection stjResult = System.Text.Json.JsonSerializer.Deserialize<GeometryCollection>(stjJson);
            Assert.AreEqual(input, stjResult);

            Assert.AreEqual(newtonsoftJson, stjJson);
        }

        /// <summary>
        /// Tests serialization/deserialization of GeometryCollection containing all geometry types.
        /// Ensures comprehensive coverage of GeometryCollection with Point, LineString, Polygon, 
        /// MultiPoint, MultiLineString, and MultiPolygon.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionWithAllGeometryTypes()
        {
            // Create a comprehensive GeometryCollection with all supported geometry types
            GeometryCollection input = new GeometryCollection(
                new Geometry[]
                {
                    // Point
                    new Point(new Position(10, 20)),
                    
                    // LineString
                    new LineString(new[] 
                    { 
                        new Position(20, 30), 
                        new Position(30, 40) 
                    }),
                    
                    // Polygon
                    new Polygon(new[]
                    {
                        new LinearRing(new[]
                        {
                            new Position(0, 0),
                            new Position(0, 10),
                            new Position(10, 10),
                            new Position(10, 0),
                            new Position(0, 0)
                        })
                    }),
                    
                    // MultiPoint
                    new MultiPoint(new[] 
                    { 
                        new Position(5, 5), 
                        new Position(15, 15) 
                    }),
                    
                    // MultiLineString
                    new MultiLineString(new[]
                    {
                        new LineStringCoordinates(new[] 
                        { 
                            new Position(25, 25), 
                            new Position(35, 35) 
                        })
                    }),
                    
                    // MultiPolygon
                    new MultiPolygon(new[]
                    {
                        new PolygonCoordinates(new[]
                        {
                            new LinearRing(new[]
                            {
                                new Position(40, 40),
                                new Position(40, 50),
                                new Position(50, 50),
                                new Position(50, 40),
                                new Position(40, 40)
                            })
                        })
                    })
                },
                new GeometryParams
                {
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(50, 50)),
                    Crs = Crs.Named("EPSG:4326"),
                    AdditionalProperties = new Dictionary<string, object>
                    {
                        ["description"] = "Comprehensive geometry collection",
                        ["version"] = 1
                    }
                });

            // Newtonsoft serialization/deserialization
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            GeometryCollection newtonsoftResult = JsonConvert.DeserializeObject<GeometryCollection>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult, "Newtonsoft deserialized result should match input");

            // STJ serialization/deserialization
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            GeometryCollection stjResult = System.Text.Json.JsonSerializer.Deserialize<GeometryCollection>(stjJson);
            Assert.AreEqual(input, stjResult, "STJ deserialized result should match input");

            // Ensure both serializers produce identical JSON output
            Assert.AreEqual(newtonsoftJson, stjJson, "STJ and Newtonsoft should produce identical JSON");
        }

        /// <summary>
        /// Tests serialization/deserialization of nested GeometryCollections.
        /// Ensures that GeometryCollection can contain other GeometryCollections.
        /// </summary>
        [TestMethod]
        public void TestNestedGeometryCollections()
        {
            // Create nested GeometryCollections
            GeometryCollection innerCollection1 = new GeometryCollection(
                new Geometry[]
                {
                    new Point(10, 20),
                    new Point(30, 40)
                });

            GeometryCollection innerCollection2 = new GeometryCollection(
                new Geometry[]
                {
                    new LineString(new[] 
                    { 
                        new Position(50, 60), 
                        new Position(70, 80) 
                    })
                });

            GeometryCollection outerCollection = new GeometryCollection(
                new Geometry[]
                {
                    innerCollection1,
                    innerCollection2,
                    new Point(90, 100)
                },
                new GeometryParams
                {
                    Crs = Crs.Named("SomeCrs")
                });

            // Newtonsoft serialization/deserialization
            string newtonsoftJson = JsonConvert.SerializeObject(outerCollection);
            GeometryCollection newtonsoftResult = JsonConvert.DeserializeObject<GeometryCollection>(newtonsoftJson);
            Assert.AreEqual(outerCollection, newtonsoftResult, "Newtonsoft deserialized nested collection should match input");

            // STJ serialization/deserialization
            string stjJson = System.Text.Json.JsonSerializer.Serialize(outerCollection);
            GeometryCollection stjResult = System.Text.Json.JsonSerializer.Deserialize<GeometryCollection>(stjJson);
            Assert.AreEqual(outerCollection, stjResult, "STJ deserialized nested collection should match input");

            // Ensure both serializers produce identical JSON output
            Assert.AreEqual(newtonsoftJson, stjJson, "STJ and Newtonsoft should produce identical JSON for nested collections");
        }

        /// <summary>
        /// Tests serialization/deserialization of GeometryCollection with complex coordinates.
        /// Ensures proper handling of floating-point precision and 3D coordinates.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionWithComplexCoordinates()
        {
            GeometryCollection input = new GeometryCollection(
                new Geometry[]
                {
                    new Point(new Position(20.123456, 30.654321, 100.5)),
                    new LineString(new[]
                    {
                        new Position(-122.419, 37.775, 0),
                        new Position(-122.420, 37.776, 50.25)
                    }),
                    new Polygon(new[]
                    {
                        new LinearRing(new[]
                        {
                            new Position(0.1, 0.2),
                            new Position(0.1, 0.3),
                            new Position(0.2, 0.3),
                            new Position(0.2, 0.2),
                            new Position(0.1, 0.2)
                        })
                    })
                },
                new GeometryParams
                {
                    BoundingBox = new BoundingBox(
                        new Position(-122.420, 37.775, 0), 
                        new Position(20.123456, 37.776, 100.5)),
                    AdditionalProperties = new Dictionary<string, object>
                    {
                        ["precision"] = "high",
                        ["elevation"] = true
                    }
                });

            // Newtonsoft serialization/deserialization
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            GeometryCollection newtonsoftResult = JsonConvert.DeserializeObject<GeometryCollection>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult, "Newtonsoft should handle complex coordinates");

            // STJ serialization/deserialization
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            GeometryCollection stjResult = System.Text.Json.JsonSerializer.Deserialize<GeometryCollection>(stjJson);
            Assert.AreEqual(input, stjResult, "STJ should handle complex coordinates");

            // Ensure both serializers produce identical JSON output
            Assert.AreEqual(newtonsoftJson, stjJson, "Both serializers should produce identical JSON for complex coordinates");
        }

        /// <summary>
        /// Tests serialization/deserialization of GeometryCollection without optional properties.
        /// Ensures proper handling when BoundingBox, Crs, and AdditionalProperties are null.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionWithoutOptionalProperties()
        {
            // Create a minimal GeometryCollection without optional properties
            GeometryCollection input = new GeometryCollection(
                new Geometry[]
                {
                    new Point(10, 20),
                    new LineString(new[] { new Position(30, 40), new Position(50, 60) })
                });

            // Newtonsoft serialization/deserialization
            string newtonsoftJson = JsonConvert.SerializeObject(input);
            GeometryCollection newtonsoftResult = JsonConvert.DeserializeObject<GeometryCollection>(newtonsoftJson);
            Assert.AreEqual(input, newtonsoftResult, "Newtonsoft should handle GeometryCollection without optional properties");

            // STJ serialization/deserialization
            string stjJson = System.Text.Json.JsonSerializer.Serialize(input);
            GeometryCollection stjResult = System.Text.Json.JsonSerializer.Deserialize<GeometryCollection>(stjJson);
            Assert.AreEqual(input, stjResult, "STJ should handle GeometryCollection without optional properties");

            // Ensure both serializers produce identical JSON output
            Assert.AreEqual(newtonsoftJson, stjJson, "Both serializers should produce identical JSON without optional properties");
        }

        public static IEnumerable<object[]> PointData => new[]
        {
              new object[] { 
                  new Point(new Position(20.4, 30.6))
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
                        Crs = Crs.Named("EPSG:4326") 
                    })
              }
        };

        public static IEnumerable<object[]> LineStringData => new[]
        {
              new object[] {
                  new LineString(
                    new[] { new Position(20.30, 30), new Position(30, 40.0) }
                )
              },
              new object[] {
                 new LineString(
                    new[] { new Position(20, 30), new Position(30, 40) },
                    new GeometryParams
                    {
                        BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                         AdditionalProperties = new Dictionary<string, object> {
                            ["hello"] = 1.2,
                            ["world"] = 3.4
                        },
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
                             Crs = Crs.Named("SomeCrs"),
                              AdditionalProperties = new Dictionary<string, object> {
                                    ["hello"] = 1.2,
                                    ["world"] = 3.4
                            },
                         })
              },
        };
        public static IEnumerable<object[]> BoundingBoxData => new[]
       {
            new object[]
            {
                new BoundingBox(new Position(0, 0), new Position(40, 40))
            },
            new object[]
            {
                new BoundingBox(new Position(0.43, 0.4), new Position(40.53, 40.5))
            },
            new object[]
            {
                new BoundingBox(new Position(-10, 20.5), new Position(30.5, -40))
            },
        };
        public static IEnumerable<object[]> PositionData => new[]
        {
            new object[]
            {
                new Position(10, 20)
            },
            new object[]
            {
                new Position(15.5, 25.5)
            },
            new object[]
            {
                new Position(30, 40, 50)
            },
            new object[]
            {
                new Position(35.5, 45.5, 55.5)
            },
            new object[]
            {
                new Position(-10.1, -20.2, -30.3)
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

        public static IEnumerable<object[]> LinearRingData => new[]
       {
            new object[]
            {
                new LinearRing(new[]
                {
                    new Position(10, 20),
                    new Position(20, 30),
                    new Position(30, 20),
                    new Position(10, 20)
                })
            },
            new object[]
            {
                new LinearRing(new[]
                {
                    new Position(10.1, 20.2),
                    new Position(20.2, 30.3, 5.0),
                    new Position(30.3, 20.2),
                    new Position(10.1, 20.2)
                })
            },
        };

        public static IEnumerable<object[]> CrsData => new[]
       {
            new object[] { Crs.Default },
            new object[] { Crs.Unspecified },
            new object[] { Crs.Named("EPSG:4326") },
            new object[] { Crs.Linked("http://example.com/crs/1") },
            new object[] { Crs.Linked("http://example.com/crs/2", "custom-type") },
        };





    }
}
