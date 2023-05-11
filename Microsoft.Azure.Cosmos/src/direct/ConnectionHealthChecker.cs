//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Sockets;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// ConnectionHealthChecker listens to the connection reset event notification fired by the transport client
    /// and refreshes the Document client's address cache
    /// </summary>
    internal sealed class ConnectionHealthChecker
    {
        private const int MinNumberOfSendsSinceLastReceiveForUnhealthyConnection = 3;

        // The connection will declare itself unhealthy if the
        // (lastSendTime - lastReceiveTime) gap grows beyond this value.
        // receiveDelayLimit must be greater than receiveHangGracePeriod.
        private readonly TimeSpan receiveDelayLimit;

        // The connection will declare itself unhealthy if the
        // (lastSendAttemptTime - lastSendTime) gap grows beyond this value.
        // sendDelayLimit must be greater than sendHangGracePeriod.
        private readonly TimeSpan sendDelayLimit;

        private readonly TimeSpan idleConnectionTimeout;

        private readonly TimeSpan timeoutDetectionTimeLimit;
        private readonly int timeoutDetectionOnWriteThreshold;
        private readonly TimeSpan timeoutDetectionOnWriteTimeLimit;
        private readonly int timeoutDetectionHighFrequencyThreshold;
        private readonly TimeSpan timeoutDetectionHighFrequencyTimeLimit;
        private readonly double timeoutDetectionDisableCPUThreshold;
        private readonly SystemUtilizationReaderBase systemUtilizationReader = SystemUtilizationReaderBase.SingletonInstance;

        // The connection should not declare itself unhealthy if a send was
        // attempted very recently. As such, ignore
        // (lastSendAttemptTime - lastSendTime) gaps lower than sendHangGracePeriod.
        // The grace period should be large enough to accommodate slow sends.
        // In effect, a setting of 2s requires the client to be able to send
        // data at least at 1 MB/s for 2 MB documents.
        private static readonly TimeSpan sendHangGracePeriod = TimeSpan.FromSeconds(2.0);

        // The connection should not declare itself unhealthy if a send
        // succeeded very recently. As such, ignore
        // (lastSendTime - lastReceiveTime) gaps lower than receiveHangGracePeriod.
        // The grace period should be large enough to accommodate the round trip
        // time of the slowest server request. Assuming 1s of network RTT,
        // a 2 MB request, a 2 MB response, a connection that can sustain
        // 1 MB/s both ways, and a 5-second deadline at the server, 10 seconds
        // should be enough.
        private static readonly TimeSpan receiveHangGracePeriod = TimeSpan.FromSeconds(10.0);

        private static readonly TimeSpan recentReceiveWindow = TimeSpan.FromSeconds(1.0);

        private static readonly byte[] healthCheckBuffer = new byte[1];

        /// <summary>
        /// Ctor.
        /// </summary>
        /// <param name="sendDelayLimit"></param>
        /// <param name="receiveDelayLimit"></param>
        /// <param name="idleConnectionTimeout"></param>
        /// <param name="timeoutDetectionEnabled"></param>
        public ConnectionHealthChecker(
            TimeSpan sendDelayLimit,
            TimeSpan receiveDelayLimit,
            TimeSpan idleConnectionTimeout,
            bool timeoutDetectionEnabled)
        {
            if (receiveDelayLimit <= ConnectionHealthChecker.receiveHangGracePeriod)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(receiveDelayLimit),
                    receiveDelayLimit,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must be greater than {1} ({2})",
                        nameof(receiveDelayLimit),
                        nameof(ConnectionHealthChecker.receiveHangGracePeriod),
                        ConnectionHealthChecker.receiveHangGracePeriod));
            }
            if (sendDelayLimit <= ConnectionHealthChecker.sendHangGracePeriod)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sendDelayLimit),
                    sendDelayLimit,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} must be greater than {1} ({2})",
                        nameof(sendDelayLimit),
                        nameof(ConnectionHealthChecker.sendHangGracePeriod),
                        ConnectionHealthChecker.sendHangGracePeriod));
            }

            this.sendDelayLimit = sendDelayLimit;
            this.receiveDelayLimit = receiveDelayLimit;
            this.idleConnectionTimeout = idleConnectionTimeout;

            if (timeoutDetectionEnabled)
            {
                this.timeoutDetectionTimeLimit = TimeSpan.FromSeconds(
                    Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionTimeLimit,
                        defaultValue: 60));
                this.timeoutDetectionOnWriteThreshold = Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnWriteThreshold,
                        defaultValue: 1);
                this.timeoutDetectionOnWriteTimeLimit = TimeSpan.FromSeconds(
                    Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnWriteTimeLimit,
                        defaultValue: 6));
                this.timeoutDetectionHighFrequencyThreshold = Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnHighFrequencyThreshold,
                        defaultValue: 3);
                this.timeoutDetectionHighFrequencyTimeLimit = TimeSpan.FromSeconds(
                    Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnHighFrequencyTimeLimit,
                        defaultValue: 10));
                this.timeoutDetectionDisableCPUThreshold = Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionDisabledOnCPUThreshold,
                        defaultValue: 90);
            }
        }

        /// <summary>
        /// Is Healthy Indicator.
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="lastSendAttempt"></param>
        /// <param name="lastSend"></param>
        /// <param name="lastReceive"></param>
        /// <param name="firstSendSinceLastReceive"></param>
        /// <param name="numberOfSendsSinceLastReceive"></param>
        /// <param name="transitTimeoutCounter"></param>
        /// <param name="transitTimeoutWriteCounter"></param>
        /// <param name="socket"></param>
        /// <returns>A boolean flag.</returns>
        public bool IsHealthy(
            DateTime currentTime,
            DateTime lastSendAttempt,
            DateTime lastSend,
            DateTime lastReceive,
            DateTime? firstSendSinceLastReceive,
            long numberOfSendsSinceLastReceive,
            int transitTimeoutCounter,
            int transitTimeoutWriteCounter,
            Socket socket)
        {
            // Assume that the connection is healthy if data was received
            // recently.
            if (this.IsDataReceivedRecently(
                currentTime: currentTime,
                lastReceiveTime: lastReceive))
            {
                return true;
            }

            if (this.IsBlackholeDetected(
                currentTime: currentTime,
                lastSendAttempt: lastSendAttempt,
                lastSend: lastSend,
                lastReceive: lastReceive,
                firstSendSinceLastReceive: firstSendSinceLastReceive,
                numberOfSendsSinceLastReceive: numberOfSendsSinceLastReceive))
            {
                return false;
            }

            if (this.IsTransitTimeoutsDetected(
                transitTimeoutCounter: transitTimeoutCounter,
                transitTimeoutWriteCounter: transitTimeoutWriteCounter,
                currentTime: currentTime,
                lastReadTime: lastReceive))
            {
                return false;
            }

            if (this.IsConnectionIdled(
                currentTime: currentTime,
                lastReceive: lastReceive))
            {
                return true;
            }

            // See https://aka.ms/zero-byte-send.
            // Socket.Send is expensive. Keep this operation last in the chain
            return this.IsSocketConnectionEstablished(
                socket: socket);
        }

        /// <summary>
        /// blabla,
        /// </summary>
        /// <param name="transitTimeoutCounter"></param>
        /// <param name="transitTimeoutWriteCounter"></param>
        /// <param name="currentTime"></param>
        /// <param name="lastReadTime"></param>
        /// <returns>Bool.</returns>
        private bool IsTransitTimeoutsDetected(
            int transitTimeoutCounter,
            int transitTimeoutWriteCounter,
            DateTime currentTime,
            DateTime lastReadTime)
        {
            if (transitTimeoutCounter > 0)
            {
                // Transit timeout can be a normal symptom under high CPU load. When request timeout due to high CPU,
                // close the existing the connection and re-establish a new one will not help the issue but rather make it worse.
                // Therefore, the timeout detection will be disabled in case of high cpu detection.
                if (this.systemUtilizationReader.GetSystemWideCpuUsage() > this.timeoutDetectionDisableCPUThreshold)
                {
                    return false;
                }

                TimeSpan readDelay = currentTime - lastReadTime;

                // The channel will be closed if all requests are failed due to transit timeout within the time limit.
                // This helps to close channel faster for sparse workload.
                if (readDelay >= this.timeoutDetectionTimeLimit)
                {
                    DefaultTrace.TraceWarning(
                        $"Unhealthy RNTBD connection: Health check failed due to transit timeout detection time limit. " +
                        $"Last channel read: {lastReadTime}. Timeout detection time limit: {this.timeoutDetectionTimeLimit}. ");
                    return true;
                }

                // Timeout detection in high frequency.
                if (transitTimeoutCounter >= this.timeoutDetectionHighFrequencyThreshold &&
                    readDelay >= this.timeoutDetectionHighFrequencyTimeLimit)
                {
                    DefaultTrace.TraceWarning(
                        "Unhealthy RNTBD connection: Transit timeout high frequency threshold hit. " +
                        $"Last channel read: {lastReadTime}. Timeout counts: {transitTimeoutCounter}. " +
                        $"Timeout detection high frequency threshold: {this.timeoutDetectionHighFrequencyThreshold}. Timeout detection high frequency time limit: {this.timeoutDetectionHighFrequencyTimeLimit}. ");
                    return true;
                }

                // Timeout detection in write operation.
                if (transitTimeoutWriteCounter >= this.timeoutDetectionOnWriteThreshold &&
                    readDelay >= this.timeoutDetectionOnWriteTimeLimit)
                {
                    DefaultTrace.TraceWarning(
                        "Unhealthy RNTBD connection: Transit timeout on write threshold hit: {0}. " +
                        $"Last channel read: {lastReadTime}. Write timeout counts: {transitTimeoutWriteCounter}. " +
                        $"Timeout detection on write threshold: {this.timeoutDetectionOnWriteThreshold}. Timeout detection on write time limit: {this.timeoutDetectionOnWriteTimeLimit}. ");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// blabla.
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="lastSendAttempt"></param>
        /// <param name="lastSend"></param>
        /// <param name="lastReceive"></param>
        /// <param name="firstSendSinceLastReceive"></param>
        /// <param name="numberOfSendsSinceLastReceive"></param>
        /// <returns>bool.</returns>
        private bool IsBlackholeDetected(
            DateTime currentTime,
            DateTime lastSendAttempt,
            DateTime lastSend,
            DateTime lastReceive,
            DateTime? firstSendSinceLastReceive,
            long numberOfSendsSinceLastReceive)
        {
            // Black hole detection, part 1:
            // Treat the connection as unhealthy if the gap between the last
            // attempted send and the last successful send grew beyond
            // acceptable limits, unless a send was attempted very recently.
            // This is a sign of a hung send().
            if ((lastSendAttempt - lastSend > this.sendDelayLimit) &&
                (currentTime - lastSendAttempt > ConnectionHealthChecker.sendHangGracePeriod))
            {
                DefaultTrace.TraceWarning(
                    "Unhealthy RNTBD connection: Hung send: {0}. " +
                    "Last send attempt: {1:o}. Last send: {2:o}. " +
                    "Tolerance {3:c}");
                return true;
            }

            // Black hole detection, part 2:
            // Treat the connection as unhealthy if the gap between the last
            // successful send and the last successful receive grew beyond
            // acceptable limits, unless a send succeeded very recently and the number of
            // outstanding receives is within reasonable limits.
            if ((lastSend - lastReceive > this.receiveDelayLimit) &&
                (
                    currentTime - lastSend > ConnectionHealthChecker.receiveHangGracePeriod ||
                    (
                        numberOfSendsSinceLastReceive >= ConnectionHealthChecker.MinNumberOfSendsSinceLastReceiveForUnhealthyConnection &&
                        firstSendSinceLastReceive != null &&
                        currentTime - firstSendSinceLastReceive > ConnectionHealthChecker.receiveHangGracePeriod
                    )
                ))
            {
                DefaultTrace.TraceWarning(
                    "Unhealthy RNTBD connection: Replies not getting back: {0}. " +
                    "Last send: {1:o}. Last receive: {2:o}. Tolerance: {3:c}. " +
                    "First send since last receieve: {4:o}. # of sends since last receive: {5}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// blabla.
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="lastReceiveTime"></param>
        /// <returns>A bool.</returns>
        private bool IsDataReceivedRecently(
            DateTime currentTime,
            DateTime lastReceiveTime)
        {
            return currentTime - lastReceiveTime < ConnectionHealthChecker.recentReceiveWindow;
        }

        /// <summary>
        /// blabla.
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="lastReceive"></param>
        /// <returns>A bool.</returns>
        private bool IsConnectionIdled(
            DateTime currentTime,
            DateTime lastReceive)
        {
            if (this.idleConnectionTimeout > TimeSpan.Zero)
            {
                // idle timeout is enabled
                if (currentTime - lastReceive > this.idleConnectionTimeout)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// blabla.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns>A bool.</returns>
        private bool IsSocketConnectionEstablished(
            Socket socket)
        {
            try
            {
                if (socket == null || !socket.Connected)
                {
                    return false;
                }
                Debug.Assert(!socket.Blocking);
                socket.Send(ConnectionHealthChecker.healthCheckBuffer, 0, 0);
                return true;
            }
            catch (SocketException e)
            {
                bool healthy = e.SocketErrorCode == SocketError.WouldBlock;
                if (!healthy)
                {
                    DefaultTrace.TraceWarning(
                        "Unhealthy RNTBD connection. Socket error code: {0}",
                        e.SocketErrorCode.ToString());
                }
                return healthy;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }
}
