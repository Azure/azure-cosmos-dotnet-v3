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
    /// Tests for <see cref="Crs"/>.
    /// </summary>
    [TestClass]
    public class CrsTest
    {
        private JsonSerializerOptions restContractOptions;
        public CrsTest()
        {
            this.restContractOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeDataContractConverters(this.restContractOptions);
        }

        /// <summary>
        /// Tests serialization of ""unspecified"" CRS.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        [TestCategory("Quarantine")] // TODO: System.Text.Json does not apply converters when the value is null
        public void UnspecifiedCrsSerialization()
        {
            Crs crs = JsonSerializer.Deserialize<Crs>(@"null", this.restContractOptions);
            Assert.AreEqual(CrsType.Unspecified, crs.Type);
            Assert.AreEqual(Crs.Unspecified, crs);

            string json = JsonSerializer.Serialize(crs, this.restContractOptions);
            Crs crs1 = JsonSerializer.Deserialize<Crs>(json, this.restContractOptions);
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
            LinkedCrs linkedCrs = (LinkedCrs)JsonSerializer.Deserialize<Crs>(@"{""type"":""link"", ""properties"":{""href"":""http://foo"", ""type"":""link""}}", this.restContractOptions);
            Assert.AreEqual("http://foo", linkedCrs.Href);
            Assert.AreEqual(CrsType.Linked, linkedCrs.Type);

            string json = JsonSerializer.Serialize(linkedCrs, this.restContractOptions);
            LinkedCrs linkedCrs1 = (LinkedCrs)JsonSerializer.Deserialize<Crs>(json, this.restContractOptions);
            Assert.AreEqual(linkedCrs1, linkedCrs);
        }

        /// <summary>
        /// Tests deserialization of linked CRS when HREF is absent.
        /// </summary>
        [TestMethod]
        [Owner("laviswa")]
        [ExpectedException(typeof(JsonException))]
        public void LinkedCrsSerializationNoHref()
        {
            JsonSerializer.Deserialize<Crs>(@"{""type"":""linked"", ""properties"":{""href"":null}}", this.restContractOptions);
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
            NamedCrs namedCrs = (NamedCrs)JsonSerializer.Deserialize<Crs>(@"{""type"":""name"", ""properties"":{""name"":""AName""}}", this.restContractOptions);
            Assert.AreEqual("AName", namedCrs.Name);

            string json = JsonSerializer.Serialize(namedCrs, this.restContractOptions);
            NamedCrs namedCrs1 = (NamedCrs)JsonSerializer.Deserialize<Crs>(json, this.restContractOptions);
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
