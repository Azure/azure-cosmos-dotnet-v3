//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class PartitionKeyRangeWriteFailoverHandlerTests
    {
        [TestMethod]
        public void TryCreateTests()
        {
            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                () => new List<Uri>() { new Uri("") },
                new Mock<IAddressResolver>().Object,
                null,
                ConnectionMode.Direct,
                out RequestHandler requestHandler));

            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
               () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
               () => new List<Uri>() { new Uri("") },
               new Mock<IAddressResolver>().Object,
               Cosmos.ConsistencyLevel.Strong,
               ConnectionMode.Direct,
               out requestHandler));

            Assert.IsFalse(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                  () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                  () => new List<Uri>() { new Uri("") },
                  new Mock<IAddressResolver>().Object,
                  Cosmos.ConsistencyLevel.Strong,
                  ConnectionMode.Gateway,
                  out requestHandler));

            foreach (Cosmos.ConsistencyLevel consistencyLevel in Enum.GetValues(typeof(Cosmos.ConsistencyLevel)).Cast<Cosmos.ConsistencyLevel>().Where(x => x != Cosmos.ConsistencyLevel.Strong))
            {
                Assert.IsFalse(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                   () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                   () => new List<Uri>() { new Uri("") },
                   new Mock<IAddressResolver>().Object,
                   consistencyLevel,
                   ConnectionMode.Direct,
                   out requestHandler));

                Assert.IsFalse(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                   () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                   () => new List<Uri>() { new Uri("") },
                   new Mock<IAddressResolver>().Object,
                   consistencyLevel,
                   ConnectionMode.Gateway,
                   out requestHandler));
            }
        }

        [TestMethod]
        public async Task PositiveScenarioAsync()
        {
            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                () => throw new Exception("Shouldn't be checking consistency"),
                () => throw new Exception("Shouldn't be getting URI list"),
                new Mock<IAddressResolver>().Object,
                null,
                ConnectionMode.Direct,
                out RequestHandler requestHandler));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Put,
                "someRequestUri",
                new CosmosDiagnosticsContextCore(),
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
            Mock<IAddressResolver> mockAddressResolver = new Mock<IAddressResolver>();
            
            Assert.IsTrue(PartitionKeyRangeWriteFailoverHandler.TryCreate(
                () => Task.FromResult(Cosmos.ConsistencyLevel.Strong),
                () => regions,
                mockAddressResolver.Object,
                null,
                ConnectionMode.Direct,
                out RequestHandler requestHandler));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Put,
                "/dbs/testdb/colls/testColl/docs/123",
                new CosmosDiagnosticsContextCore(),
                NoOpTrace.Singleton)
            { 
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Create
            };

            DocumentServiceRequest documentServiceRequest = requestMessage.ToDocumentServiceRequest();
            documentServiceRequest.RequestContext.RouteToLocation(regions[0]);

            mockAddressResolver.Setup(x => x.ResolveAsync(
                 documentServiceRequest,
                 false,
                 It.IsAny<CancellationToken>())).Returns(Task.FromResult(new PartitionAddressInformation(new AddressInformation[]
                 {
                    new AddressInformation()
                    {
                        IsPrimary = true,
                        IsPublic = true,
                        PhysicalUri = "https://FirstPysicalUri",
                        Protocol = Documents.Client.Protocol.Tcp
                    }

                 })));

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
                requestMessage.DiagnosticsContext);

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
    }
}
