// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Unit tests for <see cref="LoadBalancingPartition"/>.
    /// </summary>
    [TestClass]
    public class LoadBalancingPartitionTests
    {
        /// <summary>
        /// Test to validate that, when the provided channel override delegate returns a null value, the OpenChannelAsync() throws
        /// an argument null exception.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenChannelAsync_WhenChannelOverrideDelegateReturnsNull_ShouldThrowArgumentNullException()
        {
            // Arrange.
            int port = 15200, maximumChannels = 2, maximumRequestsPerChannel = 5;
            Uri serverUri = new($"tcp://localhost:{port}");

            Mock<IChannel> mockedChannel = new();
            mockedChannel.Setup(x => x.OpenChannelAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

            using LoadBalancingPartition loadBalancingPartition = new(
                serverUri: serverUri,
                channelProperties: this.BuildChannelProperties(
                    maximumChannels: maximumChannels,
                    maximumRequestsPerChannel: maximumRequestsPerChannel,
                    maxConcurrentOpeningConnectionCount: 1),
                localRegionRequest: true,
                channelFactory: (_, _, _, _, _, _, _) =>
                {
                    return null;
                });

            // Act and Assert.
            ArgumentNullException ex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => loadBalancingPartition.OpenChannelAsync(
                activityId: Guid.NewGuid()));

            (int, List<LbChannelState>) currentCapacityAndChannels = this.GetCurrentCapacityAndOpenChannels(loadBalancingPartition);

            Assert.IsNotNull(ex);
            Assert.IsTrue(ex.Message.Contains("Channel can't be null"));
        }

        /// <summary>
        /// Test to validate that, when no channel overrides are provided, the method tries to establish Rntbd connection
        /// to the given server uri. Note that, this test is not validating the actual connection establishment and throws
        /// a transport exception because the provided uri for dummy in nature. Additionally, note that we add the channel
        /// states into the open channels list, and eventually that gets removed during a real time request flow.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenChannelAsync_WhenChannelOverrideDelegateNotProvided_ShouldThrowTransportExceptionWhileOpeningChannel()
        {
            // Arrange.
            int port = 15200, maximumChannels = 2, maximumRequestsPerChannel = 5;
            Uri serverUri = new($"tcp://localhost:{port}");

            Mock<IChannel> mockedChannel = new();
            mockedChannel.Setup(x => x.OpenChannelAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

            using LoadBalancingPartition loadBalancingPartition = new(
                serverUri: serverUri,
                channelProperties: this.BuildChannelProperties(
                    maximumChannels: maximumChannels,
                    maximumRequestsPerChannel: maximumRequestsPerChannel,
                    maxConcurrentOpeningConnectionCount: 1),
                localRegionRequest: true);

            // Act and Assert.
            await Assert.ThrowsExceptionAsync<TransportException>(() => loadBalancingPartition.OpenChannelAsync(
                activityId: Guid.NewGuid()));

            (int, List<LbChannelState>) currentCapacityAndChannels = this.GetCurrentCapacityAndOpenChannels(loadBalancingPartition);

            Assert.IsNotNull(currentCapacityAndChannels.Item2);
            Assert.AreEqual(5, currentCapacityAndChannels.Item1);
            Assert.AreEqual(1, currentCapacityAndChannels.Item2.Count);
        }

        /// <summary>
        /// Test to validate that, when parallel calls to OpenChannelAsync is made with and without any added delay, along with a disposal task,
        /// all the channels added successfully to the channel states list and the load balancing partition class gets disposed.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, DisplayName = "Validate the open channel async flow, with a 300 milli seconds added delay.")]
        [DataRow(false, DisplayName = "Validate the open channel async flow, without any added delay.")]
        public async Task OpenChannelAsync_WithParallelInvocationsAndNoDalayInOpenChannel_ShouldOpenChannelsAndDisposeTheClient(bool addDelay)
        {
            // Arrange.
            int port = 15200, maximumChannels = 1000, maximumRequestsPerChannel = 5;
            Uri serverUri = new($"tcp://localhost:{port}");

            Mock<IChannel> mockedChannel = new();
            mockedChannel
                .Setup(x => x.OpenChannelAsync(It.IsAny<Guid>()))
                .Returns(Task.Run(async () =>
                {
                    if (addDelay)
                    {
                        await Task.Delay(300);
                    }

                    return Task.CompletedTask;
                }));

            // Set up the channel as healthy.
            mockedChannel
                .Setup(x => x.Healthy)
                .Returns(true);

            LoadBalancingPartition loadBalancingPartition = new(
                serverUri: serverUri,
                channelProperties: this.BuildChannelProperties(
                    maximumChannels: maximumChannels,
                    maximumRequestsPerChannel: maximumRequestsPerChannel,
                    maxConcurrentOpeningConnectionCount: 1),
                localRegionRequest: true,
                channelFactory: (_, _, _, _, _, _, _) =>
                {
                    return mockedChannel.Object;
                });

            // Act and Assert.
            List<Task> openChannelTasks = new();
            for (int i = 0; i < 100; i++)
            {
                openChannelTasks.Add(
                    loadBalancingPartition.OpenChannelAsync(
                        activityId: Guid.NewGuid()));
            }

            Task disposeTask = Task.Run(() =>
            {
                loadBalancingPartition.Dispose();
            });

            openChannelTasks.Add(disposeTask);

            await Task.WhenAll(openChannelTasks);

            (int, List<LbChannelState>) currentCapacityAndChannels = this.GetCurrentCapacityAndOpenChannels(loadBalancingPartition);

            Assert.IsNotNull(currentCapacityAndChannels.Item2);
            Assert.AreEqual(5, currentCapacityAndChannels.Item1);
            Assert.AreEqual(1, currentCapacityAndChannels.Item2.Count);
        }

        /// <summary>
        /// Test to validate that, when parallel calls to OpenChannelAsync is made and the channel states remain unhealthy
        /// all the un-channels added to the channel states list will be cleaned up by the OpenChannelAsync().
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task OpenChannelAsync_WhenChannelStateUnhealthy_ShouldCleanupUnhealthyChannelStates()
        {
            // Arrange.
            int port = 15200, maximumChannels = 1000, maximumRequestsPerChannel = 5;
            Uri serverUri = new($"tcp://localhost:{port}");

            Mock<IChannel> mockedChannel = new();
            mockedChannel
                .Setup(x => x.OpenChannelAsync(It.IsAny<Guid>()))
                .Returns(Task.Run(() =>
                {
                    return Task.CompletedTask;
                }));

            // Set up the channel as unhealthy.
            mockedChannel
                .Setup(x => x.Healthy)
                .Returns(false);

            LoadBalancingPartition loadBalancingPartition = new(
                serverUri: serverUri,
                channelProperties: this.BuildChannelProperties(
                    maximumChannels: maximumChannels,
                    maximumRequestsPerChannel: maximumRequestsPerChannel,
                    maxConcurrentOpeningConnectionCount: 1),
                localRegionRequest: true,
                channelFactory: (_, _, _, _, _, _, _) =>
                {
                    return mockedChannel.Object;
                });

            // Act and Assert.
            List<Task> openChannelTasks = new();
            for (int i = 0; i < 100; i++)
            {
                openChannelTasks.Add(
                    loadBalancingPartition.OpenChannelAsync(
                        activityId: Guid.NewGuid()));
            }

            Task disposeTask = Task.Run(() =>
            {
                loadBalancingPartition.Dispose();
            });

            openChannelTasks.Add(disposeTask);

            await Task.WhenAll(openChannelTasks);

            (int, List<LbChannelState>) currentCapacityAndChannels = this.GetCurrentCapacityAndOpenChannels(loadBalancingPartition);

            Assert.IsNotNull(currentCapacityAndChannels.Item2);
            Assert.AreEqual(5, currentCapacityAndChannels.Item1);
            Assert.AreEqual(1, currentCapacityAndChannels.Item2.Count, "This would imply that even though we are trying to open up channels 100 times, the unhealthy channels would get cleaned up and removed from the open channels.");
        }

        /// <summary>
        /// Helper method to get the current channel capacity and open channels using reflection.
        /// </summary>
        /// <param name="loadBalancingPartition">An instance of <see cref="LoadBalancingPartition"/>.</param>
        /// <returns>A pair of current channel capacity and number of open channels.</returns>
        private (int, List<LbChannelState>) GetCurrentCapacityAndOpenChannels(
            LoadBalancingPartition loadBalancingPartition)
        {
            int channelCapacity = (int)loadBalancingPartition.GetType()
                .GetField("capacity", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadBalancingPartition);
            List<LbChannelState> openChannels = (List<LbChannelState>)loadBalancingPartition.GetType()
                .GetField("openChannels", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadBalancingPartition);

            return (channelCapacity, openChannels);
        }

        /// <summary>
        /// Helper method to build <see cref="ChannelProperties"/> object.
        /// </summary>
        /// <param name="maximumChannels">An integer containing the maximum number of channels.</param>
        /// <param name="maximumRequestsPerChannel">An integer containing the maximum requests per channel.</param>
        /// <param name="maxConcurrentOpeningConnectionCount">An integer defining the maximum number of concurrent open connections count.</param>
        /// <returns>An instance of <see cref="ChannelProperties"/> object containing the channel properties.</returns>
        private ChannelProperties BuildChannelProperties(
            int maximumChannels = ushort.MaxValue,
            int maximumRequestsPerChannel = 1,
            int maxConcurrentOpeningConnectionCount = 1)
        {
            return new(
                new UserAgentContainer(),
                certificateHostNameOverride: "127.0.0.1",
                connectionStateListener: null,
                requestTimerPool: new TimerPool(1),
                requestTimeout: TimeSpan.FromSeconds(1.0),
                openTimeout: TimeSpan.FromMinutes(10),
                localRegionOpenTimeout: TimeSpan.FromSeconds(1.0),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                maxChannels: maximumChannels,
                partitionCount: 1,
                maxRequestsPerChannel: maximumRequestsPerChannel,
                maxConcurrentOpeningConnectionCount: maxConcurrentOpeningConnectionCount,
                receiveHangDetectionTime: TimeSpan.FromSeconds(15.0),
                sendHangDetectionTime: TimeSpan.FromSeconds(5.0),
                idleTimeout: TimeSpan.FromSeconds(-1),
                idleTimerPool: null,
                callerId: RntbdConstants.CallerId.Anonymous,
                enableChannelMultiplexing: false,
                memoryStreamPool: null,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                clientCertificateFailureHandler: null,
                dnsResolutionFunction: null);
        }
    }
}