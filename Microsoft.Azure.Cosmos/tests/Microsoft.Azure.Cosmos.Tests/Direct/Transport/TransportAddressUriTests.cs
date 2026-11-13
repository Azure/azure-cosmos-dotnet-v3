//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="TransportAddressUri"/>.
    /// </summary>
    [TestClass]
    public sealed class TransportAddressUriTests
    {
        /// <summary>
        /// Test to validate that when the transport address uri constructor initializes, the server key
        /// should reflect the correct host and port.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void ServerKey_WhenConstructorInitializes_ShouldRecordHostAndPort()
        {
            string host = "test.azure.com", port = "15211";
            Uri physicalUri = new($"rntbd://{host}:{port}/partitions/2s");
            TransportAddressUri transportAddressUri = new(physicalUri);

            Assert.IsNotNull(transportAddressUri.ReplicaServerKey);
            Assert.AreEqual(
                expected: host,
                actual: transportAddressUri.ReplicaServerKey.Server);

            Assert.AreEqual(
                expected: port,
                actual: transportAddressUri.ReplicaServerKey.Port.ToString());

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.Unknown} |" +
                $" lkt: {transportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown)})",
                currentHealthState: transportAddressUri.GetCurrentHealthState());
        }

        /// <summary>
        /// Test to validate that setting the appropiate statuses in transport address uri reflects the
        /// statuses changes correctly.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void GetHealthStatus_WhenSetsDifferentStatus_ShouldReflectStatusChanges()
        {
            int port = 15211;
            Uri physicalUri = new($"rntbd://test.azure.com:{port}/partitions/2s");
            TransportAddressUri transportAddressUri = new(physicalUri);

            Assert.IsNotNull(transportAddressUri.ReplicaServerKey);
            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Unknown, transportAddressUri.GetCurrentHealthState().GetHealthStatus());

            transportAddressUri.SetUnhealthy();
            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Unhealthy, transportAddressUri.GetCurrentHealthState().GetHealthStatus());

            transportAddressUri.SetRefreshedIfUnhealthy();
            Assert.AreEqual(TransportAddressHealthState.HealthStatus.UnhealthyPending, transportAddressUri.GetCurrentHealthState().GetHealthStatus());

            transportAddressUri.SetConnected();
            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Connected, transportAddressUri.GetCurrentHealthState().GetHealthStatus());

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.Connected} |" +
                $" lkt: {transportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Connected)})",
                currentHealthState: transportAddressUri.GetCurrentHealthState());
        }

        /// <summary>
        /// Test to validate that when the transport address uri status remains unknown or unhealthy-pending for
        /// more than one minute then the uri should be considered as healthy for the address enumerator for selection.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void GetEffectiveHealthStatus_WhenOneMinutePassesAfterUnknownOrUnhealthyPendingStatus_ShouldMarkUriAsHealthy()
        {
            // Arrange.
            int port = 15211;
            Uri physicalUri = new($"rntbd://test.azure.com:{port}/partitions/2s");
            TransportAddressUri transportAddressUri = new(physicalUri);

            // Act and Assert.
            Assert.IsNotNull(transportAddressUri.ReplicaServerKey);
            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Unknown, transportAddressUri.GetEffectiveHealthStatus());

            // Adds delay for two minute.
            this.AddMinuteToDateTimeFieldUsingReflection(
                            transportAddressHealthState: transportAddressUri.GetCurrentHealthState(),
                            fieldName: "lastUnknownTimestamp",
                            delayInMinutes: -2);

            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Connected, transportAddressUri.GetEffectiveHealthStatus());

            transportAddressUri.SetUnhealthy();
            transportAddressUri.SetRefreshedIfUnhealthy();

            Assert.AreEqual(TransportAddressHealthState.HealthStatus.UnhealthyPending, transportAddressUri.GetEffectiveHealthStatus());

            // Adds delay for two minute.
            this.AddMinuteToDateTimeFieldUsingReflection(
                            transportAddressHealthState: transportAddressUri.GetCurrentHealthState(),
                            fieldName: "lastUnhealthyPendingTimestamp",
                            delayInMinutes: -2);

            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Connected, transportAddressUri.GetEffectiveHealthStatus());

            // Removes the delay of two minute to match the last known time.
            this.AddMinuteToDateTimeFieldUsingReflection(
                transportAddressHealthState: transportAddressUri.GetCurrentHealthState(),
                fieldName: "lastUnhealthyPendingTimestamp",
                delayInMinutes: 2);

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.UnhealthyPending} |" +
                $" lkt: {transportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending)})",
                currentHealthState: transportAddressUri.GetCurrentHealthState());
        }

        /// <summary>
        /// Test to validate that when the transport address uri status remain unhealthy for more than one minute
        /// the uri should be marked for refresh.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void GetEffectiveHealthStatus_WhenOneMinutePassesAfterUnhealthyStatus_ShouldMarkTheUriForRefresh()
        {
            // Arrange.
            int port = 15211;
            Uri physicalUri = new($"rntbd://test.azure.com:{port}/partitions/2s");
            TransportAddressUri transportAddressUri = new(physicalUri);

            // Act and Assert.
            Assert.IsNotNull(transportAddressUri.ReplicaServerKey);
            Assert.AreEqual(TransportAddressHealthState.HealthStatus.Unknown, transportAddressUri.GetEffectiveHealthStatus());

            transportAddressUri.SetUnhealthy();
            Assert.IsFalse(transportAddressUri.ShouldRefreshHealthStatus());

            // Adds delay for one minute.
            this.AddMinuteToDateTimeFieldUsingReflection(
                transportAddressHealthState: transportAddressUri.GetCurrentHealthState(),
                fieldName: "lastUnhealthyTimestamp",
                delayInMinutes: -1);

            Assert.IsTrue(transportAddressUri.ShouldRefreshHealthStatus());

            // Removes the delay of one minute to match the last known time.
            this.AddMinuteToDateTimeFieldUsingReflection(
                transportAddressHealthState: transportAddressUri.GetCurrentHealthState(),
                fieldName: "lastUnhealthyTimestamp",
                delayInMinutes: 1);

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.Unhealthy} |" +
                $" lkt: {transportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy)})",
                currentHealthState: transportAddressUri.GetCurrentHealthState());
        }

        /// <summary>
        /// Test to validate that when a transport uri status reset is requested with valid timestamps, the transport uri
        /// should update it's status as well as the timestamps to reflect the requested status. This has been done to
        /// reflect the cached status for a same transport uri that is being returned from the gateway service during address refresh.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void ResetSetHealthStatus_WithValidLastKnownTimestamps_ShouldReflectTheStatusChangesAndUpdatedTimestamps()
        {
            // Arrange.
            int port = 15211;
            Uri oldPhysicalUri = new($"rntbd://test.azure.com:{port}/partitions/2s");
            TransportAddressUri oldTransportAddressUri = new(oldPhysicalUri);
            oldTransportAddressUri.SetUnhealthy();
            oldTransportAddressUri.SetRefreshedIfUnhealthy();

            // Act and Assert.
            Assert.IsNotNull(oldTransportAddressUri.ReplicaServerKey);
            Assert.AreEqual(
                expected: TransportAddressHealthState.HealthStatus.UnhealthyPending,
                actual: oldTransportAddressUri.GetCurrentHealthState().GetHealthStatus());

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.UnhealthyPending} |" +
                $" lkt: {oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending)})",
                currentHealthState: oldTransportAddressUri.GetCurrentHealthState());

            Uri newhysicalUri = new($"rntbd://test.azure.com:{port}/partitions/3s");
            TransportAddressUri newTransportAddressUri = new(newhysicalUri);

            Assert.AreEqual(
                expected: TransportAddressHealthState.HealthStatus.Unknown,
                actual: newTransportAddressUri.GetCurrentHealthState().GetHealthStatus());

            newTransportAddressUri.ResetHealthStatus(
                status: oldTransportAddressUri.GetCurrentHealthState().GetHealthStatus(),
                lastUnknownTimestamp: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                lastUnhealthyPendingTimestamp: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending),
                lastUnhealthyTimestamp: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy));

            Assert.AreEqual(
                expected: TransportAddressHealthState.HealthStatus.UnhealthyPending,
                actual: newTransportAddressUri.GetCurrentHealthState().GetHealthStatus());

            Assert.AreEqual(
                expected: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                actual: newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown));

            Assert.AreEqual(
                expected: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending),
                actual: newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending));

            Assert.AreEqual(
                expected: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy),
                actual: newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy));

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.UnhealthyPending} |" +
                $" lkt: {newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending)})",
                currentHealthState: newTransportAddressUri.GetCurrentHealthState());
        }

        /// <summary>
        /// Test to validate that when a transport uri status reset is requested with nullable timestamps, the transport uri
        /// should update it's status as well as the timestamps to reflect the requested status. This has been done to
        /// reflect the cached status for a same transport uri that is being returned from the gateway service during address refresh.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public void ResetSetHealthStatus_WithNullLastKnownTimestamps_ShouldReflectTheStatusChangesAndUpdatedTimestamps()
        {
            // Arrange.
            int port = 15211;
            Uri oldPhysicalUri = new($"rntbd://test.azure.com:{port}/partitions/2s");
            TransportAddressUri oldTransportAddressUri = new(oldPhysicalUri);

            // Act and Assert.
            Assert.AreEqual(
                expected: TransportAddressHealthState.HealthStatus.Unknown,
                actual: oldTransportAddressUri.GetCurrentHealthState().GetHealthStatus());

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.Unknown} |" +
                $" lkt: {oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown)})",
                currentHealthState: oldTransportAddressUri.GetCurrentHealthState());

            Uri newhysicalUri = new($"rntbd://test.azure.com:{port}/partitions/3s");
            TransportAddressUri newTransportAddressUri = new(newhysicalUri);

            Assert.IsNotNull(newTransportAddressUri.ReplicaServerKey);
            Assert.AreEqual(
                expected: TransportAddressHealthState.HealthStatus.Unknown,
                actual: newTransportAddressUri.GetCurrentHealthState().GetHealthStatus());

            newTransportAddressUri.ResetHealthStatus(
                status: oldTransportAddressUri.GetCurrentHealthState().GetHealthStatus(),
                lastUnknownTimestamp: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                lastUnhealthyPendingTimestamp: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending),
                lastUnhealthyTimestamp: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy));

            Assert.IsNull(value: newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending));
            Assert.IsNull(value: newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy));

            Assert.AreEqual(
                expected: TransportAddressHealthState.HealthStatus.Unknown,
                actual: newTransportAddressUri.GetCurrentHealthState().GetHealthStatus());

            Assert.AreEqual(
                expected: oldTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                actual: newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown));

            TransportAddressUriTests.AssertHealthStatusDiagnosticStringContents(
                expectedHealthStatusString: $"(port: {port} |" +
                $" status: {TransportAddressHealthState.HealthStatus.Unknown} |" +
                $" lkt: {newTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown)})",
                currentHealthState: newTransportAddressUri.GetCurrentHealthState());
        }

        /// <summary>
        /// This helper method uses reflection to set the private and read only fields
        /// to the disered values to help the test cases mimic the expected behavior.
        /// </summary>
        /// <param name="transportAddressHealthState">An instance of <see cref="TransportAddressHealthState"/>.</param>
        /// <param name="fieldName">A string containing the internal field name.</param>
        /// <param name="delayInMinutes">An integer to add or substract the desired delay in minutes.</param>
        private void AddMinuteToDateTimeFieldUsingReflection(
            TransportAddressHealthState transportAddressHealthState,
            string fieldName,
            int delayInMinutes)
        {
            FieldInfo fieldInfo = transportAddressHealthState
                .GetType()
                .GetField(
                    name: fieldName,
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            DateTime? fieldValue = (DateTime?)fieldInfo
                .GetValue(
                    obj: transportAddressHealthState);

            fieldInfo
                .SetValue(
                    obj: transportAddressHealthState,
                    value: ((DateTime)fieldValue).AddMinutes(delayInMinutes));
        }

        /// <summary>
        /// Helper method to assert the health status diagnostic string contents with that
        /// of the expected ones.
        /// </summary>
        /// <param name="expectedHealthStatusString">The expected health status diagnostics string.</param>
        /// <param name="currentHealthState">The current <see cref="TransportAddressHealthState"/> object.</param>
        private static void AssertHealthStatusDiagnosticStringContents(
            string expectedHealthStatusString,
            TransportAddressHealthState currentHealthState)
        {
            Assert.IsNotNull(
                value: currentHealthState.GetHealthStatusDiagnosticsAsReadOnlyEnumerable());

            Assert.AreEqual(
                expected: expectedHealthStatusString,
                actual: currentHealthState.GetHealthStatusDiagnosticString());

            Assert.AreEqual(
                expected: expectedHealthStatusString,
                actual: ((IReadOnlyList<string>)currentHealthState.GetHealthStatusDiagnosticsAsReadOnlyEnumerable())[0]);
        }
    }
}