//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test.Spatial
{
    using System;

    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Newtonsoft.Json;

    /// <summary>
    /// Tests for <see cref="Crs"/>.
    /// </summary>
    [TestClass]
    public class CrsTest
    {
        /// <summary>
        /// Tests serialization of 'unspecified' CRS.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void UnspecifiedCrsSerialization()
        {
            Crs crs = JsonConvert.DeserializeObject<Crs>(@"null");
            Assert.AreEqual(CrsType.Unspecified, crs.Type);
            Assert.AreEqual(Crs.Unspecified, crs);

            string json = JsonConvert.SerializeObject(crs);
            Crs crs1 = JsonConvert.DeserializeObject<Crs>(json);
            Assert.AreEqual(crs1, crs);
        }

        /// <summary>
        /// Tests construction of linked CRS.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void LinkedCrsConstructor()
        {
            LinkedCrs crs = Crs.Linked("http://foo.com");
            Assert.AreEqual("http://foo.com", crs.Href);
            Assert.IsNull(crs.HrefType);

            crs = Crs.Linked("http://foo.com", "link");
            Assert.AreEqual("http://foo.com", crs.Href);
            Assert.AreEqual("link", crs.HrefType);
        }

        /// <summary>
        /// Tests serialization of linked CRS.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void LinkedCrsSerialization()
        {
            LinkedCrs linkedCrs = (LinkedCrs)JsonConvert.DeserializeObject<Crs>(@"{'type':'link', 'properties':{'href':'http://foo', 'type':'link'}}");
            Assert.AreEqual("http://foo", linkedCrs.Href);
            Assert.AreEqual(CrsType.Linked, linkedCrs.Type);

            string json = JsonConvert.SerializeObject(linkedCrs);
            LinkedCrs linkedCrs1 = (LinkedCrs)JsonConvert.DeserializeObject<Crs>(json);
            Assert.AreEqual(linkedCrs1, linkedCrs);
        }

        /// <summary>
        /// Tests deserialization of linked CRS when HREF is absent.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        [ExpectedException(typeof(JsonSerializationException))]
        public void LinkedCrsSerializationNoHref()
        {
            JsonConvert.DeserializeObject<Crs>(@"{'type':'linked', 'properties':{'href':null}}");
        }

        /// <summary>
        /// Tests equality/hash code of linked CRS.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void LinkedCrsEquals()
        {
            LinkedCrs namedCrs1 = Crs.Linked("AName", "type");
            LinkedCrs namedCrs2 = Crs.Linked("AName", "type");
            LinkedCrs namedCrs3 = Crs.Linked("AnotherName", "type");
            LinkedCrs namedCrs4 = Crs.Linked("AName", "anotherType");
            LinkedCrs namedCrs5 = Crs.Linked("AName");

            Assert.AreEqual(namedCrs1, namedCrs2);
            Assert.AreEqual(namedCrs1.GetHashCode(), namedCrs2.GetHashCode());

            Assert.AreNotEqual(namedCrs1, namedCrs3);
            Assert.AreNotEqual(namedCrs1.GetHashCode(), namedCrs3.GetHashCode());

            Assert.AreNotEqual(namedCrs1, namedCrs4);
            Assert.AreNotEqual(namedCrs1.GetHashCode(), namedCrs4.GetHashCode());

            Assert.AreNotEqual(namedCrs1, namedCrs5);
            Assert.AreNotEqual(namedCrs1.GetHashCode(), namedCrs5.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions of linked CRS.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void LinkedCrsConstructorException()
        {
            Crs.Linked(null);
        }

        /// <summary>
        /// Tests named CRS construction.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void NamedCrsConstructor()
        {
            NamedCrs namedCrs = Crs.Named("NamedCrs");
            Assert.AreEqual("NamedCrs", namedCrs.Name);
        }

        /// <summary>
        /// Tests named CRS serialization.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void NamedCrsSerialization()
        {
            NamedCrs namedCrs = (NamedCrs)JsonConvert.DeserializeObject<Crs>(@"{'type':'name', 'properties':{'name':'AName'}}");
            Assert.AreEqual("AName", namedCrs.Name);

            string json = JsonConvert.SerializeObject(namedCrs);
            NamedCrs namedCrs1 = (NamedCrs)JsonConvert.DeserializeObject<Crs>(json);
            Assert.AreEqual(namedCrs1, namedCrs);
        }

        /// <summary>
        /// Tests named CRS equality and hash code.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        public void NamedCrsEquals()
        {
            NamedCrs namedCrs1 = Crs.Named("AName");
            NamedCrs namedCrs2 = Crs.Named("AName");
            NamedCrs namedCrs3 = Crs.Named("AnotherName");

            Assert.AreEqual(namedCrs1, namedCrs2);
            Assert.AreEqual(namedCrs1.GetHashCode(), namedCrs2.GetHashCode());
            Assert.AreNotEqual(namedCrs1, namedCrs3);
            Assert.AreNotEqual(namedCrs1.GetHashCode(), namedCrs3.GetHashCode());
        }

        /// <summary>
        /// Tests named CRS constructor exceptions.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NamedCrsConstructorException()
        {
            Crs.Named(null);
        }
    }
}