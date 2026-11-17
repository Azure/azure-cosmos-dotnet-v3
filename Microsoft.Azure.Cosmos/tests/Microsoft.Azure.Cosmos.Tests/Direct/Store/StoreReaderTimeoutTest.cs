// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class StoreReaderTimeoutTest
    {
        private static readonly StoreResponse DummyStoreResponse = new StoreResponse
        {
            Status = 200,
            Headers = new DictionaryNameValueCollection(
                new NameValueCollection
                {
                    { WFConstants.BackendHeaders.LSN, "50" },
                    { WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1" }
                })
        };

        [TestMethod]
        [Owner("jfigueroa")]
        public async Task CancellationThrowsOperationCanceledExceptionTest()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            Task<StoreResponse> InvokeResourceOperation(Uri x, DocumentServiceRequest y)
            {
                cts.Cancel();
                return Task.FromResult(StoreReaderTimeoutTest.DummyStoreResponse);
            }

            StoreReader storeReader = StoreReaderTimeoutTest.GetStoreReader(InvokeResourceOperation);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => storeReader.ReadMultipleReplicaAsync(
                    StoreReaderTimeoutTest.GetRequestEntity(cts?.Token ?? default),
                    false,
                    2,
                    false,
                    false,
                    ReadMode.Strong));
        }

        [TestMethod]
        [Owner("jfigueroa")]
        public async Task ExceptionTest()
        {
            static Task<StoreResponse> InvokeResourceOperation(Uri x, DocumentServiceRequest y)
            {
                throw new OutOfMemoryException();
            }

            StoreReader storeReader = StoreReaderTimeoutTest.GetStoreReader(InvokeResourceOperation);

            IList<ReferenceCountedDisposable<StoreResult>> readMultipleReplicaAsync = await storeReader.ReadMultipleReplicaAsync(
                StoreReaderTimeoutTest.GetRequestEntity(),
                false,
                2,
                false,
                false,
                ReadMode.Strong);

            Assert.AreEqual(0, readMultipleReplicaAsync.Count);
        }

        [TestMethod]
        [Owner("jfigueroa")]
        public async Task BarrierSucceedsWithHalfTasksCancelledTest()
        {
            int storeTaskCount = 0;
            int successStoreTaskCount = 0;
            Task<StoreResponse> cancelledTask = null, exceptionTask = null;

            Task<StoreResponse> InvokeResourceOperation(Uri x, DocumentServiceRequest y)
            {
                switch (Interlocked.Increment(ref storeTaskCount))
                {
                    case 1:
                        Assert.IsNull(cancelledTask);
                        cancelledTask = Task.FromCanceled<StoreResponse>(new CancellationToken(true));
                        Assert.IsTrue(cancelledTask.IsCanceled);
                        Assert.IsNull(cancelledTask.Exception);
                        return cancelledTask;
                    case 2:
                        Assert.IsNull(exceptionTask);
                        exceptionTask = Task.FromException<StoreResponse>(new TaskCanceledException());
                        Assert.IsFalse(exceptionTask.IsCanceled);
                        Assert.IsNotNull(exceptionTask.Exception);
                        return exceptionTask;
                    default:
                        Interlocked.Increment(ref successStoreTaskCount);
                        return Task.FromResult(StoreReaderTimeoutTest.DummyStoreResponse);
                }
            }

            StoreReader storeReader = StoreReaderTimeoutTest.GetStoreReader(InvokeResourceOperation);

            IList<ReferenceCountedDisposable<StoreResult>> readMultipleReplicaAsync = await storeReader.ReadMultipleReplicaAsync(
                StoreReaderTimeoutTest.GetRequestEntity(((CancellationTokenSource)null)?.Token ?? default),
                true,
                2,
                false,
                false,
                ReadMode.Strong,
                false,
                true);

            Assert.AreEqual(4, storeTaskCount);
            Assert.AreEqual(2, successStoreTaskCount);
            Assert.AreEqual(2, readMultipleReplicaAsync.Count);
            foreach (ReferenceCountedDisposable<StoreResult> storeResult in readMultipleReplicaAsync)
            {
                Console.WriteLine(JsonConvert.SerializeObject(storeResult.Target));
                Assert.AreEqual(200, (int)storeResult.Target.StatusCode);
            }
        }

        /// <summary>
        /// Get a store reader mock reading from 4 replicas.
        /// </summary>
        /// <param name="invokeResourceOperationAsyncHandler">Invoke operation handler.</param>
        /// <returns>Store reader configured to call the provided handler.</returns>
        private static StoreReader GetStoreReader(
            Func<Uri, DocumentServiceRequest, Task<StoreResponse>> invokeResourceOperationAsyncHandler)
        {
            Mock<TransportClient> mockTransportClient = new Mock<TransportClient>();
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<Uri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns(invokeResourceOperationAsyncHandler);
            mockTransportClient
                .Setup(x => x.InvokeResourceOperationAsync(It.IsAny<TransportAddressUri>(), It.IsAny<DocumentServiceRequest>()))
                .Returns<TransportAddressUri, DocumentServiceRequest>((x, y) => invokeResourceOperationAsyncHandler(x.Uri, y));

            const int totalReplicaCount = 4;
            AddressInformation[] addressInformation = new AddressInformation[totalReplicaCount];

            // construct URIs that look like the actual uri
            // rntbd://yt1prdddc01-docdb-1.documents.azure.com:14003/apps/ce8ab332-f59e-4ce7-a68e-db7e7cfaa128/services/68cc0b50-04c6-4716-bc31-2dfefd29e3ee/partitions/5604283d-0907-4bf4-9357-4fa9e62de7b5/replicas/131170760736528207s/
            for (int i = 0; i < totalReplicaCount; i++)
            {
                addressInformation[i] = new AddressInformation(
                    physicalUri:
                        "rntbd://dummytenant.documents.azure.com:14003/apps/APPGUID/services/SERVICEGUID/partitions/00000000-0000-0000-0000-000000000000/replicas/"
                        + i.ToString("G", CultureInfo.CurrentCulture)
                        + (i == 0 ? "p" : "s")
                        + "/",
                    isPrimary: i == 0,
                    protocol: Protocol.Tcp,
                    isPublic: true,
                    isAuxiliary: false);
            }

            // Address Selector is an internal sealed class that can't be mocked, but its dependency
            // AddressCache can be mocked.
            Mock<IAddressResolver> mockAddressCache = new Mock<IAddressResolver>();

            mockAddressCache
                .Setup(
                    cache => cache.ResolveAsync(
                        It.IsAny<DocumentServiceRequest>(),
                        It.IsAny<bool>(),
                        new CancellationToken()))
                .ReturnsAsync(new PartitionAddressInformation(addressInformation));

            return new StoreReader(
                mockTransportClient.Object,
                new AddressSelector(mockAddressCache.Object, Protocol.Tcp),
                new AddressEnumerator(),
                new NullSessionContainer(),
                enableReplicaValidation: false);
        }

        /// <summary>
        /// Get a dummy request entity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dummy request entity.</returns>
        private static DocumentServiceRequest GetRequestEntity(CancellationToken cancellationToken = default)
        {
            DocumentServiceRequest entity = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.SystemAll);
            entity.ResourceId = "1-MxAPlgMgA=";
            entity.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.BoundedStaleness.ToString();
            entity.RequestContext = new DocumentServiceRequestContext
            {
                RequestChargeTracker = new RequestChargeTracker(),
                TargetIdentity = new ServiceIdentity("dummyTargetIdentity1", new Uri("http://dummyTargetIdentity1"), false),
                ResolvedPartitionKeyRange = new PartitionKeyRange(),
                ClientRequestStatistics = new ClientSideRequestStatistics(),
                TimeoutHelper = new TimeoutHelper(TimeSpan.FromMinutes(5), cancellationToken)
            };

            return entity;
        }
    }
}