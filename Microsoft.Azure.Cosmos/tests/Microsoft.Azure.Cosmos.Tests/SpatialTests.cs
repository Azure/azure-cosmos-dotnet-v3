//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class SpatialTests
    {
        [TestMethod]
        public void BoundingBoxSerialization()
        {
            BoundingBox testObj = new BoundingBox(new Position(1, 1), new Position(1, 1));
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void LinearRingSerialization()
        {
            LinearRing testObj = new LinearRing(new List<Position>() { new Position(1, 1) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void LineStringSerialization()
        {
            LineString testObj = new LineString(new List<Position>() { new Position(1, 1) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void LineStringCoordinatesSerialization()
        {
            LineStringCoordinates testObj = new LineStringCoordinates(new List<Position>() { new Position(1, 1) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void LinkedCrsSerialization()
        {
            LinkedCrs testObj = new LinkedCrs("href", "hrefType");
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void MultiLineStringSerialization()
        {
            MultiLineString testObj = new MultiLineString(new List<LineStringCoordinates>());
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void MultiPolygonSerialization()
        {
            MultiPolygon testObj = new MultiPolygon(new List<PolygonCoordinates>() { new PolygonCoordinates(new List<LinearRing>() { new LinearRing(new List<Position>() { new Position(1, 1) }) }) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void MultiPointSerialization()
        {
            MultiPoint testObj = new MultiPoint(new List<Position>() { new Position(1, 1) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void NamedCrsSerialization()
        {
            NamedCrs testObj = new NamedCrs("name");
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void PointSerialization()
        {
            Point testObj = new Point(1, 1);
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void PolygonSerialization()
        {
            Polygon testObj = new Polygon(new List<Position>() { new Position(1, 1) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void PolygonCoordinatesSerialization()
        {
            PolygonCoordinates testObj = new PolygonCoordinates(new List<LinearRing>() { new LinearRing(new List<Position>() { new Position(1, 1) }) });
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void PositionSerialization()
        {
            Position testObj = new Position(1, 1);
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        [TestMethod]
        public void UnspecifiedCrsSerialization()
        {
            UnspecifiedCrs testObj = new UnspecifiedCrs();
            string result = ContractObjectToXml(testObj);
            Assert.IsTrue(VerifySerialization(result, testObj));
        }

        private static string ContractObjectToXml<T>(T obj)
        {
            DataContractSerializer dataContractSerializer = new DataContractSerializer(obj.GetType());
            string text;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                dataContractSerializer.WriteObject(memoryStream, obj);
                byte[] data = new byte[memoryStream.Length];
                Array.Copy(memoryStream.GetBuffer(), data, data.Length);
                text = Encoding.UTF8.GetString(data);
            }

            return text;
        }

        private static bool VerifySerialization<T>(string serialized, T obj)
        {
            DataContractSerializer dataContractSerializer = new DataContractSerializer(obj.GetType());


            using (MemoryStream stream = new MemoryStream())
            {
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(serialized);
                writer.Flush();
                stream.Position = 0;
                T deserialized = (T)dataContractSerializer.ReadObject(stream);
                return deserialized.Equals(obj);
            }
        }
    }
}