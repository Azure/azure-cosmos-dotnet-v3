//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// ConnectionHealthChecker is responsible to validate a connection health.
    /// </summary>
    internal sealed class ConnectionHealthChecker
    {
        /// <summary>
        /// A constant integer defining the limit for minimum number of sends since last receive. The default value is 3.
        /// </summary>
        private const int MinNumberOfSendsSinceLastReceiveForUnhealthyConnection = 3;

        /// <summary>
        /// A boolean field indicating if the aggressive timeout detection is enabled.
        /// </summary>
        private const bool AggressiveTimeoutDetectionEnabledDefaultValue = false;

        /// <summary>
        /// A constant integer defining the default value for aggressive timeout detection time limit in seconds.
        /// </summary>
        private const int TimeoutDetectionTimeLimitDefaultValueInSeconds = 60;

        /// <summary>
        /// A contant integer defining the default value for the timeout detection threshold on the write path.
        /// </summary>
        private const int TimeoutDetectionOnWriteThresholdDefaultValue = 1;

        /// <summary>
        /// A constant integer defining the default value for the timeout detection time limit in seconds on the write path.
        /// </summary>
        private const int TimeoutDetectionOnWriteTimeLimitDefaultValueInSeconds = 6;

        /// <summary>
        /// A constant integer defining the default value for the timeout detection threshold on the high frequency path.
        /// </summary>
        private const int TimeoutDetectionOnHighFrequencyThresholdDefaultValue = 3;

        /// <summary>
        /// A constant integer defining the default value for the timeout detection time limit in seconds on the high frequency path.
        /// </summary>
        private const int TimeoutDetectionOnHighFrequencyTimeLimitDefaultValueInSeconds = 10;

        /// <summary>
        /// A constant integer defining the default value for the CPU utilization threshold to skip the aggressive timeout validation.
        /// </summary>
        private const int TimeoutDetectionDisabledOnCPUThresholdDefaultValue = 90;

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the cpu usage cache eviction time in seconds. The default value is 10 seconds.
        /// </summary>
        private static readonly TimeSpan TimeoutDetectionCPUUsageCacheEvictionTimeInSeconds = TimeSpan.FromSeconds(10.0);

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the send hang grace period. The connection should not
        /// declare itself unhealthy if a send was attempted very recently. As such, ignore (lastSendAttemptTime - lastSendTime)
        /// gaps lower than sendHangGracePeriod. The grace period should be large enough to accommodate slow sends.
        /// In effect, a setting of 2s requires the client to be able to send data at least at 1 MB/s for 2 MB documents.
        /// The default value is 2 seconds.
        /// </summary>
        private static readonly TimeSpan sendHangGracePeriod = TimeSpan.FromSeconds(2.0);

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the receive hang grace period. The connection should not declare
        /// itself unhealthy if a send succeeded very recently. As such, ignore (lastSendTime - lastReceiveTime) gaps lower
        /// than receiveHangGracePeriod. The grace period should be large enough to accommodate the round trip time of the
        /// slowest server request. Assuming 1s of network RTT, a 2 MB request, a 2 MB response, a connection that can sustain
        /// 1 MB/s both ways, and a 5-second deadline at the server, 10 seconds should be enough. The default value is 10 seconds.
        /// </summary>
        private static readonly TimeSpan receiveHangGracePeriod = TimeSpan.FromSeconds(10.0);

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the recent receive window time limit. The default value is 1 second.
        /// </summary>
        private static readonly TimeSpan recentReceiveWindow = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// A read-only byte array of one byte to validate a socket opening.
        /// </summary>
        private static readonly byte[] healthCheckBuffer = new byte[1];

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the receive delay limit. The connection will declare itself unhealthy if the
        /// (lastSendTime - lastReceiveTime) gap grows beyond this value. receiveDelayLimit must be greater than receiveHangGracePeriod.
        /// </summary>
        private readonly TimeSpan receiveDelayLimit;

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the send delay limit. The connection will declare itself unhealthy if the
        /// (lastSendAttemptTime - lastSendTime) gap grows beyond this value. sendDelayLimit must be greater than sendHangGracePeriod.
        /// </summary>
        private readonly TimeSpan sendDelayLimit;

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the connection idle timeout.
        /// </summary>
        private readonly TimeSpan idleConnectionTimeout;

        /// <summary>
        /// A boolean field indicating if the aggressive timeout detection is enabled.
        /// </summary>
        private readonly bool aggressiveTimeoutDetectionEnabled;

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the timeout detection time limit.
        /// </summary>
        private readonly TimeSpan timeoutDetectionTimeLimit;

        /// <summary>
        /// A read-only integer containing the timeout detection threshold for write operations.
        /// </summary>
        private readonly int timeoutDetectionOnWriteThreshold;

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the timeout detection on write timeout.
        /// </summary>
        private readonly TimeSpan timeoutDetectionOnWriteTimeLimit;

        /// <summary>
        /// A read-only integer containing the timeout detection threshold for high frequency timeout occurences.
        /// </summary>
        private readonly int timeoutDetectionOnHighFrequencyThreshold;

        /// <summary>
        /// A read-only <see cref="TimeSpan"/> containing the timeout detection on high frequency timeout.
        /// </summary>
        private readonly TimeSpan timeoutDetectionOnHighFrequencyTimeLimit;

        /// <summary>
        /// A read-only double field containing the timeout detection threshold for CPU utilization. The timeout
        /// detection feature will be disabled if the CPU utilization is below this threshold.
        /// </summary>
        private readonly double timeoutDetectionDisableCPUThreshold;

        /// <summary>
        /// A read-only instance of <see cref="SystemUtilizationReaderBase"/>.
        /// </summary>
        private readonly SystemUtilizationReaderBase systemUtilizationReader;

        /// <summary>
        /// An integer containing the number of transit timeouts that occurred on the read path. This
        /// counter is updated atomically and resets to 0 when the connection is declared healthy.
        /// </summary>
        private int transitTimeoutOnReadCounter;

        /// <summary>
        /// An integer containing the number of transit timeouts that occurred on the write path. This
        /// counter is updated atomically and resets to 0 when the connection is declared healthy.
        /// </summary>
        private int transitTimeoutOnWriteCounter;

        /// <summary>
        /// Constructor to initialize a new instance of the <see cref="ConnectionHealthChecker"/>.
        /// </summary>
        /// <param name="sendDelayLimit">A <see cref="TimeSpan"/> containing the send delay limit.</param>
        /// <param name="receiveDelayLimit">A <see cref="TimeSpan"/> containing the receive delay limit.</param>
        /// <param name="idleConnectionTimeout">A <see cref="TimeSpan"/> containing the connection idle timeout.</param>
        public ConnectionHealthChecker(
            TimeSpan sendDelayLimit,
            TimeSpan receiveDelayLimit,
            TimeSpan idleConnectionTimeout)
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
            this.transitTimeoutOnWriteCounter = 0;
            this.transitTimeoutOnReadCounter = 0;
            this.aggressiveTimeoutDetectionEnabled = Helpers.GetEnvironmentVariable(
                    name: Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled,
                    defaultValue: ConnectionHealthChecker.AggressiveTimeoutDetectionEnabledDefaultValue);

            // The below variables are required only when transit timeout detection is enabled.
            if (this.aggressiveTimeoutDetectionEnabled)
            {
                this.systemUtilizationReader = SystemUtilizationReaderBase.SingletonInstance;
                this.timeoutDetectionTimeLimit = TimeSpan.FromSeconds(
                    Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionTimeLimit,
                        defaultValue: ConnectionHealthChecker.TimeoutDetectionTimeLimitDefaultValueInSeconds));

                this.timeoutDetectionOnWriteThreshold = Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnWriteThreshold,
                        defaultValue: ConnectionHealthChecker.TimeoutDetectionOnWriteThresholdDefaultValue);

                this.timeoutDetectionOnWriteTimeLimit = TimeSpan.FromSeconds(
                    Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnWriteTimeLimit,
                        defaultValue: ConnectionHealthChecker.TimeoutDetectionOnWriteTimeLimitDefaultValueInSeconds));

                this.timeoutDetectionOnHighFrequencyThreshold = Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnHighFrequencyThreshold,
                        defaultValue: ConnectionHealthChecker.TimeoutDetectionOnHighFrequencyThresholdDefaultValue);

                this.timeoutDetectionOnHighFrequencyTimeLimit = TimeSpan.FromSeconds(
                    Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionOnHighFrequencyTimeLimit,
                        defaultValue: ConnectionHealthChecker.TimeoutDetectionOnHighFrequencyTimeLimitDefaultValueInSeconds));

                this.timeoutDetectionDisableCPUThreshold = Helpers.GetEnvironmentVariable(
                        name: Constants.EnvironmentVariables.TimeoutDetectionDisabledOnCPUThreshold,
                        defaultValue: ConnectionHealthChecker.TimeoutDetectionDisabledOnCPUThresholdDefaultValue);
            }
        }

        /// <summary>
        /// Validates if a connection is in healthy state and returns a boolean flag to indicate the same.
        /// </summary>
        /// <param name="currentTime">An instance of <see cref="DateTime"/> containing the snapshot of current time.</param>
        /// <param name="lastSendAttempt">An instance of <see cref="DateTime"/> containing the snapshot of last send attempt time.</param>
        /// <param name="lastSend">An instance of <see cref="DateTime"/> containing the snapshot of last send time.</param>
        /// <param name="lastReceive">An instance of <see cref="DateTime"/> containing the snapshot of last receive time.</param>
        /// <param name="firstSendSinceLastReceive">An instance of <see cref="DateTime"/> containing the snapshot of last send since first receive time.</param>
        /// <param name="numberOfSendsSinceLastReceive">An integer containing the snapshot of number of sends since last receive.</param>
        /// <param name="socket">An instance of <see cref="Socket"/>.</param>
        /// <returns>A boolean flag indicating if the connection is healthy.</returns>
        public bool IsHealthy(
            DateTime currentTime,
            DateTime lastSendAttempt,
            DateTime lastSend,
            DateTime lastReceive,
            DateTime? firstSendSinceLastReceive,
            long numberOfSendsSinceLastReceive,
            Socket socket)
        {
            // Assume that the connection is healthy if data was received
            // recently.
            if (ConnectionHealthChecker.IsDataReceivedRecently(
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

            if (this.IsConnectionIdled(
                currentTime: currentTime,
                lastReceive: lastReceive))
            {
                return false;
            }

            if (this.IsTransitTimeoutsDetected(
                currentTime: currentTime,
                lastReceiveTime: lastReceive))
            {
                return false;
            }

            // See https://aka.ms/zero-byte-send.
            // Socket.Send is expensive. Keep this operation last in the chain
            return ConnectionHealthChecker.IsSocketConnectionEstablished(
                socket: socket);
        }

        /// <summary>
        /// Updates the transit timeout counters by incrementing or resetting them based on the given boolean flags.
        /// </summary>
        /// <param name="isCompleted">A boolean flag indicating if the request is completed successfully.</param>
        /// <param name="isReadReqeust">A boolean flag indicating if the current operation is read-only in nature.</param>
        internal void UpdateTransitTimeoutCounters(
            bool isCompleted,
            bool isReadReqeust)
        {
            if (isCompleted)
            {
                // Resets the transit timeout counters atomically.
                this.ResetTransitTimeoutCounters();
            }
            else
            {
                // Increments the transit timeout counters atomically based on the request type.
                if (isReadReqeust)
                {
                    this.IncrementTransitTimeoutOnReadCounter();
                }
                else
                {
                    this.IncrementTransitTimeoutOnWriteCounter();
                }
            }
        }

        /// <summary>
        /// Increments the transit timeout on write counters atomically.
        /// </summary>
        private void IncrementTransitTimeoutOnWriteCounter()
        {
            Interlocked.Increment(ref this.transitTimeoutOnWriteCounter);
        }

        /// <summary>
        /// Increments the transit timeout on read counters atomically.
        /// </summary>
        private void IncrementTransitTimeoutOnReadCounter()
        {
            Interlocked.Increment(ref this.transitTimeoutOnReadCounter);
        }

        /// <summary>
        /// Resets the transit timeout counters atomically. This method uses a interlocked exchange
        /// to achieve atomicity.
        /// </summary>
        private void ResetTransitTimeoutCounters()
        {
            Interlocked.Exchange(ref this.transitTimeoutOnReadCounter, 0);
            Interlocked.Exchange(ref this.transitTimeoutOnWriteCounter, 0);
        }

        /// <summary>
        /// Detects transit timeouts and returns a boolean flag to indicate the same.
        /// </summary>
        /// <param name="currentTime">An instance of <see cref="DateTime"/> containing the snapshot of current time.</param>
        /// <param name="lastReceiveTime">An instance of <see cref="DateTime"/> containing the snapshot of last read/ receive time.</param>
        /// <returns>A boolean flag indicating if a transit timeout was detected.</returns>
        private bool IsTransitTimeoutsDetected(
            DateTime currentTime,
            DateTime lastReceiveTime)
        {
            if (!this.aggressiveTimeoutDetectionEnabled)
            {
                return false;
            }

            this.SnapshotTransitTimeoutCounters(
                out int totalTransitTimeoutCounter,
                out int transitTimeoutOnWriteCounter);

            // Timeout detection is skipped when there are no timeouts detected on both read and write flow.
            if(totalTransitTimeoutCounter == 0)
            {
                return false;
            }

            // Read delay is the difference between the current time and last receive time.
            TimeSpan readDelay = currentTime - lastReceiveTime;

            // The channel will be closed if all requests are failed due to transit timeout within the time limit.
            // This helps to close channel faster for sparse workload. The default value for timeoutDetectionTimeLimit
            // is 60 seconds.
            if (totalTransitTimeoutCounter > 0 && readDelay >= this.timeoutDetectionTimeLimit)
            {
                DefaultTrace.TraceWarning(
                    $"Unhealthy RNTBD connection: Health check failed due to transit timeout detection time limit exceeded. " +
                    $"Last channel receive: {lastReceiveTime}. Timeout detection time limit: {this.timeoutDetectionTimeLimit}.");
                return this.IsCpuUtilizationBelowDisableTimeoutDetectionThreshold();
            }

            // Timeout detection in high frequency. The default values for the high frequency threshold and time limit are:
            // timeoutDetectionHighFrequencyThreshold = 3.
            // timeoutDetectionHighFrequencyTimeLimit = 10 seconds.
            if (totalTransitTimeoutCounter >= this.timeoutDetectionOnHighFrequencyThreshold &&
                readDelay >= this.timeoutDetectionOnHighFrequencyTimeLimit)
            {
                DefaultTrace.TraceWarning(
                    "Unhealthy RNTBD connection: Health check failed due to transit timeout high frequency threshold hit. " +
                    $"Last channel receive: {lastReceiveTime}. Timeout counts: {totalTransitTimeoutCounter}. " +
                    $"Timeout detection high frequency threshold: {this.timeoutDetectionOnHighFrequencyThreshold}. Timeout detection high frequency time limit: {this.timeoutDetectionOnHighFrequencyTimeLimit}.");
                return this.IsCpuUtilizationBelowDisableTimeoutDetectionThreshold();
            }

            // Timeout detection for write operations. The default values for the write threshold and time limit are:
            // timeoutDetectionOnWriteThreshold = 1.
            // timeoutDetectionOnWriteTimeLimit = 6 seconds.
            if (transitTimeoutOnWriteCounter >= this.timeoutDetectionOnWriteThreshold &&
                readDelay >= this.timeoutDetectionOnWriteTimeLimit)
            {
                DefaultTrace.TraceWarning(
                    "Unhealthy RNTBD connection: Health check failed due to transit timeout on write threshold hit: {0}. " +
                    $"Last channel receive: {lastReceiveTime}. Write timeout counts: {transitTimeoutOnWriteCounter}. " +
                    $"Timeout detection on write threshold: {this.timeoutDetectionOnWriteThreshold}. Timeout detection on write time limit: {this.timeoutDetectionOnWriteTimeLimit}.");
                return this.IsCpuUtilizationBelowDisableTimeoutDetectionThreshold();
            }

            return false;
        }

        /// <summary>
        /// Detects a black hole and returns a boolean flag to indicate the same.
        /// </summary>
        /// <param name="currentTime">An instance of <see cref="DateTime"/> containing the snapshot of current time.</param>
        /// <param name="lastSendAttempt">An instance of <see cref="DateTime"/> containing the snapshot of last send attempt time.</param>
        /// <param name="lastSend">An instance of <see cref="DateTime"/> containing the snapshot of last send time.</param>
        /// <param name="lastReceive">An instance of <see cref="DateTime"/> containing the snapshot of last receive time.</param>
        /// <param name="firstSendSinceLastReceive">An instance of <see cref="DateTime"/> containing the snapshot of last send since first receive time.</param>
        /// <param name="numberOfSendsSinceLastReceive">An integer containing the snapshot of number of sends since last receive.</param>
        /// <returns>A boolean flag indicating if a blackhole was detected.</returns>
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
        /// Detects if data was received recently and returns a boolean flag to indicate the same.
        /// </summary>
        /// <param name="currentTime">An instance of <see cref="DateTime"/> containing the snapshot of current time.</param>
        /// <param name="lastReceiveTime">An instance of <see cref="DateTime"/> containing the snapshot of last receive time.</param>
        /// <returns>A boolean flag indicating if data was received recently.</returns>
        private static bool IsDataReceivedRecently(
            DateTime currentTime,
            DateTime lastReceiveTime)
        {
            return currentTime - lastReceiveTime < ConnectionHealthChecker.recentReceiveWindow;
        }

        /// <summary>
        /// Detects socket connectivity and returns a boolean flag to indicate the same.
        /// </summary>
        /// <param name="socket">An instance of <see cref="Socket"/>.</param>
        /// <returns>A boolean flag indicating if a socket connectivity was established.</returns>
        private static bool IsSocketConnectionEstablished(
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

        /// <summary>
        /// Detects Idle Timeout and returns a boolean flag to indicate the same.
        /// </summary>
        /// <param name="currentTime">An instance of <see cref="DateTime"/> containing the snapshot of current time.</param>
        /// <param name="lastReceive">An instance of <see cref="DateTime"/> containing the snapshot of last receive time.</param>
        /// <returns>A boolean flag indicating if a connection idle timeout was detected.</returns>
        private bool IsConnectionIdled(
            DateTime currentTime,
            DateTime lastReceive)
        {
            // Validates if idle timeout is enabled and exceeds the idle timeout limit.
            return this.idleConnectionTimeout > TimeSpan.Zero && currentTime - lastReceive > this.idleConnectionTimeout;
        }

        /// <summary>
        /// Transit timeouts can be a normal symptom under high CPU load. When request times out due to high CPU,
        /// closing the existing the connection and re-establish a new one will not help the issue but rather make it worse.
        /// Therefore, the timeout detection will be disabled in case the cpu utilization goes beyond the defined threshold.
        /// </summary>
        /// <returns>A boolean flag indicating if the CPU utilization is below the defined threshold.</returns>
        private bool IsCpuUtilizationBelowDisableTimeoutDetectionThreshold()
        {
            return this.systemUtilizationReader.GetSystemWideCpuUsageCached(
                cacheEvictionTimeInSeconds: ConnectionHealthChecker.TimeoutDetectionCPUUsageCacheEvictionTimeInSeconds) <= this.timeoutDetectionDisableCPUThreshold;
        }

        /// <summary>
        /// Helper method to snapshot the transit timeout counters.
        /// </summary>
        /// <param name="totalTransitTimeoutCounter">An integer that will contain the snapshot of total transit timeout counter.</param>
        /// <param name="transitTimeoutOnWriteCounter">An integer that will contain the snapshot of transit timeout on write counter.</param>
        private void SnapshotTransitTimeoutCounters(
            out int totalTransitTimeoutCounter,
            out int transitTimeoutOnWriteCounter)
        {
            totalTransitTimeoutCounter = this.transitTimeoutOnWriteCounter + this.transitTimeoutOnReadCounter;
            transitTimeoutOnWriteCounter = this.transitTimeoutOnWriteCounter;
        }
    }
}
