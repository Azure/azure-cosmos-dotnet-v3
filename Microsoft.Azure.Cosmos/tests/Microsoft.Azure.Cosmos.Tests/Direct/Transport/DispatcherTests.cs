// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Documents.Tests.Rntbd
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Test;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Test.ConnectionTestUtils;
    using static Microsoft.Azure.Documents.Rntbd.Dispatcher;
    using static Microsoft.Azure.Documents.RntbdConstants;

    [TestClass]
    [DoNotParallelize]
    public class DispatcherTests
    {
        private Uri serverUri = new Uri("https://localhost");
        private readonly Mock<IConnectionStateListener> mockConnectionStateListener = new Mock<IConnectionStateListener>();
        private readonly Mock<IChaosInterceptor> mockChaosInterceptor = new Mock<IChaosInterceptor>();
        private readonly uint requestId = 42;

        [TestMethod]
        [Owner("amudumba")]
        public async Task Dispatcher_Connection_Unhealthy_WhenConnectionMultipleTimeouts()
        {

            // Arrange
            using X509Certificate2 certificate = ConnectionTestUtils.CreateTestCertificate();
            Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
            using CancellationTokenSource listenerCancellation = new CancellationTokenSource();
            Task startListener = Task.CompletedTask;
            Task startIpV6Listener = Task.CompletedTask;
            ConnectionTestUtils.BuildTcpListener(out int port, out TcpListener tcpListener, out TcpListener ipV6TcpListener);

            TestUtils.SnapshotConnectionTimestamps(
                out DateTime currentTime,
                out DateTime lastSendAttempt,
                out DateTime lastSend,
                out _,
                out DateTime firstSendSinceLastReceive,
                out TimeSpan sendDelayLimit,
                out TimeSpan receiveDelayLimit,
                out TimeSpan idleConnectionTimeout);

            string originalAggressiveTimeoutDetection = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled);
            string originalTimeoutDetectionTimeLimit = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.TimeoutDetectionTimeLimit);
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled, "true");
            Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.TimeoutDetectionTimeLimit, "3");
            try
            {
                Func<string, X509Certificate2> clientCertificateFunction = _ => certificate;

                using (Connection mockConnection = new(
                                   new Uri($"tcp://localhost:{port}"),
                                   "127.0.0.1",
                                    TimeSpan.FromSeconds(11.0),
                                    TimeSpan.FromSeconds(3.0),
                                    TimeSpan.FromSeconds(1800.0),
                                   memoryStreamPool: new TestMemoryStreamPool(true),
                                   remoteCertificateValidationCallback: ConnectionTestUtils.SelfSignedForLocalhost,
                                   clientCertificateFunction,
                                   dnsResolutionFunction: null))
                {
                    Dispatcher dispatcher = this.CreateDispatcherWithMockedConnection(mockConnection);

                    string testText = Guid.NewGuid().ToString() + DateTime.Now.ToString();

                    // RNTBD can open either IPv4 or IPv6 connection
                    startListener = ConnectionTestUtils.StartProcessingConnectionAsync(tcpListener, certificate, testText, cancellationToken: listenerCancellation.Token);
                    startIpV6Listener = ConnectionTestUtils.StartProcessingConnectionAsync(ipV6TcpListener, certificate, testText, cancellationToken: listenerCancellation.Token);

                    // Let some time for a server to start.
                    await Task.Delay(1000);

                    ChannelOpenArguments openArguments = new ChannelOpenArguments(
                               Guid.NewGuid(), new ChannelOpenTimeline(), TimeSpan.FromSeconds(5),
                               PortReuseMode.ReuseUnicastPort, null, RntbdConstants.CallerId.Anonymous);

                    TransportSerialization.SerializedRequest request = new(
                        requestHeader: new BufferProvider().GetBuffer(10),
                        requestBody: new CloneableStream(new MemoryStream()));

                    await mockConnection.OpenAsync(openArguments);

                    Assert.IsTrue(dispatcher.Healthy);

                    PrepareCallResult callResult = new PrepareCallResult(this.requestId, this.serverUri, null);


                    // Act with connection timeouts.
                    for (int i = 0; i < 4; i++)
                    {
                        dispatcher.CancelCallAndNotifyConnectionOnTimeoutEvent(callResult, false);
                        dispatcher.CancelCallAndNotifyConnectionOnTimeoutEvent(callResult, true);
                    }

                    // Wait for Dispatcher.Healthy to become false or cancellation token expiry of 30 seconds.
                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        while (dispatcher.Healthy && !cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(500);
                        }
                    }

                    // Assert
                    Assert.IsFalse(dispatcher.Healthy);
                }
            }
            finally
            {
                listenerCancellation.Cancel();
                try
                {
                    await Task.WhenAll(startListener, startIpV6Listener).WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally
                {
                    tcpListener.Stop();
                    ipV6TcpListener.Stop();
                    Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled, originalAggressiveTimeoutDetection);
                    Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.TimeoutDetectionTimeLimit, originalTimeoutDetectionTimeLimit);
                }
            }
        }

        [TestMethod]
        [Owner("dikshibahl")]
        public async Task OpenAsync_WhenStatusIs200_SetsServerProperties_WithExtraTokenAsync()
        {
            // ARRANGE
            Mock<IConnection> mockConnection = new Mock<IConnection>();
            ChannelOpenTimeline openTimeline = new ChannelOpenTimeline();

            mockConnection
                .Setup(c => c.OpenAsync(It.IsAny<ChannelOpenArguments>()))
                .Callback<ChannelOpenArguments>(args =>
                {
                    // Simulate the timeline events
                    args.OpenTimeline.RecordConnectFinishTime();
                    args.OpenTimeline.RecordSslHandshakeFinishTime();

                })
                .Returns(Task.CompletedTask);

            Dispatcher dispatcher = new Dispatcher(
              serverUri: this.serverUri,
              userAgent: new UserAgentContainer(),
              connectionStateListener: this.mockConnectionStateListener.Object,
              idleTimerPool: null,
              enableChannelMultiplexing: false,
              chaosInterceptor: this.mockChaosInterceptor.Object,
              mockConnection.Object);

            ChannelOpenArguments openArguments = new ChannelOpenArguments(
                Guid.NewGuid(),
                openTimeline,
                TimeSpan.FromSeconds(5),
                PortReuseMode.ReuseUnicastPort,
                null,
                RntbdConstants.CallerId.Anonymous);

            mockConnection
               .Setup(c => c.WriteRequestAsync(
                   It.IsAny<ChannelCommonArguments>(),
                   It.IsAny<TransportSerialization.SerializedRequest>(),
                   null))
               .Returns(Task.CompletedTask);

            // Set up the response metadata as needed
            byte[] headerBytes = { 80, 0, 0, 0, 200, 0, 0, 0, 30, 121, 250, 12, 27, 144, 235, 75, 183, 147, 109, 226, 65, 159, 160, 243 };

            byte[] capturedMetadataBytes = { 2, 0, 7, 17, 68, 111, 99, 117, 109, 101, 110, 116, 68, 66, 32, 83, 101, 114, 118, 101, 114, 3, 0, 7, 17, 32, 118, 101, 114, 115, 105, 111, 110, 61, 50, 46, 49, 52, 46, 48, 46, 48, 4, 0, 2, 120, 0, 0, 0, 5, 0, 2, 25, 0, 0, 0 };

            using (MemoryStream ms = new MemoryStream())
            {
                await ms.WriteAsync(capturedMetadataBytes, 0, capturedMetadataBytes.Length);

                ushort newIdentifier = (ushort)Enum.GetValues(typeof(RntbdConstants.ResponseIdentifiers)).Cast<RntbdConstants.ResponseIdentifiers>().Last();
                ms.WriteByte((byte)(newIdentifier >> 8));
                ms.WriteByte((byte)(newIdentifier & 0xFF));
                ms.WriteByte((byte)RntbdTokenTypes.SmallString);
                string newValue = "hello!";
                ms.WriteByte((byte)newValue.Length);
                await ms.WriteAsync(Encoding.UTF8.GetBytes(newValue), 0, newValue.Length);

                capturedMetadataBytes = ms.ToArray();
            }

            BufferProvider.DisposableBuffer mockHeaderBuffer = new BufferProvider.DisposableBuffer(headerBytes);
            BufferProvider.DisposableBuffer mockMetadataBuffer = new BufferProvider.DisposableBuffer(capturedMetadataBytes);
            Connection.ResponseMetadata responseMetadata = new Connection.ResponseMetadata(mockHeaderBuffer, mockMetadataBuffer);

            mockConnection
                .Setup(c => c.ReadResponseMetadataAsync(It.IsAny<ChannelCommonArguments>()))
                .ReturnsAsync(responseMetadata);

            // ACT
            await dispatcher.OpenAsync(openArguments);

            // Assert
            mockConnection.VerifyAll();


        }


        private Dispatcher CreateDispatcherWithMockedConnection(Connection connectionMock)
        {
            Dispatcher dispatcher = new Dispatcher(
               serverUri: this.serverUri,
               userAgent: new UserAgentContainer(),
               connectionStateListener: this.mockConnectionStateListener.Object,
               idleTimerPool: null,
               enableChannelMultiplexing: false,
               chaosInterceptor: this.mockChaosInterceptor.Object,
               connectionMock);

            return dispatcher;
        }

    }
}