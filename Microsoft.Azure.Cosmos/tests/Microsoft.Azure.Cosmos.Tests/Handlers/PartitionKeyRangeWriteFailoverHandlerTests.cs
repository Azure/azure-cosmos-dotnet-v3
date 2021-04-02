//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class PartitionKeyRangeWriteFailoverHandlerTests
    {
        [TestMethod]
        public void TryCreateTests()
        {
            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>();
            Lazy<IAddressResolver> lazyAddressResolver = new Lazy<IAddressResolver>(() => mockAddressResolver.Object);

            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                () => new List<Uri>() { new Uri("") },
                lazyAddressResolver,
                null,
                ConnectionMode.Direct,
                out RequestHandler requestHandler));

            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
               () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
               () => new List<Uri>() { new Uri("") },
               lazyAddressResolver,
               Cosmos.ConsistencyLevel.Strong,
               ConnectionMode.Direct,
               out requestHandler));

            Assert.IsFalse(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                  () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                  () => new List<Uri>() { new Uri("") },
                  lazyAddressResolver,
                  Cosmos.ConsistencyLevel.Strong,
                  ConnectionMode.Gateway,
                  out requestHandler));

            foreach (Cosmos.ConsistencyLevel consistencyLevel in Enum.GetValues(typeof(Cosmos.ConsistencyLevel)).Cast<Cosmos.ConsistencyLevel>().Where(x => x != Cosmos.ConsistencyLevel.Strong))
            {
                Assert.IsFalse(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                   () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                   () => new List<Uri>() { new Uri("") },
                   lazyAddressResolver,
                   consistencyLevel,
                   ConnectionMode.Direct,
                   out requestHandler));

                Assert.IsFalse(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                   () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                   () => new List<Uri>() { new Uri("") },
                   lazyAddressResolver,
                   consistencyLevel,
                   ConnectionMode.Gateway,
                   out requestHandler));
            }
        }

        [TestMethod]
        public async Task PositiveScenarioAsync()
        {
            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>();
            Lazy<IAddressResolver> lazyAddressResolver = new Lazy<IAddressResolver>(() => mockAddressResolver.Object);
            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                () => throw new Exception("Shouldn't be checking consistency"),
                () => throw new Exception("Shouldn't be getting URI list"),
                lazyAddressResolver,
                null,
                ConnectionMode.Direct,
                out RequestHandler requestHandler));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Put,
                "someRequestUri",
                NoOpTrace.Singleton);

            Mock<RequestHandler> mockRequestHandler = new Mock<RequestHandler>();
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Created, requestMessage);
            mockRequestHandler.Setup(x => x.SendAsync(requestMessage, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            requestHandler.InnerHandler = mockRequestHandler.Object;

            ResponseMessage responseFromHandler = await requestHandler.SendAsync(
                requestMessage,
                default);

            Assert.AreEqual(responseMessage, responseFromHandler);
        }

        [TestMethod]
        public async Task RetryScenarioAsync()
        {
            List<Uri> regions = new List<Uri>() { new Uri("https://FirstRegion"), new Uri("https://SecondRegion") };
            this.MockAndCreatePartitionKeyRangeWriteFailoverHandler(
                regions,
                out RequestHandler requestHandler);

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Put,
                "/dbs/testdb/colls/testColl/docs/123",
                NoOpTrace.Singleton)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Create
            };

            DocumentServiceRequest documentServiceRequest = requestMessage.ToDocumentServiceRequest();
            documentServiceRequest.RequestContext.RouteToLocation(regions[0]);

            Mock<RequestHandler> mockRequestHandler = new Mock<RequestHandler>();
            Headers headers = new Headers
            {
                SubStatusCode = SubStatusCodes.WriteForbidden
            };

            ResponseMessage writeForbiddenResponse = new ResponseMessage(
                HttpStatusCode.Forbidden,
                requestMessage,
                headers,
                null,
                NoOpTrace.Singleton);

            ResponseMessage successResponse = new ResponseMessage(HttpStatusCode.Created, requestMessage);

            mockRequestHandler.Setup(x => x.SendAsync(It.Is<RequestMessage>((request) => request.ToDocumentServiceRequest().RequestContext.LocationEndpointToRoute == regions[0]), It.IsAny<CancellationToken>()))
               .Returns(Task.FromResult(writeForbiddenResponse));
            mockRequestHandler.Setup(x => x.SendAsync(It.Is<RequestMessage>((request) => request.ToDocumentServiceRequest().RequestContext.LocationEndpointToRoute == regions[1]), It.IsAny<CancellationToken>()))
               .Returns(Task.FromResult(successResponse));

            requestHandler.InnerHandler = mockRequestHandler.Object;

            ResponseMessage responseFromHandler = await requestHandler.SendAsync(
                requestMessage,
                default);

            Assert.AreEqual(successResponse.StatusCode, responseFromHandler.StatusCode);
        }

        [TestMethod]
        public async Task HttpRetryScenarioAsync()
        {
            List<Uri> regions = new List<Uri>() { new Uri("https://FirstRegion"), new Uri("https://SecondRegion") };
            this.MockAndCreatePartitionKeyRangeWriteFailoverHandler(
                regions,
                out RequestHandler requestHandler);

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Put,
                "/dbs/testdb/colls/testColl/docs/123",
                NoOpTrace.Singleton)
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Create
            };

            DocumentServiceRequest documentServiceRequest = requestMessage.ToDocumentServiceRequest();
            documentServiceRequest.RequestContext.RouteToLocation(regions[0]);

            Mock<RequestHandler> mockRequestHandler = new Mock<RequestHandler>();

            ResponseMessage successResponse = new ResponseMessage(HttpStatusCode.Created, requestMessage);

            mockRequestHandler.Setup(x => x.SendAsync(It.Is<RequestMessage>((request) => request.ToDocumentServiceRequest().RequestContext.LocationEndpointToRoute == regions[0]), It.IsAny<CancellationToken>()))
                       .Throws(new HttpRequestException("mock HttpRequestException"));
            mockRequestHandler.Setup(x => x.SendAsync(It.Is<RequestMessage>((request) => request.ToDocumentServiceRequest().RequestContext.LocationEndpointToRoute == regions[1]), It.IsAny<CancellationToken>()))
                       .Returns(Task.FromResult(successResponse));

            requestHandler.InnerHandler = mockRequestHandler.Object;

            ResponseMessage responseFromHandler = await requestHandler.SendAsync(
                requestMessage,
                default);

            Assert.AreEqual(successResponse.StatusCode, responseFromHandler.StatusCode);
        }

        private void MockAndCreatePartitionKeyRangeWriteFailoverHandler(
            List<Uri> regions,
            out RequestHandler requestHandler)
        {
            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>();
            Lazy<IAddressResolver> lazyAddressResolver = new Lazy<IAddressResolver>(() => mockAddressResolver.Object);

            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                () => regions,
                lazyAddressResolver,
                null,
                ConnectionMode.Direct,
                out requestHandler));

            mockAddressResolver.Setup(x => x.ResolveAsync(
                 It.IsAny<DocumentServiceRequest>(),
                 false,
                 It.IsAny<CancellationToken>())).Returns<DocumentServiceRequest, bool, CancellationToken>((x, y, z) =>
                 {
                     x.RequestContext.ResolvedPartitionKeyRange = new PartitionKeyRange()
                     {
                         Id = "0",
                         MinInclusive = "00",
                         MaxExclusive = "FF",
                     };
                     return Task.FromResult(new PartitionAddressInformation(new AddressInformation[]
                     {
                        new AddressInformation()
                        {
                            IsPrimary = true,
                            IsPublic = true,
                            PhysicalUri = "https://FirstPysicalUri",
                            Protocol = Documents.Client.Protocol.Tcp
                        }
                     }));
                 });
        }
    }
}
