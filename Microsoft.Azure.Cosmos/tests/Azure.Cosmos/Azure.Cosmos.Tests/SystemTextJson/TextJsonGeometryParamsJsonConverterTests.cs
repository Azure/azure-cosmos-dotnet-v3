//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="CancellationToken"/>  scenarios.
    /// </summary>
    [TestClass]
    public class TextJsonGeometryParamsJsonConverterTests
    {
        [TestMethod]
        public void SerializationTest()
        {
            GeometryParams geometryParams = new GeometryParams();
            geometryParams.AdditionalProperties = new Dictionary<string, object>();
            geometryParams.AdditionalProperties.Add("test", 1);
            geometryParams.AdditionalProperties.Add("test2", "2");
            geometryParams.AdditionalProperties.Add("test3", null);
            geometryParams.AdditionalProperties.Add("test4", 1.5);
            geometryParams.BoundingBox = new BoundingBox(new Position(1, 2), new Position(3, 4));
            geometryParams.Crs = new NamedCrs("test");

            string serialized = JsonSerializer.Serialize(geometryParams);
            GeometryParams geometryParamsDeserialized = JsonSerializer.Deserialize<GeometryParams>(serialized);

            Assert.AreEqual(geometryParams.Crs.Type, geometryParamsDeserialized.Crs.Type);
            Assert.AreEqual(geometryParams.BoundingBox.Min.Coordinates[0], geometryParamsDeserialized.BoundingBox.Min.Coordinates[0]);
            Assert.AreEqual(geometryParams.BoundingBox.Min.Coordinates[1], geometryParamsDeserialized.BoundingBox.Min.Coordinates[1]);
            Assert.AreEqual(geometryParams.BoundingBox.Max.Coordinates[0], geometryParamsDeserialized.BoundingBox.Max.Coordinates[0]);
            Assert.AreEqual(geometryParams.BoundingBox.Max.Coordinates[1], geometryParamsDeserialized.BoundingBox.Max.Coordinates[1]);
            Assert.AreEqual(geometryParams.AdditionalProperties["test"], geometryParamsDeserialized.AdditionalProperties["test"]);
            Assert.AreEqual(geometryParams.AdditionalProperties["test2"], geometryParamsDeserialized.AdditionalProperties["test2"]);
            Assert.AreEqual(geometryParams.AdditionalProperties["test3"], geometryParamsDeserialized.AdditionalProperties["test3"]);
            Assert.AreEqual(geometryParams.AdditionalProperties["test4"], geometryParamsDeserialized.AdditionalProperties["test4"]);
        }
    }
}
