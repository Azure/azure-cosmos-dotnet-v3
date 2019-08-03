namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using FakeItEasy;
    using global::FakeItEasy;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ContainerUT
    {
        const string ContainerName = "Some Container";
        const string PartitionKeyPath = "/somepk";
        const string Query = "seelct * from c";
        const string QueryCT = "CONTINUATION_TOKEN";

        [TestMethod]
        public async Task ReadContainerTest()
        {
            Container container = A.Fake<Container>();
            ContainerResponse fakeResponse = A.Fake<ContainerResponse>();

            A.CallTo(() => container.ReadContainerAsync(A<ContainerRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeResponse);

            ContainerResponse response = await container.ReadContainerAsync();

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.ReadContainerAsync(A<ContainerRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task DeleteContainerTest()
        {
            Container container = A.Fake<Container>();
            ContainerResponse fakeResponse = A.Fake<ContainerResponse>();

            A.CallTo(() => container.DeleteContainerAsync(A<ContainerRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeResponse);

            ContainerResponse response = await container.DeleteContainerAsync();

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.DeleteContainerAsync(A<ContainerRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CreateItemTest()
        {
            Container container = A.Fake<Container>();
            ItemResponse<ItemPayload> fakeResponse = A.Fake<ItemResponse<ItemPayload>>();

            A.CallTo(() => container.CreateItemAsync<ItemPayload>(A<ItemPayload>.Ignored, A<PartitionKey?>.Ignored, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeResponse);

            ItemResponse<ItemPayload> response = await container.CreateItemAsync<ItemPayload>(null, null);

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.CreateItemAsync<ItemPayload>(null, null, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ReadItemTest()
        {
            Container container = A.Fake<Container>();
            ItemResponse<ItemPayload> fakeResponse = A.Fake<ItemResponse<ItemPayload>>();

            A.CallTo(() => container.ReadItemAsync<ItemPayload>(A<string>.Ignored, A<PartitionKey>.Ignored, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeResponse);

            ItemResponse<ItemPayload> response = await container.ReadItemAsync<ItemPayload>(null, PartitionKey.None);

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.ReadItemAsync<ItemPayload>(null, PartitionKey.None, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ReplaceItemTest()
        {
            Container container = A.Fake<Container>();
            ItemResponse<ItemPayload> fakeResponse = A.Fake<ItemResponse<ItemPayload>>();

            A.CallTo(() => container.ReplaceItemAsync<ItemPayload>(A<ItemPayload>.Ignored, A<string>.Ignored, A<PartitionKey?>.Ignored, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeResponse);

            ItemResponse<ItemPayload> response = await container.ReplaceItemAsync<ItemPayload>(null, null, null);

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.ReplaceItemAsync<ItemPayload>(null, null, null, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task DeleteItemTest()
        {
            Container container = A.Fake<Container>();
            ItemResponse<ItemPayload> fakeResponse = A.Fake<ItemResponse<ItemPayload>>();

            A.CallTo(() => container.DeleteItemAsync<ItemPayload>(A<string>.Ignored, A<PartitionKey>.Ignored, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeResponse);

            ItemResponse<ItemPayload> response = await container.DeleteItemAsync<ItemPayload>(null, PartitionKey.None);

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.DeleteItemAsync<ItemPayload>(null, PartitionKey.None, A<ItemRequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void IteratorTest()
        {
            Container container = A.Fake<Container>();
            FeedIterator<ItemPayload> fakeResponse = A.Fake<FeedIterator<ItemPayload>>();

            A.CallTo(() => container.GetItemQueryIterator<ItemPayload>(A<string>.Ignored, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .Returns(fakeResponse);

            FeedIterator<ItemPayload> response = container.GetItemQueryIterator<ItemPayload>(ContainerUT.Query, ContainerUT.QueryCT);

            Assert.ReferenceEquals(fakeResponse, response);
            A.CallTo(() => container.GetItemQueryIterator<ItemPayload>(ContainerUT.Query, ContainerUT.QueryCT, A<QueryRequestOptions>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        public class ItemPayload
        {
            string Prop1 { get; set; }
            string Prop2 { get; set; }
            string Prop3 { get; set; }
        }
    }
}
