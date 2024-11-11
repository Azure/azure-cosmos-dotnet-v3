//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public sealed class ChangeFeedObserverFactoryCoreTests
    {
        private readonly string leaseToken = Guid.NewGuid().ToString();
        private readonly CosmosSerializerCore cosmosSerializerCore = new CosmosSerializerCore();

        [TestMethod]
        public async Task WhenDelegateIsTyped_Legacy()
        {
            bool executed = false;
            Task changesHandler(IReadOnlyCollection<dynamic> docs, CancellationToken token)
            {
                Assert.AreEqual(1, docs.Count);
                Assert.AreEqual("Test", docs.First().id.ToString());
                executed = true;
                return Task.CompletedTask;
            }

            ChangeFeedObserverFactoryCore<dynamic> changeFeedObserverFactoryCore = new ChangeFeedObserverFactoryCore<dynamic>((Cosmos.Container.ChangesHandler<dynamic>)changesHandler, this.cosmosSerializerCore);

            ChangeFeedObserver changeFeedObserver = changeFeedObserverFactoryCore.CreateObserver();

            Assert.IsNotNull(changeFeedObserver);

            ResponseMessage responseMessage = this.BuildResponseMessage();
            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(this.leaseToken, responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);

            await changeFeedObserver.ProcessChangesAsync(context, responseMessage.Content, CancellationToken.None);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public async Task WhenDelegateIsTyped_Automatic()
        {
            bool executed = false;
            Task changesHandler(ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, CancellationToken token)
            {
                Assert.AreEqual(1, docs.Count);
                Assert.AreEqual("Test", docs.First().id.ToString());
                executed = true;
                return Task.CompletedTask;
            }

            ChangeFeedObserverFactoryCore<dynamic> changeFeedObserverFactoryCore = new ChangeFeedObserverFactoryCore<dynamic>((Container.ChangeFeedHandler<dynamic>)changesHandler, this.cosmosSerializerCore);

            ChangeFeedObserver changeFeedObserver = changeFeedObserverFactoryCore.CreateObserver();

            Assert.IsNotNull(changeFeedObserver);

            ResponseMessage responseMessage = this.BuildResponseMessage();
            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(this.leaseToken, responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);

            await changeFeedObserver.ProcessChangesAsync(context, responseMessage.Content, CancellationToken.None);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public async Task WhenDelegateIsTyped_Manual()
        {
            bool executed = false;
            Task changesHandler(ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> docs, Func<Task> checkpointAsync, CancellationToken token)
            {
                Assert.AreEqual(1, docs.Count);
                Assert.AreEqual("Test", docs.First().id.ToString());
                executed = true;
                return Task.CompletedTask;
            }

            ChangeFeedObserverFactoryCore<dynamic> changeFeedObserverFactoryCore = new ChangeFeedObserverFactoryCore<dynamic>(changesHandler, this.cosmosSerializerCore);

            ChangeFeedObserver changeFeedObserver = changeFeedObserverFactoryCore.CreateObserver();

            Assert.IsNotNull(changeFeedObserver);

            ResponseMessage responseMessage = this.BuildResponseMessage();
            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(this.leaseToken, responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);

            await changeFeedObserver.ProcessChangesAsync(context, responseMessage.Content, CancellationToken.None);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public async Task WhenDelegateIsStream_Automatic()
        {
            ResponseMessage responseMessage = this.BuildResponseMessage();
            bool executed = false;
            Task changesHandler(ChangeFeedProcessorContext context, Stream stream, CancellationToken token)
            {
                Assert.ReferenceEquals(responseMessage.Content, stream);
                executed = true;
                return Task.CompletedTask;
            }

            ChangeFeedObserverFactoryCore changeFeedObserverFactoryCore = new ChangeFeedObserverFactoryCore(changesHandler);

            ChangeFeedObserver changeFeedObserver = changeFeedObserverFactoryCore.CreateObserver();

            Assert.IsNotNull(changeFeedObserver);


            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(this.leaseToken, responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);

            await changeFeedObserver.ProcessChangesAsync(context, responseMessage.Content, CancellationToken.None);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public async Task WhenDelegateIsStream_Manual()
        {
            ResponseMessage responseMessage = this.BuildResponseMessage();
            bool executed = false;
            Task changesHandler(ChangeFeedProcessorContext context, Stream stream, Func<Task> checkpointAsync, CancellationToken token)
            {
                Assert.ReferenceEquals(responseMessage.Content, stream);
                executed = true;
                return Task.CompletedTask;
            }

            ChangeFeedObserverFactoryCore changeFeedObserverFactoryCore = new ChangeFeedObserverFactoryCore(changesHandler);

            ChangeFeedObserver changeFeedObserver = changeFeedObserverFactoryCore.CreateObserver();

            Assert.IsNotNull(changeFeedObserver);

            ChangeFeedObserverContextCore context = new ChangeFeedObserverContextCore(this.leaseToken, responseMessage, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange);

            await changeFeedObserver.ProcessChangesAsync(context, responseMessage.Content, CancellationToken.None);
            Assert.IsTrue(executed);
        }

        private ResponseMessage BuildResponseMessage()
        {
            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(@"{ ""Documents"": [{ ""id"": ""Test""}]}"))
            };
        }
    }
}