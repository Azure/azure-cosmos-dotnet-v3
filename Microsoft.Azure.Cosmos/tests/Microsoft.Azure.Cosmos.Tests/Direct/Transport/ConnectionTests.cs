// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [DoNotParallelize]
    public class ConnectionTests
    {
        [Owner("cosmosdbquery")] // dev maintainer: dmmelnik
        [TestMethod]
        [Timeout(10000)]
        [DataRow(true)]
        [DataRow(false)]
        [DoNotParallelize]
        public async Task TestReadResponseBodyAsync(bool usePooledStream)
        {
            using X509Certificate2 certificate = ConnectionTestUtils.CreateTestCertificate();
            Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
            using CancellationTokenSource listenerCancellation = new CancellationTokenSource();
            Task startListener = Task.CompletedTask;
            Task startIpV6Listener = Task.CompletedTask;
            ConnectionTestUtils.BuildTcpListener(out int port, out TcpListener tcpListener, out TcpListener ipV6TcpListener);
            try
            {
                Func<string, X509Certificate2> clientCertificateFunction = _ => certificate;
                using (Connection connection = new(
                    new Uri($"tcp://localhost:{port}"),
                    "127.0.0.1",
                    TimeSpan.FromSeconds(11.0),
                    TimeSpan.FromSeconds(3.0),
                    TimeSpan.FromSeconds(1800.0),
                    memoryStreamPool: new TestMemoryStreamPool(usePooledStream),
                    remoteCertificateValidationCallback: ConnectionTestUtils.SelfSignedForLocalhost,
                    clientCertificateFunction,
                    dnsResolutionFunction: null))
                {
                    // Let some time for a server to start.
                    string testText = Guid.NewGuid().ToString() + DateTime.Now.ToString();

                    // RNTBD can open either IPv4 or IPv6 connection
                    startListener = ConnectionTestUtils.StartProcessingConnectionAsync(tcpListener, certificate, testText, cancellationToken: listenerCancellation.Token);
                    startIpV6Listener = ConnectionTestUtils.StartProcessingConnectionAsync(ipV6TcpListener, certificate, testText, cancellationToken: listenerCancellation.Token);

                    await Task.Delay(1000);

                    ChannelOpenArguments openArguments = new ChannelOpenArguments(
                               Guid.NewGuid(), new ChannelOpenTimeline(), TimeSpan.FromSeconds(5),
                               PortReuseMode.ReuseUnicastPort, null, RntbdConstants.CallerId.Anonymous);
                    await connection.OpenAsync(openArguments);

                    ChannelCommonArguments arguments = new ChannelCommonArguments(
                        activityId: Guid.NewGuid(),
                        initialTimeoutCode: Documents.TransportErrorCode.RequestTimeout,
                        userPayload: true);
                    MemoryStream responseBody = await connection.ReadResponseBodyAsync(arguments);

                    Assert.AreEqual(testText, Encoding.UTF8.GetString(responseBody.ToArray()), "Unexpected response body");
                    Assert.AreEqual(1, Connection.NumberOfOpenTcpConnections);

                    Assert.IsTrue(usePooledStream ? responseBody is TestMemoryStream : responseBody is MemoryStream, "Unexpected response message stream");
                }
                Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
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
                }
            }
        }

        /// <summary>
        /// Test to validate that -
        ///
        /// 1. When a connection is opened, it's initially marked as healthy.
        ///
        /// 2. When there is a delay before sending a write request and a delay after sending
        ///    the write request, which eventually exceeds the receive hang grace period, the
        ///    connection is eventually marked un-healthy.
        ///
        /// 3. When a response is received afterwards, the connection is marked as healthy.
        ///
        /// </summary>
        [TestMethod]
        [Timeout(40000)]
        [Owner("dkunda")]
        [DoNotParallelize]
        public async Task HealthyTest_WhenNowAndLastSendTimeExceedsReceiveHangGracePeriodAndResponseReceived_ShouldMarkTheConnectionUnHealthyAndHealthySequentially()
        {
            using X509Certificate2 certificate = ConnectionTestUtils.CreateTestCertificate();
            Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
            using CancellationTokenSource listenerCancellation = new CancellationTokenSource();
            Task startListener = Task.CompletedTask;
            Task startIpV6Listener = Task.CompletedTask;
            ConnectionTestUtils.BuildTcpListener(
                out int port,
                out TcpListener tcpListener,
                out TcpListener ipV6TcpListener);
            try
            {
                Func<string, X509Certificate2> clientCertificateFunction = _ => certificate;
                using (Connection connection = new(
                    serverUri: new Uri($"tcp://localhost:{port}"),
                    hostNameCertificateOverride: "127.0.0.1",
                    receiveHangDetectionTime: TimeSpan.FromSeconds(11.00),
                    sendHangDetectionTime: TimeSpan.FromSeconds(3.0),
                    idleTimeout: TimeSpan.FromSeconds(1800.0),
                    memoryStreamPool: new TestMemoryStreamPool(false),
                    remoteCertificateValidationCallback: ConnectionTestUtils.SelfSignedForLocalhost,
                    clientCertificateFunction,
                    dnsResolutionFunction: null))
                {
                    // Arrange.
                    string testText = $"{Guid.NewGuid()}{DateTime.Now}";

                    // RNTBD can open either IPv4 or IPv6 connection
                    startListener = ConnectionTestUtils.StartProcessingConnectionAsync(
                        listener: tcpListener,
                        certificate: certificate,
                        testText: testText, cancellationToken: listenerCancellation.Token);

                    startIpV6Listener = ConnectionTestUtils.StartProcessingConnectionAsync(
                        listener: ipV6TcpListener,
                        certificate: certificate,
                        testText: testText, cancellationToken: listenerCancellation.Token);

                    // Let some time for a server to start.
                    await Task.Delay(1000);

                    ChannelOpenArguments arguments = new(
                               activityId: Guid.NewGuid(),
                               openTimeline: new ChannelOpenTimeline(),
                               openTimeout: TimeSpan.FromSeconds(5),
                               portReuseMode: PortReuseMode.ReuseUnicastPort,
                               userPortPool: null,
                               callerId: RntbdConstants.CallerId.Anonymous);

                    TransportSerialization.SerializedRequest request = new(
                        requestHeader: new BufferProvider().GetBuffer(10),
                        requestBody: new CloneableStream(new MemoryStream()));

                    // Act and Assert.
                    await connection
                        .OpenAsync(
                            args: arguments);

                    Assert.IsTrue(
                        condition: connection.Healthy,
                        message: "A connection opened successfully is initially marked as healthy.");

                    await Task.Delay(12000);
                    await connection
                        .WriteRequestAsync(
                            args: arguments.CommonArguments,
                            messagePayload: request,
                            transportRequestStats: null);

                    await Task.Delay(10000);
                    Assert.IsFalse(
                        condition: connection.Healthy,
                        message: "When a write request is sent but the response is not received with-in the receive hang grace period, the connection will be marked as un-healthy.");

                    ChannelCommonArguments commonArguments = new(
                        activityId: Guid.NewGuid(),
                        initialTimeoutCode: Documents.TransportErrorCode.RequestTimeout,
                        userPayload: true);

                    MemoryStream responseBody = await connection.ReadResponseBodyAsync(
                        args: commonArguments);

                    Assert.IsTrue(
                        condition: connection.Healthy,
                        message: "When a response is received eventually, the connection will be marked as healthy.");
                }

                Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
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
                }
            }
        }

        /// <summary>
        /// Test to validate that -
        ///
        /// 1. When the difference between current time and last send time exceedes the receiving hang
        ///    grace period, then the connection is marked as un-healthy. This has been achieved from the
        ///    input of the first DataRow (Delay time after the last write = 10 Secs).
        ///
        /// 2. When the difference between current time and first send since last receive time exceedes
        ///    the receiving hang grace period, then the connection is marked as un-healthy. This has been
        ///    achieved from the input of the second DataRow (Delay time after the last write = 5 Secs).
        ///
        /// The below table captures the real-time send/ receive times and their respective
        /// differences. To understand the scenario better, please take a look at the table, and try
        /// to map the respective values applicable for the test, to verify the difference.
        ///
        /// Truth table for data row 1:
        /// |------------------------------------------|----------------------------|
        /// | Now                                      | 10/26/2022 10:49:45 PM     |
        /// |------------------------------------------|----------------------------|
        /// | Last Send                                | 10/26/2022 10:49:35 PM     |
        /// |------------------------------------------|----------------------------|
        /// | Last Receive                             | 10/26/2022 10:49:22 PM     |
        /// |------------------------------------------|----------------------------|
        /// | First Send Since Last Received           | 10/26/2022 10:49:30 PM     |
        /// |------------------------------------------|----------------------------|
        /// | Receive Hang Grace Period                | 00:00:10 Seconds           |
        /// |------------------------------------------|----------------------------|
        /// | Receive Delay Limit                      | 00:00:11 Seconds           |
        /// |------------------------------------------|----------------------------|
        /// | Number of sends since last receive       | 3                          |
        /// |------------------------------------------|----------------------------|
        /// | (Now - Last Receive)                     | 00:00:23.0555620 Seconds   |
        /// |------------------------------------------|----------------------------|
        /// | (Now - Last Send)                        | 00:00:10.0108459 Seconds   |
        /// |------------------------------------------|----------------------------|
        /// | (Last Send - Last Receive)               | 00:00:13.0447161 Seconds   |
        /// |------------------------------------------|----------------------------|
        /// | (Now - First Send Since Last Received)   | 00:00:15.0260092 Seconds   |
        /// |------------------------------------------|----------------------------|.
        ///
        /// Truth table for data row 2:
        /// |------------------------------------------|----------------------------|
        /// | Now                                      | 10/26/2022 10:49:20 PM     |
        /// |------------------------------------------|----------------------------|
        /// | Last Send                                | 10/26/2022 10:49:15 PM     |
        /// |------------------------------------------|----------------------------|
        /// | Last Receive                             | 10/26/2022 10:49:02 PM     |
        /// |------------------------------------------|----------------------------|
        /// | First Send Since Last Received           | 10/26/2022 10:49:10 PM     |
        /// |------------------------------------------|----------------------------|
        /// | Receive Hang Grace Period                | 00:00:10 Seconds           |
        /// |------------------------------------------|----------------------------|
        /// | Receive Delay Limit                      | 00:00:11 Seconds           |
        /// |------------------------------------------|----------------------------|
        /// | Number of sends since last receive       | 3                          |
        /// |------------------------------------------|----------------------------|
        /// | (Now - Last Receive)                     | 00:00:18.0529330 Seconds   |
        /// |------------------------------------------|----------------------------|
        /// | (Now - Last Send)                        | 00:00:05.0206005 Seconds   |
        /// |------------------------------------------|----------------------------|
        /// | (Last Send - Last Receive)               | 00:00:13.0323325 Seconds   |
        /// |------------------------------------------|----------------------------|
        /// | (Now - First Send Since Last Received)   | 00:00:10.0321153 Seconds   |
        /// |------------------------------------------|----------------------------|.
        ///
        /// </summary>
        [TestMethod]
        [Timeout(50000)]
        [Owner("dkunda")]
        [DataRow(10000)]
        [DataRow(5000)]
        [DoNotParallelize]
        public async Task HealthyTest_WhenNowAndLastSendTimeAndFirstSendSinceLastReceivedTimeExceedsReceiveHangGracePeriod_ShouldMarkTheConnectionUnHealthy(int delayInMillis)
        {
            using X509Certificate2 certificate = ConnectionTestUtils.CreateTestCertificate();
            Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
            using CancellationTokenSource listenerCancellation = new CancellationTokenSource();
            Task startListener = Task.CompletedTask;
            Task startIpV6Listener = Task.CompletedTask;
            ConnectionTestUtils.BuildTcpListener(
                out int port,
                out TcpListener tcpListener,
                out TcpListener ipV6TcpListener);
            try
            {
                Func<string, X509Certificate2> clientCertificateFunction = _ => certificate;
                using (Connection connection = new(
                    serverUri: new Uri($"tcp://localhost:{port}"),
                    hostNameCertificateOverride: "127.0.0.1",
                    receiveHangDetectionTime: TimeSpan.FromSeconds(11.0),
                    sendHangDetectionTime: TimeSpan.FromSeconds(3.0),
                    idleTimeout: TimeSpan.FromSeconds(1800.0),
                    memoryStreamPool: new TestMemoryStreamPool(false),
                    remoteCertificateValidationCallback: ConnectionTestUtils.SelfSignedForLocalhost,
                    clientCertificateFunction,
                    dnsResolutionFunction: null))
                {
                    // Arrange.
                    string testText = $"{Guid.NewGuid()}{DateTime.Now}";

                    // RNTBD can open either IPv4 or IPv6 connection
                    startListener = ConnectionTestUtils.StartProcessingConnectionAsync(
                        listener: tcpListener,
                        certificate: certificate,
                        testText: testText, cancellationToken: listenerCancellation.Token);

                    startIpV6Listener = ConnectionTestUtils.StartProcessingConnectionAsync(
                        listener: ipV6TcpListener,
                        certificate: certificate,
                        testText: testText, cancellationToken: listenerCancellation.Token);

                    // Let some time for a server to start.
                    await Task.Delay(1000);

                    ChannelOpenArguments[] channelArguments = new[]
                    {
                        BuildChannelOpenArguments(),
                        BuildChannelOpenArguments(),
                        BuildChannelOpenArguments(),
                        BuildChannelOpenArguments(),
                    };

                    TransportSerialization.SerializedRequest[] requests = new[]
                    {
                        BuildSerilizedRequest(),
                        BuildSerilizedRequest(),
                        BuildSerilizedRequest(),
                    };

                    // Act.
                    await connection
                        .OpenAsync(
                            args: channelArguments[0]);

                    await Task.Delay(3000);

                    await connection
                        .WriteRequestAsync(
                            args: channelArguments[1].CommonArguments,
                            messagePayload: requests[0],
                            transportRequestStats: null);

                    await Task.Delay(5000);

                    await connection
                        .WriteRequestAsync(
                            args: channelArguments[2].CommonArguments,
                            messagePayload: requests[1],
                            transportRequestStats: null);

                    await Task.Delay(5000);

                    await connection
                        .WriteRequestAsync(
                            args: channelArguments[3].CommonArguments,
                            messagePayload: requests[2],
                            transportRequestStats: null);

                    await Task.Delay(delayInMillis);

                    // Assert.
                    Assert.IsFalse(
                        condition: connection.Healthy,
                        message: "The connection is marked un-healthy as the (Now - Last Send) OR (Now - First Send Since Last Received) check is exceeding the receive hang grace period 10 seconds.");
                }

                Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
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
                }
            }
        }

        /// <summary>
        /// Test to validate that -
        ///
        /// 1. when the transit timeout detection is enabled and eventually detected,
        /// the connection is marked unhealthy.
        ///
        /// 2. when the transit timeout detection is enabled and the CPU utilization exceeds the threshold set,
        /// the transit timeout validation is skipped and the connection is marked healthy.
        ///
        /// To better understand the test, please refer to the truth table below for the data rows.
        /// The thresholds and timeouts are set to the default following values:
        ///
        /// timeoutDetectionTimeLimit = 60 seconds.
        /// timeoutDetectionOnWriteThreshold = 1.
        /// timeoutDetectionOnWriteTimeLimit = 6 seconds.
        /// timeoutDetectionHighFrequencyThreshold = 3.
        /// timeoutDetectionHighFrequencyTimeLimit = 10 seconds.
        ///
        /// Also, note that the `delayInSecs` attribute is used to add external delay in seconds to the field
        /// `last receive time` in the connection object. This is done to simulate the transit timeout detection.
        ///
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | Data Row Number | Is Read-Only Request | Is CPU Beyond  Threshold | Transit Timeout On Receive Counter | Transit Timeout on Send Counter | Current Time        | Read Delay                                       | Conditions Validated                                                                                                      | Result IsHealthy |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 1               | True                 | False                    | 2                                  | 0                               | 2023-05-12 14:15:05 | (current time - last receive time) = 65 seconds. | (readDelay >= timeoutDetectionTimeLimit)                                                                                  | False            |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 2               | True                 | True                     | 2                                  | 0                               | 2023-05-12 14:15:05 | (current time - last receive time) = 65 seconds. | (readDelay >= timeoutDetectionTimeLimit)                                                                                  | True             |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 3               | True                 | False                    | 4                                  | 0                               | 2023-05-12 14:15:05 | (current time - last receive time) = 14 seconds. | (transitTimeoutCounter >= timeoutDetectionHighFrequencyThreshold  && readDelay >= timeoutDetectionHighFrequencyTimeLimit) | False            |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 4               | True                 | True                     | 4                                  | 0                               | 2023-05-12 14:15:05 | (current time - last receive time) = 14 seconds. | (transitTimeoutCounter >= timeoutDetectionHighFrequencyThreshold  && readDelay >= timeoutDetectionHighFrequencyTimeLimit) | True             |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 5               | False                | False                    | 1                                  | 1                               | 2023-05-12 14:15:05 | (current time - last receive time) = 7 seconds.  | (transitTimeoutWriteCounter >= timeoutDetectionOnWriteThreshold  && readDelay >= timeoutDetectionOnWriteTimeLimit)        | False            |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 6               | False                | True                     | 1                                  | 1                               | 2023-05-12 14:15:05 | (current time - last receive time) = 7 seconds.  | (transitTimeoutWriteCounter >= timeoutDetectionOnWriteThreshold  && readDelay >= timeoutDetectionOnWriteTimeLimit)        | True             |
        /// |-----------------|----------------------|--------------------------|------------------------------------|---------------------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|.
        ///
        /// </summary>
        [TestMethod]
        [Timeout(50000)]
        [Owner("dkunda")]
        [DataRow(2, -65, true, false, false, DisplayName = "Validate That Connection is Marked Unhealthy When Transit Timeout Detected And CPU Utilization is Below Threshold")]
        [DataRow(2, -65, true, true, true, DisplayName = "Validate That When Transit Timeout is Detected And CPU Utilization is Beyond Threshold The Timeout Detection Was Skipped and Connection is Marked Healthy")]
        [DataRow(4, -14, true, false, false, DisplayName = "Validate That Connection is Marked Unhealthy When Transit Timeout is Detected On High Frequency And CPU Utilization is Below Threshold")]
        [DataRow(4, -14, true, true, true, DisplayName = "Validate That When Transit Timeout is Detected On High Frequency And CPU Utilization is Beyond Threshold The Timeout Detection Was Skipped and Connection is Marked Healthy")]
        [DataRow(1, -7, false, false, false, DisplayName = "Validate That Connection is Marked Unhealthy When Transit Timeout is Detected On Write Operation And CPU Utilization is Below Threshold")]
        [DataRow(1, -7, false, true, true, DisplayName = "Validate That When Transit Timeout is Detected On Write Operation And CPU Utilization is Beyond Threshold The Timeout Detection Was Skipped and Connection is Marked Healthy ")]
        [DoNotParallelize]
        public async Task HealthyTest_WhenTransitTimeoutsDetected_ShouldMarkTheConnectionUnHealthy(
            int transitTimeoutCounter,
            int delayInSecs,
            bool isReadOnlyRequest,
            bool isCpuBeyondThreshold,
            bool isConnectionHealthy)
        {
            using X509Certificate2 certificate = ConnectionTestUtils.CreateTestCertificate();
            Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
            using CancellationTokenSource listenerCancellation = new CancellationTokenSource();
            Task startListener = Task.CompletedTask;
            Task startIpV6Listener = Task.CompletedTask;
            ConnectionTestUtils.BuildTcpListener(
                out int port,
                out TcpListener tcpListener,
                out TcpListener ipV6TcpListener);

            string originalAggressiveTimeoutDetection = Environment.GetEnvironmentVariable(
                Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled);
            SystemUtilizationReaderBase.ApplySingletonOverride(
                readerOverride: new DummySystemUtilizationReader(
                    cpuUsage: isCpuBeyondThreshold ? 99.3F : 14.34F));
            try
            {
                Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled, "true");
                Func<string, X509Certificate2> clientCertificateFunction = _ => certificate;
                using (Connection connection = new(
                    serverUri: new Uri($"tcp://localhost:{port}"),
                    hostNameCertificateOverride: "127.0.0.1",
                    receiveHangDetectionTime: TimeSpan.FromSeconds(11.0),
                    sendHangDetectionTime: TimeSpan.FromSeconds(3.0),
                    idleTimeout: TimeSpan.FromSeconds(1800.0),
                    memoryStreamPool: new TestMemoryStreamPool(false),
                    remoteCertificateValidationCallback: ConnectionTestUtils.SelfSignedForLocalhost,
                    clientCertificateFunction,
                    dnsResolutionFunction: null))
                {
                    // Arrange.
                    string testText = $"{Guid.NewGuid()}{DateTime.Now}";

                    // RNTBD can open either IPv4 or IPv6 connection
                    startListener = ConnectionTestUtils.StartProcessingConnectionAsync(
                        listener: tcpListener,
                        certificate: certificate,
                        testText: testText, cancellationToken: listenerCancellation.Token);

                    startIpV6Listener = ConnectionTestUtils.StartProcessingConnectionAsync(
                        listener: ipV6TcpListener,
                        certificate: certificate,
                        testText: testText, cancellationToken: listenerCancellation.Token);

                    // Let some time for a server to start.
                    await Task.Delay(1000);

                    ChannelOpenArguments[] channelArguments = new[]
                    {
                        this.BuildChannelOpenArguments(),
                        this.BuildChannelOpenArguments(),
                        this.BuildChannelOpenArguments(),
                        this.BuildChannelOpenArguments(),
                    };

                    TransportSerialization.SerializedRequest[] requests = new[]
                    {
                        this.BuildSerilizedRequest(),
                        this.BuildSerilizedRequest(),
                        this.BuildSerilizedRequest(),
                    };

                    // Act.
                    await connection
                        .OpenAsync(
                            args: channelArguments[0]);

                    // A delay of 'delayInSecs' seconds was added to validate different transit timeout flows.
                    TestUtils.AddSecondsToDateTimeFieldUsingReflection(
                                    objectName: connection,
                                    fieldName: "lastReceiveTime",
                                    delayInSeconds: delayInSecs);

                    for (int i = 0; i < transitTimeoutCounter; i++)
                    {
                        connection.NotifyConnectionStatus(
                            isCompleted: false,
                            isReadRequest: isReadOnlyRequest);
                    }

                    await connection
                        .WriteRequestAsync(
                            args: channelArguments[1].CommonArguments,
                            messagePayload: requests[0],
                            transportRequestStats: null);

                    // Assert.
                    string assertMessage = $"The connection is marked {(isConnectionHealthy ? "healthy" : "un-healthy")} as the transit timeout validation {(isConnectionHealthy ? "has skipped" : "has failed")}.";
                    Assert.AreEqual(
                        expected: isConnectionHealthy,
                        actual: connection.Healthy,
                        message: assertMessage);
                }

                Assert.AreEqual(0, Connection.NumberOfOpenTcpConnections);
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
                    SystemUtilizationReaderBase.ApplySingletonOverride(readerOverride: null);
                    Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled, originalAggressiveTimeoutDetection);
                }
            }
        }

        /// <summary>
        /// <see cref="ChannelCommonArguments"/> tracks state in a single long (packing two values into it), managed by <see cref="Interlocked"/> operations.
        /// 
        /// This tests that packing.
        /// </summary>
        // https://msdata.visualstudio.com/CosmosDB/_workitems/edit/3921630
        [Owner("kmontrose")]
        [TestMethod]
        [TestCategory("Integration")]
        public void TestChannelCommonArgumentsPacking()
        {
            // SetTimeoutCode
            {
                ChannelCommonArguments args = new(Guid.NewGuid(), TransportErrorCode.ChannelOpenTimeout, false);

                args.SnapshotCallState(out TransportErrorCode initCode, out bool initSent);
                Assert.AreEqual(TransportErrorCode.ChannelOpenTimeout, initCode);
                Assert.IsFalse(initSent);

                args.SetTimeoutCode(TransportErrorCode.SendLockTimeout);
                args.SnapshotCallState(out TransportErrorCode setCode, out bool setSent);
                Assert.AreEqual(TransportErrorCode.SendLockTimeout, setCode);
                Assert.IsFalse(setSent);
            }

            // SetPayloadSent
            {
                ChannelCommonArguments args = new(Guid.NewGuid(), TransportErrorCode.SslNegotiationTimeout, false);

                args.SnapshotCallState(out TransportErrorCode initCode, out bool initSent);
                Assert.AreEqual(TransportErrorCode.SslNegotiationTimeout, initCode);
                Assert.IsFalse(initSent);

                args.SetPayloadSent();
                args.SnapshotCallState(out TransportErrorCode setCode, out bool setSent);
                Assert.AreEqual(TransportErrorCode.SslNegotiationTimeout, setCode);
                Assert.IsTrue(setSent);

                // send call should fail, how depends on if we're in DEBUG or not
                bool throwsException;
                try
                {
                    args.SetPayloadSent();
                    throwsException = false;
                }
                catch
                {
                    throwsException = true;
                }

                Assert.IsTrue(throwsException, "Expected failure if SetPayloadSent called twice");
            }
        }

        /// <summary>
        /// Verifies that TCP keepalive socket options are set using the int overload
        /// of SetSocketOption, not the object overload. Passing uint values resolves
        /// to the object overload due to C# overload resolution (uint cannot implicitly
        /// narrow to int), which throws ArgumentException.
        /// </summary>
        [Owner("aavasthy")]
        [TestMethod]
        public void SetSocketOption_WithUIntValue_ThrowsArgumentException()
        {
            // These match the values in Connection.cs
            const uint TcpKeepAliveTimeSocketOptionEnumValue = 3;
            const uint TcpKeepAliveIntervalSocketOptionEnumValue = 17;
            const uint KeepAliveTime = 30;
            const uint KeepAliveInterval = 1;

            using (Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                // Without (int) cast: uint boxes to object, resolves to
                // SetSocketOption(level, name, object) which rejects boxed uint.
                Assert.ThrowsException<ArgumentException>(() =>
                    socket.SetSocketOption(
                        SocketOptionLevel.Tcp,
                        (SocketOptionName)TcpKeepAliveTimeSocketOptionEnumValue,
                        KeepAliveTime));

                Assert.ThrowsException<ArgumentException>(() =>
                    socket.SetSocketOption(
                        SocketOptionLevel.Tcp,
                        (SocketOptionName)TcpKeepAliveIntervalSocketOptionEnumValue,
                        KeepAliveInterval));
            }
        }

        [Owner("aavasthy")]
        [TestMethod]
        public void SetSocketOption_ArgumentException_IsNotSocketException()
        {
            const uint TcpKeepAliveTimeSocketOptionEnumValue = 3;
            const uint keepAliveTimeAsUInt = 30;

            using (Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                bool caughtBySocketException = false;
                bool caughtByGeneralException = false;
                Exception caughtException = null;

                try
                {
                    socket.SetSocketOption(SocketOptionLevel.Tcp,
                        (SocketOptionName)TcpKeepAliveTimeSocketOptionEnumValue,
                        keepAliveTimeAsUInt);
                }
                catch (SocketException)
                {
                    caughtBySocketException = true;
                }
                catch (Exception ex)
                {
                    caughtByGeneralException = true;
                    caughtException = ex;
                }

                // The uint bug must fall through catch(SocketException)
                // and land in catch(Exception) as ArgumentException.
                Assert.IsFalse(caughtBySocketException,
                    "ArgumentException from uint must NOT be caught by catch(SocketException).");
                Assert.IsTrue(caughtByGeneralException,
                    "ArgumentException from uint must be caught by catch(Exception).");
                Assert.IsInstanceOfType(caughtException, typeof(ArgumentException));
            }
        }

        /// <summary>
        /// Calls the actual Connection.SetKeepAliveSocketOptions() method via reflection,
        /// then reads back the TCP keepalive values using GetSocketOption to verify they
        /// were applied — same pattern as the #if DEBUG validation block in Connection.cs.
        /// </summary>
        [Owner("aavasthy")]
        [TestMethod]
        public void SetKeepAliveSocketOptions_ValuesAreAppliedOnSocket()
        {
            const uint TcpKeepAliveTimeSocketOptionEnumValue = 3;
            const uint TcpKeepAliveIntervalSocketOptionEnumValue = 17;

            using (Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                // Call the actual Connection.SetKeepAliveSocketOptions(socket) via reflection.
                MethodInfo setKeepAliveMethod = typeof(Connection).GetMethod(
                    "SetKeepAliveSocketOptions",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(setKeepAliveMethod, "SetKeepAliveSocketOptions method not found on Connection.");

                try
                {
                    setKeepAliveMethod.Invoke(null, new object[] { socket });
                }
                catch (TargetInvocationException ex) when (ex.InnerException is SocketException)
                {
                    // Platform does not support TCP_KEEPIDLE/TCP_KEEPINTVL — skip gracefully.
                    return;
                }

                // Read back and verify — mirrors the #if DEBUG block in Connection.cs.
                int tcpKeepAliveTime = (int)socket.GetSocketOption(
                    SocketOptionLevel.Tcp,
                    (SocketOptionName)TcpKeepAliveTimeSocketOptionEnumValue);
                int tcpKeepAliveInterval = (int)socket.GetSocketOption(
                    SocketOptionLevel.Tcp,
                    (SocketOptionName)TcpKeepAliveIntervalSocketOptionEnumValue);

                // Read expected values from Connection's static fields via reflection.
                FieldInfo keepAliveTimeField = typeof(Microsoft.Azure.Documents.Rntbd.Connection).GetField(
                    "SocketOptionTcpKeepAliveTime",
                    BindingFlags.NonPublic | BindingFlags.Static);
                FieldInfo keepAliveIntervalField = typeof(Microsoft.Azure.Documents.Rntbd.Connection).GetField(
                    "SocketOptionTcpKeepAliveInterval",
                    BindingFlags.NonPublic | BindingFlags.Static);

                int expectedTime = (int)keepAliveTimeField.GetValue(null);
                int expectedInterval = (int)keepAliveIntervalField.GetValue(null);

                Assert.AreEqual(expectedTime, tcpKeepAliveTime,
                    "TcpKeepAliveTime was not applied to the socket.");
                Assert.AreEqual(expectedInterval, tcpKeepAliveInterval,
                    "TcpKeepAliveInterval was not applied to the socket.");
            }
        }

        private static async Task StartProcessingConnectionAsync(TcpListener listener, X509Certificate2 certificate, string testText)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            try
            {
                SslStream sslStream = new(client.GetStream(), leaveInnerStreamOpen: false);
                sslStream.AuthenticateAsServer(
                    serverCertificate: certificate,
                    clientCertificateRequired: false,
                    enabledSslProtocols: SslProtocols.Tls12,
                    checkCertificateRevocation: false);
                byte[] message = Encoding.UTF8.GetBytes(testText);
                await sslStream.WriteAsync(BitConverter.GetBytes(message.Length), offset: 0, count: sizeof(int));
                await sslStream.WriteAsync(message, offset: 0, count: message.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static TcpListener BuildTcpListener(
            out int port,
            out TcpListener tcpListener,
            out TcpListener ipV6TcpListener)
        {
            port = 15000;
            tcpListener = null;
            ipV6TcpListener = null;

            while (port < 15100)
            {
                try
                {
                    IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);
                    tcpListener = new TcpListener(ipEndPoint);
                    IPEndPoint ipV6EndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
                    tcpListener = new TcpListener(ipEndPoint);
                    ipV6TcpListener = new TcpListener(ipV6EndPoint);
                    tcpListener.Start();
                    ipV6TcpListener.Start();
                    break;
                }
                catch (SocketException)
                {
                    // Try other port.
                }

                port++;
            }

            if (tcpListener == null)
            {
                throw new Exception($"Failed to bind to a local port in range 15000 .. 15100");
            }

            return tcpListener;
        }

        private class TestMemoryStream : MemoryStream
        {
        }

        private class TestMemoryStreamPool : MemoryStreamPool
        {
            private bool generateStream;

            public TestMemoryStreamPool(bool generateStream)
            {
                this.generateStream = generateStream;
            }

            public bool TryGetMemoryStream(int capacity, out MemoryStream memoryStream)
            {
                if (generateStream)
                {
                    memoryStream = new TestMemoryStream();
                    return true;
                }
                else
                {
                    memoryStream = null;
                    return false;
                }
            }
        }

        private static bool SelfSignedForLocalhost(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;

        private ChannelOpenArguments BuildChannelOpenArguments() => new(
                activityId: Guid.NewGuid(),
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(5),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

        private TransportSerialization.SerializedRequest BuildSerilizedRequest() => new(
                requestHeader: new BufferProvider().GetBuffer(10),
                requestBody: new CloneableStream(new MemoryStream()));
    }
}