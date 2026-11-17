// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;

    [TestClass]
    public class ClientCollectionCacheTests
    {
        private const string CollectionPath = "dbs/testDb/colls/testContainer";
        private static readonly string OriginalRid = ResourceId.NewDocumentCollectionId(42, 129).DocumentCollectionId.ToString();
        private static readonly string ReplacementRid = ResourceId.NewDocumentCollectionId(42, 130).DocumentCollectionId.ToString();

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, true, false)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public async Task RefreshCollection_EntryPointsPreserveReplacementSession(
            bool useRequest, bool changedRid, bool alreadyRefreshed)
        {
            const string originalToken = "0:1#9#4=8#5=7";
            const string replacementToken = "0:1#10#4=9#5=8";
            const string unrelatedPath = "dbs/testDb/colls/unrelatedContainer";
            string unrelatedRid = ResourceId.NewDocumentCollectionId(42, 131).DocumentCollectionId.ToString();
            SessionContainer sessions = new SessionContainer("testhost");
            sessions.SetSessionToken(OriginalRid, CollectionPath, CreateSessionHeaders(originalToken));
            sessions.SetSessionToken(unrelatedRid, unrelatedPath, CreateSessionHeaders(originalToken));
            Mock<ISessionContainer> sessionContainer = new Mock<ISessionContainer>();
            sessionContainer.Setup(s => s.ClearTokenByResourceId(It.IsAny<string>()))
                .Callback<string>(rid =>
                {
                    // Model a successful replacement-container operation after publication but before cleanup.
                    sessions.SetSessionToken(ReplacementRid, CollectionPath, CreateSessionHeaders(replacementToken));
                    sessions.ClearTokenByResourceId(rid);
                });
            sessionContainer.Setup(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()))
                .Callback<string>(sessions.ClearTokenByCollectionFullname);
            int metadataReads = 0;
            string currentRid = OriginalRid;
            Mock<ClientCollectionCache> cache = CreateCache(sessionContainer.Object, _ =>
            {
                metadataReads++;
                return Task.FromResult(ContainerProperties.CreateWithResourceId(currentRid));
            });
            await cache.Object.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion, CollectionPath, false, NoOpTrace.Singleton, null, CancellationToken.None);
            currentRid = changedRid ? ReplacementRid : OriginalRid;
            if (alreadyRefreshed)
            {
                cache.Object.Refresh(CollectionPath, HttpConstants.Versions.CurrentVersion);
                await cache.Object.ResolveByNameAsync(
                    HttpConstants.Versions.CurrentVersion, CollectionPath, false, NoOpTrace.Singleton, null, CancellationToken.None);
            }

            using DocumentServiceRequest request = CreateRequest(OriginalRid);
            ContainerProperties result = useRequest
                ? await cache.Object.ResolveCollectionAsync(request, CancellationToken.None, NoOpTrace.Singleton)
                : await cache.Object.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, CollectionPath, OriginalRid,
                    NoOpTrace.Singleton, null, CancellationToken.None);

            Assert.AreEqual(currentRid, result.ResourceId);
            Assert.AreEqual(2, metadataReads);
            Assert.AreEqual(changedRid ? replacementToken : originalToken, sessions.GetSessionToken(CollectionPath));
            Assert.AreEqual(originalToken, sessions.GetSessionToken(unrelatedPath));
            sessionContainer.Verify(s => s.ClearTokenByResourceId(OriginalRid), changedRid ? Times.Once() : Times.Never());
            sessionContainer.Verify(s => s.ClearTokenByResourceId(It.Is<string>(rid => rid != OriginalRid)), Times.Never());
            sessionContainer.Verify(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()), Times.Never());
            if (useRequest)
            {
                Assert.IsFalse(request.ForceNameCacheRefresh);
                Assert.AreEqual(currentRid, request.RequestContext.ResolvedCollectionRid);
                Assert.AreEqual(currentRid, request.ResourceId);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RefreshCollection_EntryPointsSupportMissingSessionContainer(bool useRequest)
        {
            Mock<ClientCollectionCache> cache = CreateCache(null, _ =>
                Task.FromResult(ContainerProperties.CreateWithResourceId(ReplacementRid)));
            using DocumentServiceRequest request = CreateRequest(OriginalRid);

            ContainerProperties result = useRequest
                ? await cache.Object.ResolveCollectionAsync(request, CancellationToken.None, NoOpTrace.Singleton)
                : await cache.Object.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, CollectionPath, OriginalRid,
                    NoOpTrace.Singleton, null, CancellationToken.None);

            Assert.AreEqual(ReplacementRid, result.ResourceId);
        }

        [DataTestMethod]
        [DataRow(false, 0)]
        [DataRow(true, 0)]
        [DataRow(false, 1)]
        [DataRow(true, 1)]
        [DataRow(false, 2)]
        [DataRow(true, 2)]
        public async Task RefreshCollection_EntryPointsPropagateFailureWithoutSessionCleanup(bool useRequest, int failureKind)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            DocumentClientException notFound = CreateNotFoundException();
            InvalidOperationException unexpected = new InvalidOperationException("Metadata resolution failed.");
            Exception failure = failureKind switch
            {
                0 => notFound,
                1 => unexpected,
                _ => new OperationCanceledException(cancellation.Token)
            };
            Mock<ISessionContainer> sessions = new Mock<ISessionContainer>();
            Mock<ClientCollectionCache> cache = CreateCache(sessions.Object, token =>
            {
                Assert.AreEqual(cancellation.Token, token);
                return Task.FromException<ContainerProperties>(failure);
            });
            using DocumentServiceRequest request = CreateRequest(OriginalRid);
            Func<Task<ContainerProperties>> resolve = () => useRequest
                ? cache.Object.ResolveCollectionAsync(request, cancellation.Token, NoOpTrace.Singleton)
                : cache.Object.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, CollectionPath, OriginalRid,
                    NoOpTrace.Singleton, null, cancellation.Token);

            Exception thrown;
            if (failureKind == 0)
            {
                DocumentClientException exception = await Assert.ThrowsExceptionAsync<DocumentClientException>(resolve);
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
                Assert.AreEqual(SubStatusCodes.Unknown, exception.GetSubStatus());
                thrown = exception;
            }
            else if (failureKind == 1)
            {
                thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(resolve);
                Assert.AreSame(unexpected, thrown);
            }
            else
            {
                thrown = await Assert.ThrowsExceptionAsync<OperationCanceledException>(resolve);
            }

            Assert.AreEqual(failure.Message, thrown.Message);
            sessions.Verify(s => s.ClearTokenByResourceId(It.IsAny<string>()), Times.Never());
            sessions.Verify(s => s.ClearTokenByCollectionFullname(It.IsAny<string>()), Times.Never());
            if (useRequest)
            {
                Assert.IsTrue(request.ForceNameCacheRefresh);
                Assert.AreEqual(OriginalRid, request.RequestContext.ResolvedCollectionRid);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RefreshCollection_EntryPointsHonorPreCancelledToken(bool useRequest)
        {
            int metadataReads = 0;
            Mock<ISessionContainer> sessions = new Mock<ISessionContainer>();
            Mock<ClientCollectionCache> cache = CreateCache(sessions.Object, _ =>
            {
                metadataReads++;
                return Task.FromResult(ContainerProperties.CreateWithResourceId(ReplacementRid));
            });
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            using DocumentServiceRequest request = CreateRequest(OriginalRid);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => useRequest
                ? cache.Object.ResolveCollectionAsync(request, cancellation.Token, NoOpTrace.Singleton)
                : cache.Object.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, CollectionPath, OriginalRid,
                    NoOpTrace.Singleton, null, cancellation.Token));

            Assert.AreEqual(0, metadataReads);
            sessions.Verify(s => s.ClearTokenByResourceId(It.IsAny<string>()), Times.Never());
        }

        [TestMethod]
        public async Task ResolveCollectionAsync_RefreshWithoutSubmittedRidInvalidatesCachedMetadata()
        {
            Mock<ISessionContainer> sessions = new Mock<ISessionContainer>();
            string currentRid = OriginalRid;
            int metadataReads = 0;
            Mock<ClientCollectionCache> cache = CreateCache(sessions.Object, _ =>
            {
                metadataReads++;
                return Task.FromResult(ContainerProperties.CreateWithResourceId(currentRid));
            });
            await cache.Object.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion, CollectionPath, false, NoOpTrace.Singleton, null, CancellationToken.None);
            currentRid = ReplacementRid;
            using DocumentServiceRequest request = CreateRequest(null);

            ContainerProperties result = await cache.Object.ResolveCollectionAsync(
                request, CancellationToken.None, NoOpTrace.Singleton);

            Assert.AreEqual(ReplacementRid, result.ResourceId);
            Assert.AreEqual(ReplacementRid, request.RequestContext.ResolvedCollectionRid);
            Assert.IsFalse(request.ForceNameCacheRefresh);
            Assert.AreEqual(2, metadataReads);
            sessions.Verify(s => s.ClearTokenByResourceId(It.IsAny<string>()), Times.Never());
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task ResolveCollectionAsync_PreservesNotFoundSubstatusHeaders(bool itemRequest, bool useRefreshAfter)
        {
            DocumentClientException notFound = CreateNotFoundException();
            string originalSubstatus = notFound.Headers[WFConstants.BackendHeaders.SubStatus];
            Mock<ClientCollectionCache> cache = CreateCache(null, _ => Task.FromException<ContainerProperties>(notFound));
            using DocumentServiceRequest request = CreateRequest(null, itemRequest ? ResourceType.Document : ResourceType.Collection);
            request.ForceNameCacheRefresh = !useRefreshAfter;

            DocumentClientException thrown = await Assert.ThrowsExceptionAsync<DocumentClientException>(() => useRefreshAfter
                ? cache.Object.ResolveCollectionAsync(request, TimeSpan.Zero, CancellationToken.None, NoOpTrace.Singleton)
                : cache.Object.ResolveCollectionAsync(request, CancellationToken.None, NoOpTrace.Singleton));

            Assert.AreEqual(HttpStatusCode.NotFound, thrown.StatusCode);
            Assert.AreEqual(itemRequest ? "1003" : originalSubstatus, thrown.Headers[WFConstants.BackendHeaders.SubStatus]);
        }

        [DataTestMethod]
        [DataRow(null, "rid", "resourceAddress")]
        [DataRow("", "rid", "resourceAddress")]
        [DataRow(CollectionPath, null, "previouslyResolvedCollectionRid")]
        [DataRow(CollectionPath, "", "previouslyResolvedCollectionRid")]
        public async Task RefreshCollectionAsync_RejectsMissingInputs(string path, string rid, string parameter)
        {
            Mock<ClientCollectionCache> cache = CreateCache(null, _ =>
                throw new AssertFailedException("Invalid inputs must not trigger metadata resolution."));

            ArgumentNullException exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                cache.Object.RefreshCollectionAsync(
                    HttpConstants.Versions.CurrentVersion, path, rid, NoOpTrace.Singleton, null, CancellationToken.None));

            Assert.AreEqual(parameter, exception.ParamName);
        }

        private static Mock<ClientCollectionCache> CreateCache(
            ISessionContainer sessions,
            Func<CancellationToken, Task<ContainerProperties>> readMetadata)
        {
            Mock<ClientCollectionCache> cache = new Mock<ClientCollectionCache>(
                sessions, new ServerStoreModel(null), null, null, null, true, null)
            {
                CallBase = true
            };
            cache.Protected().Setup<Task<ContainerProperties>>(
                "GetByNameAsync", ItExpr.IsAny<string>(), ItExpr.IsAny<string>(),
                ItExpr.IsAny<ITrace>(), ItExpr.IsAny<IClientSideRequestStatistics>(),
                ItExpr.IsAny<CancellationToken>())
                .Returns<string, string, ITrace, IClientSideRequestStatistics, CancellationToken>(
                    (_, _, _, _, token) => readMetadata(token));
            return cache;
        }

        private static DocumentServiceRequest CreateRequest(string submittedRid, ResourceType resourceType = ResourceType.Collection)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read, resourceType,
                resourceType == ResourceType.Document ? CollectionPath + "/docs/item" : CollectionPath,
                AuthorizationTokenType.PrimaryMasterKey, new RequestNameValueCollection());
            request.Headers[HttpConstants.HttpHeaders.Version] = HttpConstants.Versions.CurrentVersion;
            request.RequestContext.ResolvedCollectionRid = submittedRid;
            request.ForceNameCacheRefresh = true;
            return request;
        }

        private static RequestNameValueCollection CreateSessionHeaders(string token)
        {
            return new RequestNameValueCollection { [HttpConstants.HttpHeaders.SessionToken] = token };
        }

        private static DocumentClientException CreateNotFoundException()
        {
            return new DocumentClientException(
                "Container not found", innerException: null, statusCode: HttpStatusCode.NotFound,
                substatusCode: SubStatusCodes.Unknown, requestUri: null,
                responseHeaders: new DictionaryNameValueCollection());
        }
    }
}
