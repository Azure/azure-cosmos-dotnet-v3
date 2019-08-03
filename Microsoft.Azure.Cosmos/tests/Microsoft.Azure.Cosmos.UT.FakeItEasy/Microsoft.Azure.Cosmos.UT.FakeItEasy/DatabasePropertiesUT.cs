namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using FakeItEasy;
    using global::FakeItEasy;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DatabasePropertiesUT
    {
        const string DbName = "SomeName";
        const string Etag = "SomeEtag";
        static readonly DateTime LastModified = DateTime.UtcNow;

        [TestMethod]
        public void UT()
        {
            DatabaseProperties databaseProperties = new DatabaseProperties(DatabasePropertiesUT.DbName);
            PropertiesHelper.SetETag(databaseProperties, DatabasePropertiesUT.Etag);
            PropertiesHelper.SetLastModified(databaseProperties, DatabasePropertiesUT.LastModified);


            Assert.AreEqual(DatabasePropertiesUT.DbName, databaseProperties.Id);
            Assert.AreEqual(DatabasePropertiesUT.Etag, databaseProperties.ETag);
            Assert.AreEqual(DatabasePropertiesUT.LastModified, databaseProperties.LastModified);
        }
    }
}
