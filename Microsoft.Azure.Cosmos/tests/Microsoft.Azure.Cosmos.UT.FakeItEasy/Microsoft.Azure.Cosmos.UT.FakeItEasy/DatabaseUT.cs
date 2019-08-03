namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using FakeItEasy;
    using global::FakeItEasy;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net.Cache;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class DatabaseUT
    {
        const string ContainerName = "Some Container";
        const string PartitionKey = "/somepk";
        const string Query = "seelct * from c";

        [TestMethod]
        public void GetId()
        {
            Database db = A.Fake<Database>();

            A.CallTo(() => db.Id).Returns(DatabaseUT.ContainerName);

            Assert.ReferenceEquals(DatabaseUT.ContainerName, db.Id);
            A.CallTo(() => db.Id).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CreateTests()
        {
            Database db = A.Fake<Database>();
            ContainerResponse fakeContainerResponse = A.Fake<ContainerResponse>();

            A.CallTo(() => db.CreateContainerAsync(A<ContainerProperties>.Ignored, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeContainerResponse);

            ContainerProperties cp = new ContainerProperties(DatabaseUT.ContainerName, DatabaseUT.PartitionKey);
            ContainerResponse response = await db.CreateContainerAsync(cp);

            Assert.ReferenceEquals(fakeContainerResponse, response);
            A.CallTo(() => db.CreateContainerAsync(
                    A<ContainerProperties>.That.Matches(e => e.Id == DatabaseUT.ContainerName && e.PartitionKeyPath == DatabaseUT.PartitionKey),
                    A<int?>.Ignored,
                    A<RequestOptions>.Ignored,
                    A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();

        }

        [TestMethod]
        public async Task CreateStreamTests()
        {
            Database db = A.Fake<Database>();
            ResponseMessage fakeContainerResponse = A.Fake<ResponseMessage>();

            A.CallTo(() => db.CreateContainerStreamAsync(A<ContainerProperties>.Ignored, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeContainerResponse);

            ContainerProperties cp = new ContainerProperties(DatabaseUT.ContainerName, DatabaseUT.PartitionKey);
            ResponseMessage response = await db.CreateContainerStreamAsync(cp);

            Assert.ReferenceEquals(fakeContainerResponse, response);
            A.CallTo(() => db.CreateContainerStreamAsync(
                    A<ContainerProperties>.That.Matches(e => e.Id == DatabaseUT.ContainerName && e.PartitionKeyPath == DatabaseUT.PartitionKey),
                    A<int?>.Ignored,
                    A<RequestOptions>.Ignored,
                    A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();

        }

        [TestMethod]
        public async Task DeleteDbTests()
        {
            Database db = A.Fake<Database>();
            DatabaseResponse fakeDbResponse = A.Fake<DatabaseResponse>();

            A.CallTo(() => db.DeleteAsync(A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeDbResponse);

            DatabaseResponse response = await db.DeleteAsync();

            Assert.ReferenceEquals(fakeDbResponse, response);
            A.CallTo(() => db.DeleteAsync(A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task DbThroughputTests()
        {
            Database db = A.Fake<Database>();
            int? testThroughput = 100000;

            A.CallTo(() => db.ReadThroughputAsync(A<CancellationToken>.Ignored))
                .Returns(testThroughput);

            int? response = await db.ReadThroughputAsync();

            Assert.ReferenceEquals(testThroughput, response);
            A.CallTo(() => db.ReadThroughputAsync(A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetContainerTest()
        {
            Database db = A.Fake<Database>();
            Container fakeContainer = A.Fake<Container>();

            A.CallTo(() => db.GetContainer(A<string>.Ignored))
                .Returns(fakeContainer);

            Container response = db.GetContainer(DatabaseUT.ContainerName);

            Assert.ReferenceEquals(fakeContainer, response);
            A.CallTo(() => db.GetContainer(A<string>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ContainerIteratorTests()
        {
            Database db = A.Fake<Database>();
            FeedIterator<ContainerProperties> fakeContainerIter = A.Fake<FeedIterator<ContainerProperties>>();

            A.CallTo(() => db.GetContainerQueryIterator<ContainerProperties>(A<string>.Ignored, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .Returns(fakeContainerIter);

            FeedIterator<ContainerProperties> response = db.GetContainerQueryIterator<ContainerProperties>(DatabaseUT.Query);

            Assert.ReferenceEquals(fakeContainerIter, response);
            A.CallTo(() => db.GetContainerQueryIterator<ContainerProperties>(DatabaseUT.Query, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .MustHaveHappenedOnceExactly();
        }
    }
}
