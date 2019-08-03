namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using global::FakeItEasy;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Net;

    [TestClass]
    public class DatabaseResponseUT
    {
        [TestMethod]
        public void DatabaseResponseMock()
        {
            DatabaseResponse fakeDbResponse = A.Fake<DatabaseResponse>();

            string activityId = Guid.NewGuid().ToString();
            string etag = Guid.NewGuid().ToString();
            Database fakeDb = A.Fake<Database>();
            DatabaseProperties fakeDbProperties = A.Fake<DatabaseProperties>();
            Headers fakeHeaders = A.Fake<Headers>();
            double charges = 5.5;
            HttpStatusCode statusCode = HttpStatusCode.OK;

            A.CallTo(() => fakeDbResponse.ActivityId).Returns(activityId);
            A.CallTo(() => fakeDbResponse.Database).Returns(fakeDb);
            A.CallTo(() => fakeDbResponse.ETag).Returns(etag);
            A.CallTo(() => fakeDbResponse.Headers).Returns(fakeHeaders);
            A.CallTo(() => fakeDbResponse.RequestCharge).Returns(charges);
            A.CallTo(() => fakeDbResponse.StatusCode).Returns(statusCode);
            A.CallTo(() => fakeDbResponse.Resource).Returns(fakeDbProperties);

            Assert.AreEqual(activityId, fakeDbResponse.ActivityId);
            Assert.AreEqual(etag, fakeDbResponse.ETag);
            Assert.AreEqual(charges, fakeDbResponse.RequestCharge);
            Assert.AreEqual(statusCode, fakeDbResponse.StatusCode);
            Assert.ReferenceEquals(fakeDb, fakeDbResponse.Database);
            Assert.ReferenceEquals(fakeHeaders, fakeDbResponse.Headers);
            Assert.ReferenceEquals(fakeDbProperties, fakeDbResponse.Resource);

            A.CallTo(() => fakeDbResponse.ActivityId).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeDbResponse.Database).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeDbResponse.ETag).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeDbResponse.Headers).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeDbResponse.RequestCharge).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeDbResponse.StatusCode).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeDbResponse.Resource).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ImplicitDatabaseMock()
        {
            DatabaseResponse fakeDbResponse = A.Fake<DatabaseResponse>();

            Database fakeDb = A.Fake<Database>();
            A.CallTo(() => fakeDbResponse.Database).Returns(fakeDb);

            Database db = (Database)fakeDbResponse;
            Assert.ReferenceEquals(fakeDb, db);
            A.CallTo(() => fakeDbResponse.Database).MustHaveHappenedOnceExactly();
        }
    }
}
