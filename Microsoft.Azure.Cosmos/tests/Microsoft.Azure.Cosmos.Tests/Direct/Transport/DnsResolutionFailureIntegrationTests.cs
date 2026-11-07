//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DnsResolutionFailureIntegrationTests
    {
        [TestMethod]
        [Owner("trivediyash")]
        [TestCategory("Integration")]
        public async Task Connection_OpenSocketAsync_WithInvalidHostName_ThrowsTransportException()
        {
            // Arrange
            Uri invalidUri = new Uri("https://this-host-does-not-exist-12345.documents.azure.com:443/");
            MemoryStreamPool memoryStreamPool = null;

            Connection connection = new Connection(
                serverUri: invalidUri,
                hostNameCertificateOverride: null,
                receiveHangDetectionTime: TimeSpan.FromSeconds(60),
                sendHangDetectionTime: TimeSpan.FromSeconds(60),
                idleTimeout: TimeSpan.FromMinutes(5),
                memoryStreamPool: memoryStreamPool,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                dnsResolutionFunction: (hostName) =>
                {
                    // Simulate DNS resolution failure
                    throw new SocketException((int)SocketError.HostNotFound);
                });

            Guid activityId = Guid.NewGuid();
            ChannelOpenArguments args = new ChannelOpenArguments(
                activityId: activityId,
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(30),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

            // Act & Assert
            TransportException exception = await Assert.ThrowsExceptionAsync<TransportException>(
                async () => await connection.OpenAsync(args));

            Assert.AreEqual(TransportErrorCode.DnsResolutionFailed, exception.ErrorCode);
            Assert.IsInstanceOfType(exception.InnerException, typeof(SocketException));

            // Cleanup
            if (!connection.Disposed)
            {
                connection.Dispose();
            }
        }

        [TestMethod]
        [Owner("trivediyash")]
        [TestCategory("Integration")]
        public async Task Connection_OpenSocketAsync_WithDnsTimeout_ThrowsTransportException()
        {
            // Arrange
            Uri uri = new Uri("https://test.documents.azure.com:443/");
            MemoryStreamPool memoryStreamPool = null;

            Connection connection = new Connection(
                serverUri: uri,
                hostNameCertificateOverride: null,
                receiveHangDetectionTime: TimeSpan.FromSeconds(60),
                sendHangDetectionTime: TimeSpan.FromSeconds(60),
                idleTimeout: TimeSpan.FromMinutes(5),
                memoryStreamPool: memoryStreamPool,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                dnsResolutionFunction: async (hostName) =>
                {
                    // Simulate DNS timeout
                    await Task.Delay(100);
                    throw new SocketException((int)SocketError.TimedOut);
                });

            Guid activityId = Guid.NewGuid();
            ChannelOpenArguments args = new ChannelOpenArguments(
                activityId: activityId,
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(30),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

            // Act & Assert
            TransportException exception = await Assert.ThrowsExceptionAsync<TransportException>(
                async () => await connection.OpenAsync(args));

            Assert.AreEqual(TransportErrorCode.DnsResolutionFailed, exception.ErrorCode);

            // Cleanup
            if (!connection.Disposed)
            {
                connection.Dispose();
            }
        }

        [TestMethod]
        [Owner("trivediyash")]
        [TestCategory("Integration")]
        public async Task Connection_OpenSocketAsync_WithGenericDnsException_ThrowsTransportException()
        {
            // Arrange
            Uri uri = new Uri("https://test.documents.azure.com:443/");
            MemoryStreamPool memoryStreamPool = null;

            Connection connection = new Connection(
                serverUri: uri,
                hostNameCertificateOverride: null,
                receiveHangDetectionTime: TimeSpan.FromSeconds(60),
                sendHangDetectionTime: TimeSpan.FromSeconds(60),
                idleTimeout: TimeSpan.FromMinutes(5),
                memoryStreamPool: memoryStreamPool,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                dnsResolutionFunction: (hostName) =>
                {
                    // Simulate generic DNS failure
                    throw new Exception("DNS resolution error");
                });

            Guid activityId = Guid.NewGuid();
            ChannelOpenArguments args = new ChannelOpenArguments(
                activityId: activityId,
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(30),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

            // Act & Assert
            TransportException exception = await Assert.ThrowsExceptionAsync<TransportException>(
                async () => await connection.OpenAsync(args));

            Assert.AreEqual(TransportErrorCode.DnsResolutionFailed, exception.ErrorCode);
            Assert.IsNotNull(exception.InnerException);
            Assert.AreEqual("DNS resolution error", exception.InnerException.Message);

            // Cleanup
            if (!connection.Disposed)
            {
                connection.Dispose();
            }
        }

        [TestMethod]
        [Owner("trivediyash")]
        [TestCategory("Integration")]
        public async Task Connection_OpenSocketAsync_AfterDnsFailure_IncludesActivityIdInException()
        {
            // Arrange
            Guid expectedActivityId = Guid.NewGuid();
            Uri uri = new Uri("https://test.documents.azure.com:443/");
            MemoryStreamPool memoryStreamPool = null;

            Connection connection = new Connection(
                serverUri: uri,
                hostNameCertificateOverride: null,
                receiveHangDetectionTime: TimeSpan.FromSeconds(60),
                sendHangDetectionTime: TimeSpan.FromSeconds(60),
                idleTimeout: TimeSpan.FromMinutes(5),
                memoryStreamPool: memoryStreamPool,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                dnsResolutionFunction: (hostName) =>
                {
                    // Simulate DNS resolution failure
                    throw new SocketException((int)SocketError.HostNotFound);
                });

            ChannelOpenArguments args = new ChannelOpenArguments(
                activityId: expectedActivityId,
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(30),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

            // Act & Assert
            TransportException exception = await Assert.ThrowsExceptionAsync<TransportException>(
                async () => await connection.OpenAsync(args));

            Assert.AreEqual(TransportErrorCode.DnsResolutionFailed, exception.ErrorCode);
            Assert.AreEqual(expectedActivityId, exception.ActivityId);

            // Cleanup
            if (!connection.Disposed)
            {
                connection.Dispose();
            }
        }

        [TestMethod]
        [Owner("trivediyash")]
        [TestCategory("Integration")]
        public async Task Connection_OpenSocketAsync_SuccessfulDns_DoesNotThrowDnsException()
        {
            // Arrange
            Uri uri = new Uri("https://test.documents.azure.com:443/");
            MemoryStreamPool memoryStreamPool = null;
            IPAddress testAddress = IPAddress.Parse("127.0.0.1");

            Connection connection = new Connection(
                serverUri: uri,
                hostNameCertificateOverride: null,
                receiveHangDetectionTime: TimeSpan.FromSeconds(60),
                sendHangDetectionTime: TimeSpan.FromSeconds(60),
                idleTimeout: TimeSpan.FromMinutes(5),
                memoryStreamPool: memoryStreamPool,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                dnsResolutionFunction: (hostName) =>
                {
                    return Task.FromResult(testAddress);
                });

            Guid activityId = Guid.NewGuid();
            ChannelOpenArguments args = new ChannelOpenArguments(
                activityId: activityId,
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(30),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

            // Act & Assert
            // This will fail at TCP connect or SSL handshake, but NOT at DNS resolution
            Exception exception = null;
            try
            {
                await connection.OpenAsync(args);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // If we get a TransportException, it should NOT be a DNS resolution failure
            if (exception is TransportException transportEx)
            {
                Assert.IsFalse(TransportException.IsDNSException(transportEx.ErrorCode),
                    "Should not throw DNS exception when DNS succeeds");
            }

            // Cleanup
            if (!connection.Disposed)
            {
                connection.Dispose();
            }
        }

        [TestMethod]
        [Owner("trivediyash")]
        [TestCategory("Integration")]
        public async Task Connection_OpenSocketAsync_DnsFailure_PreservesExceptionChain()
        {
            // Arrange
            Uri uri = new Uri("https://test.documents.azure.com:443/");
            MemoryStreamPool memoryStreamPool = null;
            SocketException innerSocketException = new SocketException((int)SocketError.HostNotFound);

            Connection connection = new Connection(
                serverUri: uri,
                hostNameCertificateOverride: null,
                receiveHangDetectionTime: TimeSpan.FromSeconds(60),
                sendHangDetectionTime: TimeSpan.FromSeconds(60),
                idleTimeout: TimeSpan.FromMinutes(5),
                memoryStreamPool: memoryStreamPool,
                remoteCertificateValidationCallback: null,
                clientCertificateFunction: null,
                dnsResolutionFunction: (hostName) =>
                {
                    throw innerSocketException;
                });

            Guid activityId = Guid.NewGuid();
            ChannelOpenArguments args = new ChannelOpenArguments(
                activityId: activityId,
                openTimeline: new ChannelOpenTimeline(),
                openTimeout: TimeSpan.FromSeconds(30),
                portReuseMode: PortReuseMode.ReuseUnicastPort,
                userPortPool: null,
                callerId: RntbdConstants.CallerId.Anonymous);

            // Act & Assert
            TransportException exception = await Assert.ThrowsExceptionAsync<TransportException>(
                async () => await connection.OpenAsync(args));

            // Verify exception chain is preserved
            Assert.AreEqual(TransportErrorCode.DnsResolutionFailed, exception.ErrorCode);
            Assert.IsNotNull(exception.InnerException);
            Assert.AreSame(innerSocketException, exception.InnerException,
                "Original socket exception should be preserved in the exception chain");
            Assert.AreEqual(SocketError.HostNotFound, ((SocketException)exception.InnerException).SocketErrorCode);

            // Cleanup
            if (!connection.Disposed)
            {
                connection.Dispose();
            }
        }
    }
}