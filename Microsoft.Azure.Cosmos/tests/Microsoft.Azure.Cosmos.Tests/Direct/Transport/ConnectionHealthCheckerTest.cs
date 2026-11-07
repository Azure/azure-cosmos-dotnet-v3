// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Test
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="ConnectionHealthChecker"/>.
    /// </summary>
    [TestClass]
    public class ConnectionHealthCheckerTest
    {
        /// <summary>
        /// Test to validate that, when the data is received recently, the connection is marked healthy.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void IsHealthy_WhenDataReceivedRecently_ShouldMarkTheConnectionHealthy()
        {
            // Arrange.
            TestUtils.SnapshotConnectionTimestamps(
                out DateTime currentTime,
                out DateTime lastSendAttempt,
                out DateTime lastSend,
                out _,
                out DateTime firstSendSinceLastReceive,
                out TimeSpan sendDelayLimit,
                out TimeSpan receiveDelayLimit,
                out TimeSpan idleConnectionTimeout);

            DateTime lastReceive = TestUtils.ParseDateTime("2023-05-12 14:15:05");
            ConnectionHealthChecker healthChecker = new(
                sendDelayLimit: sendDelayLimit,
                receiveDelayLimit: receiveDelayLimit,
                idleConnectionTimeout: idleConnectionTimeout);

            // Act.
            bool healthy = healthChecker.IsHealthy(
                currentTime: currentTime,
                lastSendAttempt: lastSendAttempt,
                lastSend: lastSend,
                lastReceive: lastReceive,
                firstSendSinceLastReceive: firstSendSinceLastReceive,
                numberOfSendsSinceLastReceive: 1,
                socket: default);

            // Assert.
            Assert.IsTrue(healthy);
        }

        /// <summary>
        /// Test to validate that, when the connection is idle for a duration that exceeds the connection idle timeout
        /// detection limit, the connection is marked unhealthy.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void IsHealthy_WhenIdleConnectionDetected_ShouldMarkTheConnectionUnHealthy()
        {
            // Arrange.
            TestUtils.SnapshotConnectionTimestamps(
                out DateTime currentTime,
                out DateTime lastSendAttempt,
                out DateTime lastSend,
                out DateTime lastReceive,
                out DateTime firstSendSinceLastReceive,
                out TimeSpan sendDelayLimit,
                out TimeSpan receiveDelayLimit,
                out TimeSpan idleConnectionTimeout);

            ConnectionHealthChecker healthChecker = new(
                sendDelayLimit: sendDelayLimit,
                receiveDelayLimit: receiveDelayLimit,
                idleConnectionTimeout: idleConnectionTimeout);

            // Act.
            bool healthy = healthChecker.IsHealthy(
                currentTime: currentTime,
                lastSendAttempt: lastSendAttempt,
                lastSend: lastSend,
                lastReceive: lastReceive,
                firstSendSinceLastReceive: firstSendSinceLastReceive,
                numberOfSendsSinceLastReceive: 1,
                socket: default);

            // Assert.
            Assert.IsFalse(healthy);
        }

        /// <summary>
        /// Test to validate that, when the transit timeout detection is enabled and eventually detected,
        /// the connection is marked unhealthy. To better understand the test, please refer to the truth table
        /// below for the data rows. The thresholds and timeouts are set to the default following values:
        ///
        /// timeoutDetectionTimeLimit = 60 seconds.
        /// timeoutDetectionOnWriteThreshold = 1.
        /// timeoutDetectionOnWriteTimeLimit = 6 seconds.
        /// timeoutDetectionHighFrequencyThreshold = 3.
        /// timeoutDetectionHighFrequencyTimeLimit = 10 seconds.
        ///
        /// |-----------------|--------------------------------|---------------------------------|----------------------------------|----------------------------------|---------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | Data Row Number | Timeout Detection Enabled Flag | Transit Timeout on Read Counter | Transit Timeout on Write Counter | CPU Utilization Percentage       | Current Time        | Last Receive Time   | Read Delay                                       | Conditions Validated                                                                                                      | Result IsHealthy |
        /// |-----------------|--------------------------------|---------------------------------|----------------------------------|----------------------------------|---------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 1               | True                           | 4                               | 0                                | 14.34 %                          | 2023-05-12 14:15:05 | 2023-05-12 14:13:00 | (current time - last receive time) = 2 minutes.  | (readDelay >= timeoutDetectionTimeLimit) && IsCpuUtilizationBelowDisableTimeoutDetectionThreshold()                       | False            |
        /// |-----------------|--------------------------------|---------------------------------|----------------------------------|----------------------------------|---------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 2               | True                           | 3                               | 0                                | 34.21 %                          | 2023-05-12 14:15:05 | 2023-05-12 14:14:54 | (current time - last receive time) = 11 seconds. | (transitTimeoutCounter >= timeoutDetectionHighFrequencyThreshold  && readDelay >= timeoutDetectionHighFrequencyTimeLimit) | False            |
        /// |-----------------|--------------------------------|---------------------------------|----------------------------------|----------------------------------|---------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 3               | True                           | 5                               | 2                                | 20.77 %                          | 2023-05-12 14:15:05 | 2023-05-12 14:14:57 | (current time - last receive time) = 8 seconds.  | (transitTimeoutWriteCounter >= timeoutDetectionOnWriteThreshold  && readDelay >= timeoutDetectionOnWriteTimeLimit)        | False            |
        /// |-----------------|--------------------------------|---------------------------------|----------------------------------|----------------------------------|---------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|
        /// | 4               | True                           | 5                               | 2                                | NaN                              | 2023-05-12 14:15:05 | 2023-05-12 14:12:55 | (current time - last receive time) = 130 seconds.| (readDelay >= timeoutDetectionTimeLimit) && IsCpuUtilizationBelowDisableTimeoutDetectionThreshold()                       | False            |
        /// |-----------------|--------------------------------|---------------------------------|----------------------------------|----------------------------------|---------------------|---------------------|--------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|------------------|.
        ///
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        [DataRow("2023-05-12 14:13:00", 4, 0, 14.34F, DisplayName = "Validate That IsHealthy Returns False When Transit Timeout is Detected And CPU Utilization Value is Within Threshold")]
        [DataRow("2023-05-12 14:14:54", 3, 0, 34.21F, DisplayName = "Validate That IsHealthy Returns False When Transit Timeout is Detected On High Frequency")]
        [DataRow("2023-05-12 14:14:57", 5, 2, 20.77F, DisplayName = "Validate That IsHealthy Returns False When Transit Timeout is Detected On Write Operations")]
        [DataRow("2023-05-12 14:12:55", 4, 0, float.NaN, DisplayName = "Validate That IsHealthy Returns False When Transit Timeout is Detected And CPU Utilization Value is NaN")]
        public void IsHealthy_WhenDifferentTransitTimeoutsDetected_ShouldMarkTheConnectionUnHealthy(
            string lastReceiveTime,
            int transitTimeoutOnReadCount,
            int transitTimeoutOnWriteCount,
            float cpuUsageInPercent)
        {
            try
            {
                SystemUtilizationReaderBase.ApplySingletonOverride(
                    readerOverride: new DummySystemUtilizationReader(
                        cpuUsage: cpuUsageInPercent));

                // Arrange.
                Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled, "true");
                TestUtils.SnapshotConnectionTimestamps(
                    out DateTime currentTime,
                    out DateTime lastSendAttempt,
                    out DateTime lastSend,
                    out _,
                    out DateTime firstSendSinceLastReceive,
                    out TimeSpan sendDelayLimit,
                    out TimeSpan receiveDelayLimit,
                    out TimeSpan idleConnectionTimeout);

                DateTime lastReceive = TestUtils.ParseDateTime(lastReceiveTime);

                ConnectionHealthChecker healthChecker = new(
                    sendDelayLimit: sendDelayLimit,
                    receiveDelayLimit: receiveDelayLimit,
                    idleConnectionTimeout: idleConnectionTimeout);

                for (int i = 0; i < transitTimeoutOnReadCount; i++)
                {
                    healthChecker.UpdateTransitTimeoutCounters(
                        isCompleted: false,
                        isReadReqeust: true);
                }

                for (int i = 0; i < transitTimeoutOnWriteCount; i++)
                {
                    healthChecker.UpdateTransitTimeoutCounters(
                        isCompleted: false,
                        isReadReqeust: false);
                }

                // Act.
                bool healthy = healthChecker.IsHealthy(
                    currentTime: currentTime,
                    lastSendAttempt: lastSendAttempt,
                    lastSend: lastSend,
                    lastReceive: lastReceive,
                    firstSendSinceLastReceive: firstSendSinceLastReceive,
                    numberOfSendsSinceLastReceive: 1,
                    socket: default);

                // Assert.
                Assert.IsFalse(healthy);
            }
            finally
            {
                SystemUtilizationReaderBase.ApplySingletonOverride(readerOverride: null);
                Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.AggressiveTimeoutDetectionEnabled, "false");
            }
        }

        /// <summary>
        /// Test to validate that when timeout detection is disabled and a socket connection is failed to establish,
        /// the connection is marked un-healthy. Note that, when transit timeout detection is disabled, the validation
        /// for transit timeout detection is skipped.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void IsHealthy_WhenTransitTimeoutDetectionDisabledAndSocketConnectionFails_ShouldMarkTheConnectionUnHealthy()
        {
            // Arrange.
            TestUtils.SnapshotConnectionTimestamps(
                out DateTime currentTime,
                out DateTime lastSendAttempt,
                out DateTime lastSend,
                out DateTime lastReceive,
                out DateTime firstSendSinceLastReceive,
                out TimeSpan sendDelayLimit,
                out TimeSpan receiveDelayLimit,
                out TimeSpan idleConnectionTimeout);

            ConnectionHealthChecker healthChecker = new(
                sendDelayLimit: sendDelayLimit,
                receiveDelayLimit: receiveDelayLimit,
                idleConnectionTimeout: idleConnectionTimeout);

            TestUtils.UpdateFieldUsingReflection(
                objectName: healthChecker,
                fieldName: "transitTimeoutOnWriteCounter",
                fieldValue: 2);

            TestUtils.UpdateFieldUsingReflection(
                objectName: healthChecker,
                fieldName: "transitTimeoutOnReadCounter",
                fieldValue: 4);

            // Act.
            bool healthy = healthChecker.IsHealthy(
                currentTime: currentTime,
                lastSendAttempt: lastSendAttempt,
                lastSend: lastSend,
                lastReceive: lastReceive,
                firstSendSinceLastReceive: firstSendSinceLastReceive,
                numberOfSendsSinceLastReceive: 1,
                socket: default);

            // Assert.
            Assert.IsFalse(healthy);
        }
    }
}