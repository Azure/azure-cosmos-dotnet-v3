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
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;

    [TestClass]
    public class ThinClientTransportSerializerTests
    {
        // A single base64-encoded RNTBD response representing an HTTP 201 (Created) in RNTBD format.
        private const string base64MockResponse =
            "9AEAAMkAAAAIvhHfD23jSaynaR+gyTZ3AAAAAQIAByFUaHUsIDEzIEZlYiAyMDI1IDE0OjI1OjI4LjAyNCBHTVQEAAgmACIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDAiDgAIVABkb2N1bWVudFNpemU9NTEyMDA7ZG9jdW1lbnRzU2l6ZT01MjQyODgwMDtkb2N1bWVudHNDb3VudD0tMTtjb2xsZWN0aW9uU2l6ZT01MjQyODgwMDsPAAhBAGRvY3VtZW50U2l6ZT0wO2RvY3VtZW50c1NpemU9MTtkb2N1bWVudHNDb3VudD04O2NvbGxlY3Rpb25TaXplPTM7EAAHBDEuMTkTAAUKAAAAAAAAABUADgzDMAzDMBxAFwAIOgBkYnMvdGhpbi1jbGllbnQtdGVzdC1kYi9jb2xscy90aGluLWNsaWVudC10ZXN0LWNvbnRhaW5lci0xGAAIDABOSDF1QUo2QU5tMD0aAAUJAAAAAAAAAB4AAgMAAAAfAAIEAAAAIQAIAQAwJgACAQAAACkABQkAAAAAAAAAMAACAAAAADUAAgEAAAA6AAUKAAAAAAAAADsABQkAAAAAAAAAPgAIBQAtMSMxMFEADkjhehSuRxBAYwAIAQAweAAF//////////89AQAAeyJpZCI6IjNiMTFiNDM2LTViMTUtNGQwZS1iZWYwLWY1MzVmNjA0MTQxYyIsInBrIjoicGsiLCJuYW1lIjoiODM2MzI0NTA2IiwiZW1haWwiOiJhYmNAZGVmLmNvbSIsImJvZHkiOiJibGFibGEiLCJfcmlkIjoiTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09IiwiX3NlbGYiOiJkYnMvTkgxdUFBPT0vY29sbHMvTkgxdUFKNkFObTA9L2RvY3MvTkgxdUFKNkFObTBKQUFBQUFBQUFBQT09LyIsIl9ldGFnIjoiXCIwMDAwYWQzZS0wMDAwLTAyMDAtMDAwMC02N2FlNjRjMDAwMDBcIiIsIl9hdHRhY2htZW50cyI6ImF0dGFjaG1lbnRzLyIsIl90cyI6MTczOTQ4MjMwNH0=";

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ShouldThrowIfNoPartitionKeyInPointOperation()
        {
            // Arrange
            HttpRequestMessage message = new HttpRequestMessage
            {
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Read.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null; // or mock if needed

            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
              .Setup(c => c.ResolveCollectionAsync(
                  It.IsAny<DocumentServiceRequest>(),
                  It.IsAny<CancellationToken>(),
                  It.IsAny<ITrace>()))
              .ReturnsAsync(this.GetMockContainerProperties());

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InternalServerErrorException>(
                () => ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    bufferProvider,
                    "MockAccount",
                    clientCollectionCacheMock.Object,
                    message),
                "Expected an InternalServerErrorException for missing PartitionKey in point operation");
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ShouldNotThrowIfPartitionKeyProvided()
        {
            // Arrange
            HttpRequestMessage message = new HttpRequestMessage
            {
                Content = new StringContent("Test Body"),
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Read.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.PartitionKey, "[\"SamplePk\"]");
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;

            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            // Act
            Stream resultStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                bufferProvider,
                "MockAccount",
                clientCollectionCacheMock.Object,
                message);

            // Assert
            Assert.IsNotNull(resultStream, "Expected a valid stream result.");
            Assert.IsTrue(resultStream.Length > 0, "Stream should contain RNTBD-serialized bytes.");
        }

        [TestMethod]
        public void GetEffectivePartitionKeyHash_ShouldReturnHash()
        {
            // Arrange
            string pkJson = "[\"TestValue\"]";

            // Act
            string epkHash = ThinClientTransportSerializer.GetEffectivePartitionKeyHash(
                pkJson,
                this.GetMockContainerProperties().PartitionKey);

            // Assert
            Assert.IsNotNull(epkHash, "EPK hash should not be null for a valid JSON partition key.");
            Assert.IsTrue(epkHash.Length > 0, "EPK hash should have a non-empty value.");
        }

        [TestMethod]
        public void ApplyEpkRangeHeaders_UsesProxyBoundsWhenCallerHasNoEpkRange()
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();

            Dictionary<string, object> properties = ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers);

            this.AssertEpkRange(headers, properties, string.Empty, "FF");
        }

        [TestMethod]
        public void ApplyEpkRangeHeaders_PreservesCallerSubRangeAndOverwritesExistingHeaders()
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();
            headers.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            headers.Set(HttpConstants.HttpHeaders.StartEpk, "1A2B");
            headers.Set(HttpConstants.HttpHeaders.EndEpk, "3C4D");

            Dictionary<string, object> properties = ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers);

            this.AssertEpkRange(headers, properties, "1A2B", "3C4D");
        }

        [TestMethod]
        public void ApplyEpkRangeHeaders_UsesProxyBoundsWhenReadFeedKeyTypeIsNotEpkRange()
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();
            headers.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, "ResourceId");
            headers.Set(HttpConstants.HttpHeaders.StartEpk, "1A2B");
            headers.Set(HttpConstants.HttpHeaders.EndEpk, "3C4D");

            Dictionary<string, object> properties = ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers);

            this.AssertEpkRange(headers, properties, string.Empty, "FF");
        }

        [TestMethod]
        public void ApplyEpkRangeHeaders_ThrowsWhenCallerRangeIsOutsideProxyRange()
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();
            headers.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            headers.Set(HttpConstants.HttpHeaders.StartEpk, "10");
            headers.Set(HttpConstants.HttpHeaders.EndEpk, "FF01");

            Assert.ThrowsException<InvalidOperationException>(
                () => ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers));
        }

        [TestMethod]
        public void ApplyEpkRangeHeaders_ThrowsWhenProxyRangeIsIncomplete()
        {
            RequestNameValueCollection headers = new RequestNameValueCollection();
            headers.Set(ThinClientConstants.ProxyStartEpk, string.Empty);

            Assert.ThrowsException<InvalidOperationException>(
                () => ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers));
        }

        [TestMethod]
        [DataRow("10", "10")]
        [DataRow("20", "10")]
        public void ApplyEpkRangeHeaders_RejectsEmptyOrReversedProxyRange(string startEpk, string endEpk)
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();
            headers.Set(ThinClientConstants.ProxyStartEpk, startEpk);
            headers.Set(ThinClientConstants.ProxyEndEpk, endEpk);

            Assert.ThrowsException<InvalidOperationException>(
                () => ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers));
        }

        [TestMethod]
        [DataRow(null, "3C4D")]
        [DataRow("1A2B", null)]
        [DataRow(null, null)]
        [DataRow("3C4D", "1A2B")]
        [DataRow("1A2B", "1A2B")]
        public void ApplyEpkRangeHeaders_RejectsIncompleteOrEmptyCallerRange(string startEpk, string endEpk)
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();
            headers.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            headers.Set(HttpConstants.HttpHeaders.StartEpk, startEpk);
            headers.Set(HttpConstants.HttpHeaders.EndEpk, endEpk);

            Assert.ThrowsException<InvalidOperationException>(
                () => ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers));
        }

        [TestMethod]
        public void ApplyEpkRangeHeaders_IsIdempotent()
        {
            RequestNameValueCollection headers = this.CreateEpkRangeHeaders();
            headers.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            headers.Set(HttpConstants.HttpHeaders.StartEpk, "1A2B");
            headers.Set(HttpConstants.HttpHeaders.EndEpk, "3C4D");

            ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers);
            Dictionary<string, object> properties = ThinClientTransportSerializer.ApplyEpkRangeHeaders(headers);

            this.AssertEpkRange(headers, properties, "1A2B", "3C4D");
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_PreservesExistingEpkSubRange()
        {
            HttpRequestMessage message = new HttpRequestMessage
            {
                Content = new StringContent("{\"query\":\"SELECT * FROM c\"}"),
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Query.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            message.Headers.TryAddWithoutValidation(ThinClientConstants.ProxyStartEpk, string.Empty);
            message.Headers.TryAddWithoutValidation(ThinClientConstants.ProxyEndEpk, "FF");
            message.Headers.TryAddWithoutValidation(
                HttpConstants.HttpHeaders.ReadFeedKeyType,
                RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.StartEpk, "1A2B");
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.EndEpk, "3C4D");

            using Stream resultStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                new ThinClientTransportSerializer.BufferProviderWrapper(),
                "MockAccount",
                this.CreateClientCollectionCacheMock().Object,
                message);

            Assert.IsTrue(resultStream.Length > 0, "The EPK sub-range feed request should serialize successfully.");
            using MemoryStream copy = new MemoryStream();
            await resultStream.CopyToAsync(copy);
            RntbdConstants.Request request = ParseProxyRequest(copy.ToArray());
            CollectionAssert.AreEqual(HexStringUtility.HexStringToBytes("1A2B"), request.startEpkHash.value.valueBytes.ToArray());
            CollectionAssert.AreEqual(HexStringUtility.HexStringToBytes("3C4D"), request.endEpkHash.value.valueBytes.ToArray());
            Assert.AreEqual("1A2B", Encoding.UTF8.GetString(request.startEpk.value.valueBytes.Span));
            Assert.AreEqual("3C4D", Encoding.UTF8.GetString(request.endEpk.value.valueBytes.Span));
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ShouldOverwriteExistingCollectionRid()
        {
            byte[] baseline = await this.SerializeWithExtraHeadersAsync(new Dictionary<string, string>());
            byte[] payload = await this.SerializeWithExtraHeadersAsync(
                new Dictionary<string, string>
                {
                    { WFConstants.BackendHeaders.CollectionRid, "staleCollectionRid" }
                });
            RntbdConstants.Request expected = ParseProxyRequest(baseline);
            RntbdConstants.Request actual = ParseProxyRequest(payload);
            Assert.IsTrue(actual.collectionRid.isPresent);
            CollectionAssert.AreEqual(expected.collectionRid.value.valueBytes.ToArray(), actual.collectionRid.value.valueBytes.ToArray());
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task SerializeProxyRequestAsync_RejectsIncompleteProxyRange(bool hasStartBound)
        {
            using HttpRequestMessage message = new HttpRequestMessage
            {
                Content = new StringContent("{\"query\":\"SELECT * FROM c\"}"),
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs"),
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, OperationType.Query.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            message.Headers.TryAddWithoutValidation(
                hasStartBound ? ThinClientConstants.ProxyStartEpk : ThinClientConstants.ProxyEndEpk,
                hasStartBound ? string.Empty : "FF");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    new ThinClientTransportSerializer.BufferProviderWrapper(),
                    "MockAccount",
                    this.CreateClientCollectionCacheMock().Object,
                    message));
        }

        [TestMethod]
        [DataRow(HttpConstants.HttpHeaders.Authorization, "type=master&ver=1.0&sig=test", (ushort)RntbdConstants.RequestIdentifiers.AuthorizationToken)]
        [DataRow(HttpConstants.HttpHeaders.SessionToken, "0:1#100#1=90#2=85", (ushort)RntbdConstants.RequestIdentifiers.SessionToken)]
        [DataRow(HttpConstants.HttpHeaders.Continuation, "continuation-token", (ushort)RntbdConstants.RequestIdentifiers.ContinuationToken)]
        [DataRow(HttpConstants.HttpHeaders.IfNoneMatch, "\"etag-value\"", (ushort)RntbdConstants.RequestIdentifiers.Match)]
        [DataRow(HttpConstants.HttpHeaders.Version, "2018-12-31", (ushort)RntbdConstants.RequestIdentifiers.ClientVersion)]
        [DataRow(HttpConstants.HttpHeaders.PreTriggerInclude, "trigger1,trigger2", (ushort)RntbdConstants.RequestIdentifiers.PreTriggerInclude)]
        [DataRow(HttpConstants.HttpHeaders.PostTriggerInclude, "trigger1,trigger2", (ushort)RntbdConstants.RequestIdentifiers.PostTriggerInclude)]
        [DataRow(HttpConstants.HttpHeaders.Date, "Thu, 15 Oct 2026 17:00:00 GMT", (ushort)RntbdConstants.RequestIdentifiers.Date)]
        public async Task SerializeProxyRequestAsync_PreservesStringHeaderOnWire(
            string headerName,
            string headerValue,
            ushort tokenIdentifier)
        {
            byte[] payload = await this.SerializeWithExtraHeadersAsync(
                new Dictionary<string, string> { { headerName, headerValue } });

            RntbdConstants.Request request = ParseProxyRequest(payload);
            RntbdToken token = request.tokens.Single(
                candidate => candidate != null && candidate.GetTokenIdentifier() == tokenIdentifier);
            Assert.IsTrue(token.isPresent, $"The {headerName} header must be serialized.");
            Assert.AreEqual(headerValue, Encoding.UTF8.GetString(token.value.valueBytes.Span));
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_PreservesCapabilitiesOnWire()
        {
            byte[] payload = await this.SerializeWithExtraHeadersAsync(
                new Dictionary<string, string> { { HttpConstants.HttpHeaders.SDKSupportedCapabilities, "1" } });
            RntbdConstants.Request request = ParseProxyRequest(payload);

            Assert.IsTrue(request.sDKSupportedCapabilities.isPresent);
            Assert.AreEqual(1u, request.sDKSupportedCapabilities.value.valueULong);
        }

        [TestMethod]
        [DataRow("Create")]
        [DataRow("Replace")]
        [DataRow("Patch")]
        [DataRow("Delete")]
        public async Task SerializeProxyRequestAsync_PreservesIfMatchOnWire(string operation)
        {
            byte[] payload = await this.SerializeWithExtraHeadersAsync(
                new Dictionary<string, string> { { HttpConstants.HttpHeaders.IfMatch, "\"write-etag\"" } },
                (OperationType)Enum.Parse(typeof(OperationType), operation));
            RntbdConstants.Request request = ParseProxyRequest(payload);

            Assert.IsTrue(request.match.isPresent);
            Assert.AreEqual("\"write-etag\"", Encoding.UTF8.GetString(request.match.value.valueBytes.Span));
        }

        [TestMethod]
        public async Task ConvertProxyResponseAsync_PreservesSessionAndEtagWithoutDuplicatingHeaders()
        {
            using HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };
            using HttpResponseMessage converted = await ThinClientTransportSerializer.ConvertProxyResponseAsync(response);

            Assert.AreEqual("0:-1#10", converted.Headers.GetValues(HttpConstants.HttpHeaders.SessionToken).Single());
            Assert.AreEqual("\"0000ad3e-0000-0200-0000-67ae64c00000\"", converted.Headers.GetValues(HttpConstants.HttpHeaders.ETag).Single());
            Assert.AreEqual("7.05", converted.Headers.GetValues(HttpConstants.HttpHeaders.RequestCharge).Single());
            Assert.AreEqual("1", converted.Headers.GetValues(ThinClientConstants.RoutedViaProxy).Single());
        }

        [TestMethod]
        [DataRow("Read")]
        [DataRow("Query")]
        [DataRow("ReadFeed")]
        public async Task InvokeAsync_HttpRetryPreservesPayloadHeadersWithoutAccumulatingValues(string operation)
        {
            OperationType operationType = (OperationType)Enum.Parse(typeof(OperationType), operation);
            Guid activityId = Guid.NewGuid();
            Guid previousActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId;
            int attempts = 0;
            Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage message, CancellationToken cancellationToken) =>
                {
                    attempts++;
                    Assert.AreEqual(activityId.ToString(), message.Headers.GetValues(HttpConstants.HttpHeaders.ActivityId).Single());
                    Assert.AreEqual(HttpConstants.Versions.CurrentVersion, message.Headers.GetValues(HttpConstants.HttpHeaders.Version).Single());
                    Assert.AreEqual(Headers.SDKSUPPORTEDCAPABILITIES, message.Headers.GetValues(HttpConstants.HttpHeaders.SDKSupportedCapabilities).Single());
                    Assert.IsFalse(message.Headers.Contains(HttpConstants.HttpHeaders.Authorization));
                    Assert.IsFalse(message.Headers.Contains(ThinClientConstants.ProxyStartEpk));
                    Assert.IsFalse(message.Headers.Contains(ThinClientConstants.ProxyOperationType));

                    byte[] payload = await message.Content.ReadAsByteArrayAsync();
                    Assert.AreEqual(activityId, new Guid(payload.AsSpan(8, 16)));
                    RntbdConstants.Request serialized = ParseProxyRequest(payload);
                    Assert.AreEqual("test-authorization", Encoding.UTF8.GetString(serialized.authorizationToken.value.valueBytes.Span));
                    Assert.AreEqual("0:1#100", Encoding.UTF8.GetString(serialized.sessionToken.value.valueBytes.Span));
                    if (operationType != OperationType.Read)
                    {
                        Assert.AreEqual("1A2B", Encoding.UTF8.GetString(serialized.startEpk.value.valueBytes.Span));
                        Assert.AreEqual("3C4D", Encoding.UTF8.GetString(serialized.endEpk.value.valueBytes.Span));
                        CollectionAssert.AreEqual(HexStringUtility.HexStringToBytes("1A2B"), serialized.startEpkHash.value.valueBytes.ToArray());
                        CollectionAssert.AreEqual(HexStringUtility.HexStringToBytes("3C4D"), serialized.endEpkHash.value.valueBytes.ToArray());
                    }

                    return new HttpResponseMessage(attempts == 1 ? HttpStatusCode.RequestTimeout : HttpStatusCode.Created)
                    {
                        RequestMessage = message,
                        Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse)),
                    };
                });

            ConnectionPolicy policy = new ConnectionPolicy
            {
                HttpClientFactory = () => new HttpClient(handler.Object),
            };
            using CosmosHttpClient httpClient = CosmosHttpClientCore.CreateWithConnectionPolicy(
                apiType: default,
                eventSource: DocumentClientEventSource.Instance,
                connectionPolicy: policy,
                httpMessageHandler: null,
                sendingRequestEventArgs: null,
                receivedResponseEventArgs: null);
            using ThinClientStoreClient storeClient = new ThinClientStoreClient(
                httpClient,
                new Cosmos.UserAgentContainer(0, "HeaderAudit", "TestRegion", "TestSuffix"),
                DocumentClientEventSource.Instance,
                GlobalPartitionEndpointManagerNoOp.Instance);
            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                operationType: operationType,
                resourceType: ResourceType.Document,
                resourceId: "docId",
                body: operationType == OperationType.Query
                    ? new MemoryStream(Encoding.UTF8.GetBytes("{\"query\":\"SELECT * FROM c\"}"))
                    : null,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);
            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, "[\"SamplePk\"]");
            request.Headers.Set(HttpConstants.HttpHeaders.Authorization, "test-authorization");
            request.Headers.Set(HttpConstants.HttpHeaders.SessionToken, "0:1#100");
            if (operationType != OperationType.Read)
            {
                request.Headers.Remove(HttpConstants.HttpHeaders.PartitionKey);
                request.Headers.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString());
                request.Headers.Set(HttpConstants.HttpHeaders.StartEpk, "1A2B");
                request.Headers.Set(HttpConstants.HttpHeaders.EndEpk, "3C4D");
                request.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange
                {
                    MinInclusive = string.Empty,
                    MaxExclusive = "FF",
                };
            }

            if (operationType == OperationType.ReadFeed)
            {
                request.Headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);
            }

            System.Diagnostics.Trace.CorrelationManager.ActivityId = activityId;
            try
            {
                await storeClient.InvokeAsync(
                    request,
                    ResourceType.Document,
                    new Uri(operationType == OperationType.Read
                        ? "https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc"
                        : "https://localhost/dbs/TestDb/colls/TestColl/docs"),
                    new Uri("https://proxy.localhost/"),
                    "MockAccount",
                    this.CreateClientCollectionCacheMock().Object,
                    CancellationToken.None);
                Assert.AreEqual(2, attempts, "The HTTP 408 response should trigger one retry.");
                Assert.AreEqual("test-authorization", request.Headers[HttpConstants.HttpHeaders.Authorization]);
                Assert.AreEqual("0:1#100", request.Headers[HttpConstants.HttpHeaders.SessionToken]);
                Assert.IsNull(request.Headers[ThinClientConstants.ProxyOperationType]);
            }
            finally
            {
                System.Diagnostics.Trace.CorrelationManager.ActivityId = previousActivityId;
            }
        }

        private static RntbdConstants.Request ParseProxyRequest(byte[] payload)
        {
            Rntbd.BytesDeserializer reader = new Rntbd.BytesDeserializer(payload, BitConverter.ToInt32(payload, 0));
            reader.ReadBytes(24);
            RntbdConstants.Request request = new RntbdConstants.Request();
            // Proxy requests omit the replica path and transport request ID used by direct TCP requests.
            foreach (RntbdConstants.RequestIdentifiers optionalIdentifier in new[]
            {
                RntbdConstants.RequestIdentifiers.ReplicaPath,
                RntbdConstants.RequestIdentifiers.TransportRequestID,
            })
            {
                int index = (int)optionalIdentifier;
                request.tokens[index] = new RntbdToken(false, request.tokens[index].GetTokenType(), (ushort)optionalIdentifier);
            }

            request.ParseFrom(ref reader);
            return request;
        }

        [TestMethod]
        public async Task ConvertProxyResponseAsync_ShouldThrowIfStatusMismatch()
        {
            // Arrange
            HttpResponseMessage httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InternalServerErrorException>(() =>
                ThinClientTransportSerializer.ConvertProxyResponseAsync(httpResponse));
        }

        [TestMethod]
        public async Task ConvertProxyResponseAsync_ShouldReturnHttpResponse_WhenValid()
        {
            // Arrange
            HttpResponseMessage httpResponse = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(base64MockResponse))
            };

            // Act
            HttpResponseMessage converted = await ThinClientTransportSerializer.ConvertProxyResponseAsync(httpResponse);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, converted.StatusCode, "Expected matched 201 status code.");
            Assert.IsNotNull(converted.Content, "Converted response should have content.");

            Assert.IsTrue(
                converted.Headers.Any(h => h.Key == ThinClientConstants.RoutedViaProxy),
                "Expected 'x-ms-thinclient-route-via-proxy' header to be set in the converted response.");
        }

        private ContainerProperties GetMockContainerProperties()
        {
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/pk" }
                }
            };

            typeof(ContainerProperties)
                .GetProperty("ResourceId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(containerProperties, "-Jlvm9pqHGk=");

            return containerProperties;
        }

        /// <summary>
        /// Verifies that when the <c>x-ms-cosmos-read-consistency-strategy</c> header is set on the
        /// outbound <see cref="HttpRequestMessage"/>, the RNTBD payload built by
        /// <see cref="ThinClientTransportSerializer.SerializeProxyRequestAsync"/> contains the
        /// corresponding RNTBD token. Token layout for byte-typed RNTBD identifiers is
        /// <c>[identifier (uint16 LE)][type (1 byte)][value (1 byte)]</c>. ReadConsistencyStrategy
        /// has identifier <c>254</c> (<c>0x00FE</c>) and type <c>Byte</c> (<c>0x00</c>).
        /// </summary>
        [TestMethod]
        [DataRow("Eventual", (byte)1, DisplayName = "Eventual -> 1")]
        [DataRow("Session", (byte)2, DisplayName = "Session -> 2")]
        [DataRow("LatestCommitted", (byte)3, DisplayName = "LatestCommitted -> 3")]
        [DataRow("GlobalStrong", (byte)4, DisplayName = "GlobalStrong -> 4")]
        public async Task SerializeProxyRequestAsync_ShouldEncodeReadConsistencyStrategy(string strategyName, byte expectedRntbdValue)
        {
            byte[] payload = await this.SerializeWithExtraHeadersAsync(
                new Dictionary<string, string>
                {
                    { HttpConstants.HttpHeaders.ReadConsistencyStrategy, strategyName }
                });

            byte[] expectedToken = { 0xFE, 0x00, 0x00, expectedRntbdValue };

            Assert.IsTrue(
                ContainsSequence(payload, expectedToken),
                $"Expected to find ReadConsistencyStrategy RNTBD token bytes [FE 00 00 {expectedRntbdValue:X2}] for strategy '{strategyName}' in the serialized proxy request payload.");
        }

        private async Task<byte[]> SerializeWithExtraHeadersAsync(
            IDictionary<string, string> extraHeaders,
            OperationType operationType = OperationType.Read)
        {
            HttpRequestMessage message = new HttpRequestMessage
            {
                Content = new StringContent("{\"id\":\"item\"}"),
                RequestUri = new Uri("https://localhost/dbs/TestDb/colls/TestColl/docs/TestDoc")
            };
            message.Headers.Add(ThinClientConstants.ProxyOperationType, operationType.ToString());
            message.Headers.Add(ThinClientConstants.ProxyResourceType, ResourceType.Document.ToString());
            message.Headers.TryAddWithoutValidation(HttpConstants.HttpHeaders.PartitionKey, "[\"SamplePk\"]");
            message.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            foreach (KeyValuePair<string, string> header in extraHeaders)
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Mock<ClientCollectionCache> clientCollectionCacheMock = this.CreateClientCollectionCacheMock();

            ThinClientTransportSerializer.BufferProviderWrapper bufferProvider = new();

            using Stream resultStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                bufferProvider,
                "MockAccount",
                clientCollectionCacheMock.Object,
                message);

            Assert.IsTrue(resultStream.CanSeek, "Expected a seekable stream.");
            resultStream.Position = 0;

            using MemoryStream copy = new MemoryStream();
            await resultStream.CopyToAsync(copy);
            return copy.ToArray();
        }

        private Mock<ClientCollectionCache> CreateClientCollectionCacheMock()
        {
            Mock<ISessionContainer> sessionContainerMock = new Mock<ISessionContainer>();
            Mock<IStoreModel> storeModelMock = new Mock<IStoreModel>();
            Mock<ICosmosAuthorizationTokenProvider> tokenProviderMock = new Mock<ICosmosAuthorizationTokenProvider>();
            Mock<IRetryPolicyFactory> retryPolicyFactoryMock = new Mock<IRetryPolicyFactory>();
            TelemetryToServiceHelper telemetry = null;

            Mock<ClientCollectionCache> clientCollectionCacheMock = new Mock<ClientCollectionCache>(
                sessionContainerMock.Object,
                storeModelMock.Object,
                tokenProviderMock.Object,
                retryPolicyFactoryMock.Object,
                telemetry,
                true,
                null)
            {
                CallBase = true
            };

            clientCollectionCacheMock
                .Setup(c => c.ResolveCollectionAsync(
                    It.IsAny<DocumentServiceRequest>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<ITrace>()))
                .ReturnsAsync(this.GetMockContainerProperties());

            return clientCollectionCacheMock;
        }

        private RequestNameValueCollection CreateEpkRangeHeaders()
        {
            RequestNameValueCollection headers = new RequestNameValueCollection();
            headers.Set(ThinClientConstants.ProxyStartEpk, string.Empty);
            headers.Set(ThinClientConstants.ProxyEndEpk, "FF");
            return headers;
        }

        private void AssertEpkRange(
            INameValueCollection headers,
            Dictionary<string, object> properties,
            string expectedStartEpk,
            string expectedEndEpk)
        {
            Assert.AreEqual(
                RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKeyRange.ToString(),
                headers[HttpConstants.HttpHeaders.ReadFeedKeyType]);
            Assert.AreEqual(expectedStartEpk, headers[HttpConstants.HttpHeaders.StartEpk]);
            Assert.AreEqual(expectedEndEpk, headers[HttpConstants.HttpHeaders.EndEpk]);
            CollectionAssert.AreEqual(
                HexStringUtility.HexStringToBytes(expectedStartEpk),
                (byte[])properties[WFConstants.BackendHeaders.StartEpkHash]);
            CollectionAssert.AreEqual(
                HexStringUtility.HexStringToBytes(expectedEndEpk),
                (byte[])properties[WFConstants.BackendHeaders.EndEpkHash]);
        }

        private static bool ContainsSequence(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
            {
                return false;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
