//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Text;
    using System.Text.Json;
    using global::Azure.Core.GeoJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Serialization tests applying to all geometry types.
    /// </summary>
    [TestClass]
    public class CommonSerializationTest
    {
        /// <summary>
        /// Tests that incorrect JSON throws exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException), AllowDerivedTypes = true)]
        public void TestInvalidJson()
        {
            string json = @"{""type"":""Poi}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests that coordinates cannot be null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestNullCoordinates()
        {
            string json = @"{""type"":""Point"",""coordinates"":null}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests that type cannot be null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void TestNullType()
        {
            string json = @"{""type"":null, ""coordinates"":[20, 30]}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests that type cannot be null and uses <see cref="GeoJsonConverter"/>.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void TestNullTypeGeometry()
        {
            string json = @"{""type"":null, ""coordinates"":[20, 30]}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests empty coordinates.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestEmptyCoordinates()
        {
            // This throw a JsonException, but it's a valid Json...
            string json = @"{
                    ""type"":""Point"",
                    ""coordinates"":[]
                    }";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests bounding box with not even number of coordinates.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestBoundingBoxWithNonEvenNumberOfCoordinates()
        {
            // This throw a JsonException, but it's a valid Json...
            string json = @"{""type"":""Point"", ""coordinates"":[20, 30], ""bbox"":[0, 0, 0, 5, 5]}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests bounding box with insufficient number of coordinates.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestBoundingBoxWithNotEnoughCoordinates()
        {
            // This throw a JsonException, but it's a valid Json...
            string json = @"{""type"":""Point"", ""coordinates"":[20, 30], ""bbox"":[0, 0]}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
        }

        /// <summary>
        /// Tests bounding box which is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestNullBoundingBox()
        {
            // The value of the bbox member MUST be an array
            string json = @"{""type"":""Point"", ""coordinates"":[20, 30], ""bbox"":null}";
            GeoPoint geoPoint = ParseGeoObject<GeoPoint>(json);
            Assert.IsNull(geoPoint.BoundingBox);
        }

        private static TGeoObject ParseGeoObject<TGeoObject>(string json)
            where TGeoObject : GeoObject
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            GeoJsonConverter geoJsonConverter = new GeoJsonConverter();
            Utf8JsonReader utf8JsonReader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            TGeoObject geoObject = (TGeoObject)geoJsonConverter.Read(ref utf8JsonReader, typeof(TGeoObject), new JsonSerializerOptions());
            return geoObject;
        }
    }
}
