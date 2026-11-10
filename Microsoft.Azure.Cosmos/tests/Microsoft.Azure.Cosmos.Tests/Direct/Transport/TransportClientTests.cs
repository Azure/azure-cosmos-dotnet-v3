// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Reflection;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry;
    using Microsoft.Azure.Cosmos.Test.OpenTelemetryUtils;
    using OpenTelemetry.Trace;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.Telemetry;

    [TestClass]
    [DoNotParallelize]
    public class TrasnportClientTests
    {
        private bool originalActivitySourceEnabled;

        [TestInitialize]
        public void TestInitialize()
        {
            AppContext.TryGetSwitch("Azure.Experimental.EnableActivitySource", out this.originalActivitySourceEnabled);
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            CustomOtelExporter.ResetData();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", this.originalActivitySourceEnabled);
            CustomOtelExporter.ResetData();
        }

        [TestMethod]
        [Owner("askagarw")]
        public async Task TransportExceptionsContainsSubStatus()
        {
            Rntbd.TransportClient transportClient = new Rntbd.TransportClient(new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1)));

            // Setup channel to throw
            Mock<IChannel> channel = new Mock<IChannel>();
            channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                              It.IsAny<TransportAddressUri>(),
                                              It.IsAny<ResourceOperation>(),
                                              It.IsAny<Guid>(),
                                              It.IsAny<TransportRequestStats>()))
                   .ThrowsAsync(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true));

            // Setup Channel Dictionary
            ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(transportClient);

            ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(channelDict);

            allChannels.TryAdd(new ServerKey(new Uri("http://one.com")), channel.Object);

            DocumentClientException dce = await Assert.ThrowsExceptionAsync<ServiceUnavailableException>(() =>
                            transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                            ResourceOperation.CreateDocument,
                            DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));

            // 503 generated because request is non-retriable and exception is not a timeout
            Assert.AreEqual(dce.StatusCode, System.Net.HttpStatusCode.ServiceUnavailable);
            Assert.AreEqual(dce.GetSubStatus(), SubStatusCodes.Channel_Closed);

            dce = await Assert.ThrowsExceptionAsync<GoneException>(() =>
                            transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                            ResourceOperation.ReadDocument,
                            DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));

            Assert.AreEqual(dce.StatusCode, System.Net.HttpStatusCode.Gone);
            Assert.AreEqual(dce.GetSubStatus(), SubStatusCodes.TransportGenerated410);

            // Setup channel for service generated exception
            Mock<IChannel> serviceExceptionChannel = new Mock<IChannel>();
            serviceExceptionChannel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                              It.IsAny<TransportAddressUri>(),
                                              It.IsAny<ResourceOperation>(),
                                              It.IsAny<Guid>(),
                                              It.IsAny<TransportRequestStats>()))
                .ReturnsAsync(new StoreResponse()
                {
                    Status = 503
                });

            allChannels.TryAdd(new ServerKey(new Uri("http://two.com")), serviceExceptionChannel.Object);

            dce = await Assert.ThrowsExceptionAsync<ServiceUnavailableException>(() =>
                            transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://two.com")),
                            ResourceOperation.ReadDocument,
                            DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));
            Assert.AreEqual(dce.StatusCode, System.Net.HttpStatusCode.ServiceUnavailable);
            Assert.AreEqual(dce.GetSubStatus(), SubStatusCodes.ServerGenerated503);
        }

        [TestMethod]
        [Owner("naveenv")]
        public async Task ForbiddenExceptionPreservesSubStatus()
        {
            Rntbd.TransportClient transportClient = new Rntbd.TransportClient(new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1)));

            // Setup channel that returns 403 with SubStatus 5314 (NspS2SServiceTagDenied)
            StoreResponseNameValueCollection forbiddenHeaders = new StoreResponseNameValueCollection();
            forbiddenHeaders.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.NspS2SServiceTagDenied).ToString());

            Mock<IChannel> forbiddenChannel = new Mock<IChannel>();
            forbiddenChannel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                              It.IsAny<TransportAddressUri>(),
                                              It.IsAny<ResourceOperation>(),
                                              It.IsAny<Guid>(),
                                              It.IsAny<TransportRequestStats>()))
                .ReturnsAsync(new StoreResponse()
                {
                    Status = (int)StatusCodes.Forbidden,
                    Headers = forbiddenHeaders
                });

            ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(transportClient);

            ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(channelDict);

            allChannels.TryAdd(new ServerKey(new Uri("http://forbidden-substatus.com")), forbiddenChannel.Object);

            // Verify ForbiddenException is thrown with correct substatus
            DocumentClientException dce = await Assert.ThrowsExceptionAsync<ForbiddenException>(() =>
                            transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://forbidden-substatus.com")),
                            ResourceOperation.ReadDocument,
                            DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));
            Assert.AreEqual(System.Net.HttpStatusCode.Forbidden, dce.StatusCode);
            Assert.AreEqual(SubStatusCodes.NspS2SServiceTagDenied, dce.GetSubStatus());

            // Setup channel that returns 403 WITHOUT SubStatus (original behavior)
            Mock<IChannel> forbiddenNoSubStatusChannel = new Mock<IChannel>();
            forbiddenNoSubStatusChannel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                              It.IsAny<TransportAddressUri>(),
                                              It.IsAny<ResourceOperation>(),
                                              It.IsAny<Guid>(),
                                              It.IsAny<TransportRequestStats>()))
                .ReturnsAsync(new StoreResponse()
                {
                    Status = (int)StatusCodes.Forbidden
                });

            allChannels.TryAdd(new ServerKey(new Uri("http://forbidden-no-substatus.com")), forbiddenNoSubStatusChannel.Object);

            // Verify ForbiddenException without substatus maintains default behavior
            dce = await Assert.ThrowsExceptionAsync<ForbiddenException>(() =>
                            transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://forbidden-no-substatus.com")),
                            ResourceOperation.ReadDocument,
                            DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));
            Assert.AreEqual(System.Net.HttpStatusCode.Forbidden, dce.StatusCode);
            Assert.AreEqual(SubStatusCodes.Unknown, dce.GetSubStatus());
        }

        [TestMethod]
        [Owner("askagarw")]
        public void TransportClientThrowServerExceptionTest()
        {
            StoreResponseNameValueCollection responseHeaders = new StoreResponseNameValueCollection();
            StoreResponse storeResponse = new StoreResponse()
            {
                Headers = responseHeaders
            };

            // Service Generated 410
            storeResponse.Status = 410;
            DocumentClientException ex = Assert.ThrowsException<GoneException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreEqual(ex.GetSubStatus(), SubStatusCodes.ServerGenerated410);

            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "1002");
            ex = Assert.ThrowsException<PartitionKeyRangeGoneException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreEqual(ex.GetSubStatus(), SubStatusCodes.PartitionKeyRangeGone);

            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "1007");
            ex = Assert.ThrowsException<PartitionKeyRangeIsSplittingException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreEqual(ex.GetSubStatus(), SubStatusCodes.CompletingSplit);

            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "1008");
            ex = Assert.ThrowsException<PartitionIsMigratingException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreEqual(ex.GetSubStatus(), SubStatusCodes.CompletingPartitionMigration);

            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "1000");
            ex = Assert.ThrowsException<InvalidPartitionException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreEqual(ex.GetSubStatus(), SubStatusCodes.NameCacheIsStale);

            // Sub Status from backend is not overwritten
            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "834721");
            ex = Assert.ThrowsException<GoneException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreNotEqual(ex.GetSubStatus(), SubStatusCodes.ServerGenerated410);
            string substatus = responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
            Assert.AreEqual(substatus, "834721");

            // 503 with SubStatus
            storeResponse.Status = 503;
            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "12345");
            ex = Assert.ThrowsException<ServiceUnavailableException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreNotEqual(ex.GetSubStatus(), SubStatusCodes.ServerGenerated503);
            substatus = responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
            Assert.AreEqual(substatus, "12345");

            // 400 with no SubStatus != SubStatusCodes.PartitionKeyMismatch
            storeResponse.Status = 400;
            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, "12345");
            ex = Assert.ThrowsException<BadRequestException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreNotEqual(ex.GetSubStatus(), SubStatusCodes.ServerGenerated503);
            substatus = responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
            Assert.AreEqual(substatus, "12345");

            // 400 with no SubStatus == SubStatusCodes.PartitionKeyMismatch
            storeResponse.Status = 400;
            responseHeaders.Set(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.PartitionKeyMismatch).ToString());
            ex = Assert.ThrowsException<BadRequestException>(() => TransportClient.ThrowServerException("http://one.com", storeResponse,
                                                         new Uri("http://one.com"), Guid.Empty));
            Assert.AreNotEqual(ex.GetSubStatus(), SubStatusCodes.ServerGenerated503);
            substatus = responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
            Assert.AreEqual(substatus, ((int)SubStatusCodes.PartitionKeyMismatch).ToString());
        }

        [TestMethod]
        [Owner("aavasthy")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WithDistributedTracingEnabled_InvokeAsync_RecordsActivity(bool customPropagator)
        {
            //Arrange
            CustomOtelExporter exporter = new CustomOtelExporter();

            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"Azure.Cosmos.Request")
                .Build())
            {
                ActivitySource activitySource = new ActivitySource("Azure.Cosmos.Request");
                Activity parentActivity = activitySource.StartActivity("ParentActivity");

                DistributedTracingOptions dtOptions = new DistributedTracingOptions()
                {
                    IsDistributedTracingEnabled = true
                };

                if (customPropagator)
                {
                    dtOptions.Propagator = new CustomCosmosDistributedContextPropagator();
                }

                Rntbd.TransportClient.Options options = new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1))
                {
                    DistributedTracingOptions = dtOptions
                };

                Rntbd.TransportClient transportClient = new Rntbd.TransportClient(options);

                // Setup channel to throw
                Mock<IChannel> channel = new Mock<IChannel>();
                channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                                  It.IsAny<TransportAddressUri>(),
                                                  It.IsAny<ResourceOperation>(),
                                                  It.IsAny<Guid>(),
                                                  It.IsAny<TransportRequestStats>()))
                                    .ReturnsAsync(new StoreResponse()
                                    {
                                        Status = 200
                                    });

                // Setup Channel Dictionary
                ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                    .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportClient);

                ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                allChannels.TryAdd(new ServerKey(new Uri("http://one.com/with/path")), channel.Object);

                DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
                ////Act
                StoreResponse storeResponse = await transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com/with/path")),
                              ResourceOperation.CreateDocument,
                              request);

                parentActivity.Stop();

                //Assert
                if (customPropagator)
                {
                    Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "traceparent") < 0);
                    Assert.IsNotNull(request.Headers.Get("custom-traceparent"));
                }
                else
                {
                    Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "custom-traceparent") < 0);
                    Assert.IsNotNull(request.Headers.Get("traceparent"));
                }

                List<Activity> exportData = exporter.ExportData();

                Assert.AreEqual(2, exportData.Count);
                foreach (Activity activity in exportData)
                {
                    Assert.AreEqual(ActivityIdFormat.W3C, activity.IdFormat);

                    if (activity.OperationName == "RequestAsync")
                    {
                        Assert.AreEqual(parentActivity.Id, activity.ParentId);
                        Assert.AreEqual("http://one.com/with/path", activity.TagObjects.First(x => x.Key == "rntbd.url").Value);
                        Assert.AreEqual(200, Convert.ToInt32(activity.TagObjects.First(x => x.Key == "rntbd.status_code").Value));
                        Assert.AreEqual(0, Convert.ToInt32(activity.TagObjects.First(x => x.Key == "rntbd.sub_status_code").Value));
                        Assert.AreEqual("Create Document", activity.DisplayName);
                        Assert.AreNotEqual(ActivityStatusCode.Error, activity.Status);
                    }
                    else
                    {
                        Assert.AreEqual("ParentActivity", parentActivity.OperationName);
                    }
                }
            }
        }

        [TestMethod]
        [Owner("cosmosdb_devplat_idc")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WithDistributedTracingEnabled_InvokeAsync_RecordsActivity_WithAlwaysOffSampler(bool customPropagator)
        {
            //Arrange
            CustomOtelExporter exporter = new CustomOtelExporter();

            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .SetSampler(new AlwaysOffSampler())
                .AddSource($"Azure.Cosmos.Request")
                .Build())
            {
                DistributedTracingOptions dtOptions = new DistributedTracingOptions()
                {
                    IsDistributedTracingEnabled = true
                };

                if (customPropagator)
                {
                    dtOptions.Propagator = new CustomCosmosDistributedContextPropagator();
                }

                Rntbd.TransportClient.Options options = new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1))
                {
                    DistributedTracingOptions = dtOptions
                };

                Rntbd.TransportClient transportClient = new Rntbd.TransportClient(options);

                // Setup channel to throw
                Mock<IChannel> channel = new Mock<IChannel>();
                channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                                  It.IsAny<TransportAddressUri>(),
                                                  It.IsAny<ResourceOperation>(),
                                                  It.IsAny<Guid>(),
                                                  It.IsAny<TransportRequestStats>()))
                                    .ReturnsAsync(new StoreResponse()
                                    {
                                        Status = 200
                                    });

                ActivitySource activitySource = new ActivitySource("Azure.Cosmos.Request");
                Activity parentActivity = activitySource.StartActivity("ParentActivity");

                // Setup Channel Dictionary
                ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                    .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportClient);

                ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                allChannels.TryAdd(new ServerKey(new Uri("http://one.com/")), channel.Object);

                DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);
                ////Act
                StoreResponse storeResponse = await transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                              ResourceOperation.CreateDocument,
                              request);

                parentActivity.Stop();

                //Assert
                List<Activity> exportData = exporter.ExportData();

                if (customPropagator)
                {
                    Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "traceparent") < 0);
                    Assert.IsNotNull(request.Headers.Get("custom-traceparent"));
                }
                else
                {
                    Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "custom-traceparent") < 0);
                    Assert.IsNotNull(request.Headers.Get("traceparent"));
                }

                Assert.AreEqual(0, exportData.Count);
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WithDistributedTracingEnabled_InvokeAsync_NoListenerAtNetworkLevel_NoNetworkActivityCreated(bool customPropagator)
        {
            //Arrange
            CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"Azure.Cosmos.Operation")
                .Build())
            {
                DistributedTracingOptions dtOptions = new DistributedTracingOptions()
                {
                    IsDistributedTracingEnabled = true
                };

                if (customPropagator)
                {
                    dtOptions.Propagator = new CustomCosmosDistributedContextPropagator();
                }

                Rntbd.TransportClient.Options options = new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1))
                {
                    DistributedTracingOptions = dtOptions
                };

                Rntbd.TransportClient transportClient = new Rntbd.TransportClient(options);

                // Setup channel to throw
                Mock<IChannel> channel = new Mock<IChannel>();
                channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                                  It.IsAny<TransportAddressUri>(),
                                                  It.IsAny<ResourceOperation>(),
                                                  It.IsAny<Guid>(),
                                                  It.IsAny<TransportRequestStats>()))
                                    .ReturnsAsync(new StoreResponse()
                                    {
                                        Status = 200
                                    });

                ActivitySource activitySource = new ActivitySource("Azure.Cosmos.Operation");
                Activity parentActivity = activitySource.StartActivity("ParentActivity");

                // Setup Channel Dictionary
                ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                    .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportClient);

                ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                allChannels.TryAdd(new ServerKey(new Uri("http://one.com/")), channel.Object);

                DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

                //Act
                StoreResponse storeResponse = await transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                              ResourceOperation.CreateDocument,
                              request);

                parentActivity.Stop();

                //Assert
                List<Activity> exportData = exporter.ExportData();

                if (customPropagator)
                {
                    Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "traceparent") < 0);
                    Assert.IsNotNull(request.Headers.Get("custom-traceparent"));
                }
                else
                {
                    Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "custom-traceparent") < 0);
                    Assert.IsNotNull(request.Headers.Get("traceparent"));
                }

                Assert.AreEqual(1, exportData.Count);
                Assert.AreEqual(parentActivity.Id, exportData[0].Id);
                Assert.AreEqual("ParentActivity", exportData[0].OperationName);
                Assert.AreEqual("ParentActivity", exportData[0].DisplayName);
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WithDistributedTracingDisabled_InvokeAsync_NoActivity(bool customPropagator)
        {
            //Arrange
            CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"Azure.Cosmos.Request")
                .Build())
            {
                DistributedTracingOptions dtOptions = new DistributedTracingOptions()
                {
                    IsDistributedTracingEnabled = false
                };

                if (customPropagator)
                {
                    dtOptions.Propagator = new CustomCosmosDistributedContextPropagator();
                }

                Rntbd.TransportClient.Options options = new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1))
                {
                    DistributedTracingOptions = dtOptions
                };

                Rntbd.TransportClient transportClient = new Rntbd.TransportClient(options);

                // Setup channel to throw
                Mock<IChannel> channel = new Mock<IChannel>();
                channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                                  It.IsAny<TransportAddressUri>(),
                                                  It.IsAny<ResourceOperation>(),
                                                  It.IsAny<Guid>(),
                                                  It.IsAny<TransportRequestStats>()))
                                    .ReturnsAsync(new StoreResponse()
                                    {
                                        Status = 200
                                    });

                // Setup Channel Dictionary
                ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                    .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportClient);

                ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                allChannels.TryAdd(new ServerKey(new Uri("http://one.com/")), channel.Object);

                DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey);

                //Act
                StoreResponse storeResponse = await transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                              ResourceOperation.CreateDocument,
                              request);

                //Assert
                List<Activity> exportData = exporter.ExportData();

                Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "custom-traceparent") < 0);
                Assert.IsTrue(Array.BinarySearch(request.Headers.AllKeys(), "traceparent") < 0);

                Assert.AreEqual(0, exportData.Count);
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task InvokeAsync_ThrowsTransportException_RecordsExceptionActivity()
        {
            //Arrange
            CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"Azure.Cosmos.Request")
                .Build())
            {
                Rntbd.TransportClient.Options options = new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1))
                {
                    DistributedTracingOptions = new DistributedTracingOptions()
                    {
                        IsDistributedTracingEnabled = true
                    }
                };

                Rntbd.TransportClient transportClient = new Rntbd.TransportClient(options);

                // Setup channel to throw
                Mock<IChannel> channel = new Mock<IChannel>();
                channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                                  It.IsAny<TransportAddressUri>(),
                                                  It.IsAny<ResourceOperation>(),
                                                  It.IsAny<Guid>(),
                                                  It.IsAny<TransportRequestStats>()))
                       .ThrowsAsync(new TransportException(TransportErrorCode.ConnectFailed, null, Guid.NewGuid(), new Uri("http://one.com"), "description", true, true));

                // Setup Channel Dictionary
                ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                    .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportClient);

                ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                allChannels.TryAdd(new ServerKey(new Uri("http://one.com")), channel.Object);

                DocumentClientException dce = await Assert.ThrowsExceptionAsync<ServiceUnavailableException>(() =>
                                transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                                ResourceOperation.CreateDocument,
                                DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));

                //Assert
                // 503 generated because request is non-retriable and exception is not a timeout
                Assert.AreEqual(System.Net.HttpStatusCode.ServiceUnavailable, dce.StatusCode);

                List<Activity> exportData = exporter.ExportData();

                Assert.IsTrue(exportData.Count > 0, "No activity is generated");

                foreach (Activity activity in exportData)
                {
                    Assert.AreEqual("http://one.com", activity.TagObjects.First(x => x.Key == "rntbd.url").Value);
                    Assert.AreEqual(503, Convert.ToInt32(activity.TagObjects.First(x => x.Key == "rntbd.status_code").Value));
                    Assert.AreEqual("Microsoft.Azure.Documents.ServiceUnavailableException", activity.TagObjects.First(x => x.Key == "exception.type").Value);
                    Assert.IsNotNull(activity.TagObjects.First(x => x.Key == "exception.message"));
                    Assert.IsNotNull(activity.TagObjects.First(x => x.Key == "rntbd.sub_status_code"));
                    Assert.AreEqual("Request.RequestAsync", activity.OperationName);
                    Assert.AreEqual("Create Document", activity.DisplayName);
                    Assert.AreEqual(ActivityStatusCode.Error, activity.Status);
                }
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        public async Task InvokeAsync_ThrowServerExceptionBadRequest_RecordsExceptionActivityOnce()
        {
            //Arrange
            CustomOtelExporter exporter = new CustomOtelExporter();
            using (TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter()
                .AddSource($"Azure.Cosmos.Request")
                .Build())
            {
                Rntbd.TransportClient.Options options = new Rntbd.TransportClient.Options(TimeSpan.FromMilliseconds(1))
                {
                    DistributedTracingOptions = new DistributedTracingOptions()
                    {
                        IsDistributedTracingEnabled = true
                    }
                };

                Rntbd.TransportClient transportClient = new Rntbd.TransportClient(options);

                // Setup channel to throw
                Mock<IChannel> channel = new Mock<IChannel>();
                channel.Setup(x => x.RequestAsync(It.IsAny<DocumentServiceRequest>(),
                                                It.IsAny<TransportAddressUri>(),
                                                It.IsAny<ResourceOperation>(),
                                                It.IsAny<Guid>(),
                                                It.IsAny<TransportRequestStats>()))
                                  .ReturnsAsync(new StoreResponse()
                                  {
                                      Status = 400
                                  });

                // Setup Channel Dictionary
                ChannelDictionary channelDict = (ChannelDictionary)transportClient.GetType()
                    .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(transportClient);

                ConcurrentDictionary<ServerKey, IChannel> allChannels = (ConcurrentDictionary<ServerKey, IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                allChannels.TryAdd(new ServerKey(new Uri("http://one.com")), channel.Object);

                DocumentClientException dce = await Assert.ThrowsExceptionAsync<BadRequestException>(() =>
                                transportClient.InvokeStoreAsync(new TransportAddressUri(new Uri("http://one.com")),
                                ResourceOperation.CreateDocument,
                                DocumentServiceRequest.Create(OperationType.Create, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey)));

                List<Activity> exportData = exporter.ExportData();

                Assert.IsTrue(exportData.Count > 0, "No activity is generated");

                foreach (Activity activity in exportData)
                {
                    Assert.AreEqual("http://one.com", activity.TagObjects.First(x => x.Key == "rntbd.url").Value);
                    Assert.AreEqual(1, activity.TagObjects.Where(x => x.Key == "rntbd.url").Count());

                }
            }
        }

    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class CustomCosmosDistributedContextPropagator : CosmosDistributedContextPropagatorBase
#pragma warning restore SA1402 // File may only contain a single type
    {
        internal override void Inject(Activity activity, INameValueCollection headers) => headers.Set("custom-traceparent", activity?.Id);
    }
}