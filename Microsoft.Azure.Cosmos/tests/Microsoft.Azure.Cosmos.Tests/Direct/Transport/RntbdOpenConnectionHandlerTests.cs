//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Rntbd;
    using Microsoft.Azure.Cosmos.Test;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Test class for <see cref="RntbdOpenConnectionHandler"/>.
    /// </summary>
    [TestClass]
    public sealed class RntbdOpenConnectionHandlerTests
    {
        /// <summary>
        /// Test to validate that when a background replica node is healthy/ unhealthy during opening the channel
        /// the corresponding transport address uri is marked as healthy/ unhealthy accordingly.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task TryOpenRntbdChannelsAsync_WithHealthyAndUnhealthyAddressUri_ShouldMarkTransportAddressAccordinly()
        {
            // Arrange.
            Uri backendReplicaUri1 = new(
                uriString: "rntbd://test.azure.com:15211/partitions/2s");
            Uri backendReplicaUri2 = new(
                uriString: "rntbd://test.azure.com:15211/partitions/3s");

            TransportAddressUri backendTransportAddress1 = new(
                addressUri: backendReplicaUri1);
            TransportAddressUri backendTransportAddress2 = new(
                addressUri: backendReplicaUri2);

            Mock<Documents.TransportClient> mockedTransportClient = new();
            mockedTransportClient
                .SetupSequence(x => x.OpenConnectionAsync(It.IsAny<Uri>()))
                .Returns(Task.CompletedTask)
                .Throws(new Exception("Backend replica is not yet in ready state."));

            RntbdOpenConnectionHandler rntbdOpenConnectionHandler = new(mockedTransportClient.Object);
            List<TransportAddressUri> addresses = new()
            {
                backendTransportAddress1,
                backendTransportAddress2,
            };

            // Act.
            await rntbdOpenConnectionHandler.TryOpenRntbdChannelsAsync(addresses);

            // Assert.
            Assert.AreEqual(backendTransportAddress1.GetCurrentHealthState().GetHealthStatus(), TransportAddressHealthState.HealthStatus.Connected);
            Assert.AreEqual(backendTransportAddress2.GetCurrentHealthState().GetHealthStatus(), TransportAddressHealthState.HealthStatus.Unhealthy);
        }

        /// <summary>
        /// Test to validate that when the RntbdOpenConnectionHandler used with-in an using block the
        /// object is indeed disposed after usage.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task RntbdOpenConnectionHandler_WhenInvokedWithinUsing_ShouldDisposeObjectAfterUsage()
        {
            // Arrange.
            Uri backendReplicaUri = new(
                uriString: "rntbd://test.azure.com:15211/partitions/2s");

            TransportAddressUri backendTransportAddress = new(
                addressUri: backendReplicaUri);

            Mock<Documents.TransportClient> mockedTransportClient = new();
            mockedTransportClient
                .Setup(x => x.OpenConnectionAsync(It.IsAny<Uri>()))
                .Returns(Task.CompletedTask);

            RntbdOpenConnectionHandler rntbdOpenConnectionHandler = new(mockedTransportClient.Object);
            List<TransportAddressUri> addresses = new()
            {
                backendTransportAddress,
            };

            FieldInfo fieldInfo = rntbdOpenConnectionHandler
                .GetType()
                .GetField(
                    name: "disposed",
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            // Act.
            using (rntbdOpenConnectionHandler)
            {
                await rntbdOpenConnectionHandler.TryOpenRntbdChannelsAsync(addresses);

                bool fieldValueBeforeDispose = (bool)fieldInfo.GetValue(
                        obj: rntbdOpenConnectionHandler);

                Assert.IsFalse(fieldValueBeforeDispose);
            }

            bool fieldValueAfterDispose = (bool)fieldInfo.GetValue(
                    obj: rntbdOpenConnectionHandler);

            Assert.IsTrue(fieldValueAfterDispose);
        }

        /// <summary>
        /// Test to validate that when there is very less delay in opening the chanels to the backend, then with a high degree of parallism,
        /// only few TCP connections were established to the backend, which are lesser than the number of logical cpu cores.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(0, 600, DisplayName = "Validate that when there is no delay in opening the chanels to the backend, then with a high degree of parallism, only few TCP connections were established to the backend, which are lesser than the number of logical cpu cores.")]
        [DataRow(700, 600, DisplayName = "Validate that when there is a significant delay in opening the chanels to the backend, then with a high degree of parallism, a higher number of TCP connections were established to the backend, which are greater than the number of logical cpu cores.")]
        public async Task TryOpenRntbdChannelsAsync_WithHighDegreeOfParallelInvocation_ShouldLimitTCPConnectionsAndOpenTheChannelsSuccessfully(
            int delayInMs,
            int degreeOfParallalism)
        {
            // Arrange.
            int port = 15200;
            Uri serverUri = new($"tcp://localhost:{port}");
            Documents.Rntbd.TransportClient.Options clientOptions = new(TimeSpan.FromMilliseconds(1));
            ChannelProperties channelProperties = RntbdOpenConnectionHandlerTests.BuildChannelProperties(clientOptions);

            List<int[]> openChannelCounters = new()
            {
                new int[] { 0 },
                new int[] { 0 },
                new int[] { 0 },
                new int[] { 0 },
            };

            TransportAddressUri backendTransportAddress1 = new(
                addressUri: new Uri(
                    uriString: "rntbd://test.azure.com:15211/partitions/1p"));

            TransportAddressUri backendTransportAddress2 = new(
                addressUri: new(
                    uriString: "rntbd://test.azure.com:15214/partitions/2s"));

            TransportAddressUri backendTransportAddress3 = new(
                addressUri: new(
                    uriString: "rntbd://test.azure.com:15212/partitions/3s"));

            TransportAddressUri backendTransportAddress4 = new(
                addressUri: new(
                    uriString: "rntbd://test.azure.com:15218/partitions/4s"));

            LoadBalancingChannel loadBalancingChannel1 = this.SetupAndGetLoadBalancingChannel(
                delayInMs: delayInMs,
                openChannelCounters[0],
                serverUri: serverUri,
                channelProperties: channelProperties,
                backendReplicaUrl: backendTransportAddress1);

            LoadBalancingChannel loadBalancingChannel2 = this.SetupAndGetLoadBalancingChannel(
                delayInMs: delayInMs,
                openChannelCounters[1],
                serverUri: serverUri,
                channelProperties: channelProperties,
                backendReplicaUrl: backendTransportAddress2);

            LoadBalancingChannel loadBalancingChannel3 = this.SetupAndGetLoadBalancingChannel(
                delayInMs: delayInMs,
                openChannelCounters[2],
                serverUri: serverUri,
                channelProperties: channelProperties,
                backendReplicaUrl: backendTransportAddress3);

            LoadBalancingChannel loadBalancingChannel4 = this.SetupAndGetLoadBalancingChannel(
                delayInMs: delayInMs,
                openChannelCounters[3],
                serverUri: serverUri,
                channelProperties: channelProperties,
                backendReplicaUrl: backendTransportAddress4);

            Mock<IChannelDictionary> mockedChannelDictionary = new();

            mockedChannelDictionary
                .Setup(x => x.GetChannel(backendTransportAddress1.Uri, false))
                .Returns(loadBalancingChannel1);

            mockedChannelDictionary
                .Setup(x => x.GetChannel(backendTransportAddress2.Uri, false))
                .Returns(loadBalancingChannel2);

            mockedChannelDictionary
                .Setup(x => x.GetChannel(backendTransportAddress3.Uri, false))
                .Returns(loadBalancingChannel3);

            mockedChannelDictionary
                .Setup(x => x.GetChannel(backendTransportAddress4.Uri, false))
                .Returns(loadBalancingChannel4);

            MockedTransportClient mockedTransportClient = new(
                mockedChannelDictionary);

            RntbdOpenConnectionHandler rntbdOpenConnectionHandler = new(
                transportClient: mockedTransportClient);

            List<TransportAddressUri> addressesSet = new()
            {
                backendTransportAddress1,
                backendTransportAddress2,
                backendTransportAddress3,
                backendTransportAddress4,
            };

            List<Task> openConnectionTasks = new();
            FieldInfo fieldInfo = rntbdOpenConnectionHandler
                .GetType()
                .GetField(
                    name: "disposed",
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            // Act.
            for (int i = 0; i < degreeOfParallalism; i++)
            {
                openConnectionTasks
                    .Add(rntbdOpenConnectionHandler
                        .TryOpenRntbdChannelsAsync(
                            addresses: addressesSet));
            }

            await Task.WhenAll(openConnectionTasks);

            // This assertion signify that when there is no delay/ minor delay in opening Rntbd connections to the backend replicas,
            // then the maximum number of parallel threads that can open the channels for a specific endpoint is limited to 1.
            Assert.IsTrue(openChannelCounters[0][0] == 1);
            Assert.IsTrue(openChannelCounters[1][0] == 1);
            Assert.IsTrue(openChannelCounters[2][0] == 1);
            Assert.IsTrue(openChannelCounters[3][0] == 1);

            // The total number of calls made to the Rntbd open connection handler would be the number of replicas * the degree
            // of parallism.
            Assert.AreEqual(4 * degreeOfParallalism, mockedTransportClient.GetRntbdOpenConnectionHandlerInvocationCounter());
        }

        private static void UpdateLoadBalancingChannelUsingReflection(
            LoadBalancingChannel objectName,
            string fieldName,
            LoadBalancingPartition fieldValue)
        {
            FieldInfo fieldInfo = objectName
                .GetType()
                .GetField(
                    name: fieldName,
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            LoadBalancingPartition objectValue = (LoadBalancingPartition)fieldValue;

            fieldInfo
                .SetValue(
                    obj: objectName,
                    value: objectValue);
        }

        private LoadBalancingChannel SetupAndGetLoadBalancingChannel(
            int delayInMs,
            int[] openChannelCounter,
            Uri serverUri,
            ChannelProperties channelProperties,
            TransportAddressUri backendReplicaUrl)
        {
            Mock<Documents.Rntbd.IChannel> mockedChannel = new();
            mockedChannel
                .Setup(x => x.OpenChannelAsync(It.IsAny<Guid>()))
                .Callback(() =>
                {
                    openChannelCounter[0]++;
                })
                .Returns(
                    delayInMs == 0
                        ? Task.FromResult(0)
                        : Task.Run(async () =>
                        {
                            await Task.Delay(delayInMs);
                            return Task.FromResult(0);
                        }));

            mockedChannel
                .Setup(x => x.Healthy)
                .Returns(true);

            LoadBalancingPartition loadBalancingPartition = new(
                serverUri: serverUri,
                channelProperties: channelProperties,
                localRegionRequest: true,
                channelFactory: (_, _, _, _, _, _, _) =>
                {
                    return mockedChannel.Object;
                });

            LoadBalancingChannel loadBalancingChannel = new(
                serverUri: new Uri(backendReplicaUrl.Uri.GetLeftPart(UriPartial.Authority)),
                channelProperties: channelProperties,
                localRegionRequest: false);

            RntbdOpenConnectionHandlerTests.UpdateLoadBalancingChannelUsingReflection(
                objectName: loadBalancingChannel,
                fieldName: "singlePartition",
                fieldValue: loadBalancingPartition);

            return loadBalancingChannel;
        }

        private static ChannelProperties BuildChannelProperties(
            Documents.Rntbd.TransportClient.Options clientOptions)
        {
            TimerPool idleTimerPool = null;
            TimerPool timerPool = new((int)clientOptions.TimerPoolResolution.TotalSeconds);

            if (clientOptions.IdleTimeout > TimeSpan.Zero)
            {
                idleTimerPool = new TimerPool(minSupportedTimerDelayInSeconds: 30);
            }

            UserPortPool userPortPool = null;
            if (clientOptions.PortReuseMode == PortReuseMode.PrivatePortPool)
            {
                userPortPool = new UserPortPool(
                    clientOptions.PortPoolReuseThreshold,
                    clientOptions.PortPoolBindAttempts);
            }

            return new ChannelProperties(
                clientOptions.UserAgent,
                clientOptions.CertificateHostNameOverride,
                clientOptions.ConnectionStateListener,
                timerPool,
                clientOptions.RequestTimeout,
                clientOptions.OpenTimeout,
                clientOptions.LocalRegionOpenTimeout,
                clientOptions.PortReuseMode,
                userPortPool,
                clientOptions.MaxChannels,
                clientOptions.PartitionCount,
                clientOptions.MaxRequestsPerChannel,
                clientOptions.MaxConcurrentOpeningConnectionCount,
                clientOptions.ReceiveHangDetectionTime,
                clientOptions.SendHangDetectionTime,
                clientOptions.IdleTimeout,
                idleTimerPool,
                clientOptions.CallerId,
                clientOptions.EnableChannelMultiplexing,
                clientOptions.MemoryStreamPool,
                clientOptions.RemoteCertificateValidationCallback,
                clientOptions.ClientCertificateFunction,
                clientOptions.ClientCertificateFailureHandler,
                clientOptions.DnsResolutionFunction);
        }

        internal class MockedTransportClient : Documents.TransportClient
        {
            private ReaderWriterLockSlim counterLock = new();
            private volatile int rntbdOpenConnectionHandlerInvocationCounter;
            private readonly IChannelDictionary channelDictionary;

            public MockedTransportClient(
                Mock<IChannelDictionary> mockedChannelDictionary)
            {
                this.channelDictionary = mockedChannelDictionary.Object;
            }

            internal override Task<StoreResponse> InvokeStoreAsync(Uri physicalAddress, ResourceOperation resourceOperation, DocumentServiceRequest request)
            {
                throw new NotImplementedException();
            }

            internal override Task OpenConnectionAsync(
               Uri physicalAddress)
            {
                Interlocked.Increment(ref this.rntbdOpenConnectionHandlerInvocationCounter);
                Guid activityId = Trace.CorrelationManager.ActivityId;

                Documents.Rntbd.IChannel channel = this.channelDictionary.GetChannel(
                    requestUri: physicalAddress,
                    localRegionRequest: false);

                return channel.OpenChannelAsync(
                        activityId: activityId);
            }

            internal int GetRntbdOpenConnectionHandlerInvocationCounter()
            {
                this.counterLock.EnterReadLock();
                try
                {
                    return this.rntbdOpenConnectionHandlerInvocationCounter;
                }
                finally
                {
                    this.counterLock.ExitReadLock();
                }
            }
        }
    }
}