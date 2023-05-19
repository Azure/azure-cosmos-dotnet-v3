//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Wraps the different health statuses and their corresponding last known timestamps
    /// for a transport address uri.
    /// </summary>
    internal sealed class TransportAddressHealthState
    {
        /// <summary>
        /// A read-only <see cref="DateTime"/> field containing the last unknown timestamp.
        /// </summary>
        private readonly DateTime? lastUnknownTimestamp;

        /// <summary>
        /// A read-only <see cref="DateTime"/> field containing the last unhealthy pending timestamp.
        /// </summary>
        private readonly DateTime? lastUnhealthyPendingTimestamp;

        /// <summary>
        /// A read-only <see cref="DateTime"/> field containing the last unhealthy timestamp.
        /// </summary>
        private readonly DateTime? lastUnhealthyTimestamp;

        /// <summary>
        /// The read-only health status Enum containing the transport address health status.
        /// </summary>
        private readonly HealthStatus healthStatus;

        /// <summary>
        /// The read-only health status diagnostic string containing the URI and health status information.
        /// </summary>
        private readonly string healthStatusDiagnosticString;

        /// <summary>
        /// A read-only list of health status diagnostic string.
        /// </summary>
        private readonly IReadOnlyList<string> healthStatusDiagnosticEnumerable;

        /// <summary>
        /// The constructor for initializing the <see cref="TransportAddressHealthState"/>.
        /// </summary>
        /// <param name="transportUri">An instance off <see cref="Uri"/> containing the physical uri.</param>
        /// <param name="healthStatus">An instance of <see cref="HealthStatus"/> containing
        /// the current health status of the transport address.</param>
        /// <param name="lastUnknownTimestamp">The last unknown timestamp.</param>
        /// <param name="lastUnhealthyPendingTimestamp">The last unhealthy pending timestamp.</param>
        /// <param name="lastUnhealthyTimestamp">The last unhealthy timestamp.</param>
        public TransportAddressHealthState(
            Uri transportUri,
            HealthStatus healthStatus,
            DateTime? lastUnknownTimestamp,
            DateTime? lastUnhealthyPendingTimestamp,
            DateTime? lastUnhealthyTimestamp)
        {
            if (transportUri is null)
            {
                throw new ArgumentNullException(
                    paramName: nameof(transportUri),
                    message: $"Argument {nameof(transportUri)} can not be null");
            }

            this.healthStatus = healthStatus;
            this.lastUnknownTimestamp = lastUnknownTimestamp;
            this.lastUnhealthyPendingTimestamp = lastUnhealthyPendingTimestamp;
            this.lastUnhealthyTimestamp = lastUnhealthyTimestamp;
            this.healthStatusDiagnosticString = $"{transportUri.Port}:{healthStatus}";

            List<string> healthStatusList = new ()
            {
                this.healthStatusDiagnosticString,
            };
            this.healthStatusDiagnosticEnumerable = healthStatusList.AsReadOnly();
        }

        /// <summary>
        /// Gets the current health status of the transport address uri.
        /// </summary>
        /// <returns>An instance of <see cref="HealthStatus"/> containing
        /// the current health status.</returns>
        public HealthStatus GetHealthStatus()
        {
            return this.healthStatus;
        }

        /// <summary>
        /// Gets the Transport Address Uri health status diagnostic string.
        /// </summary>
        /// <returns>A string containing the health status diagnostics information.</returns>
        public string GetHealthStatusDiagnosticString()
        {
            return this.healthStatusDiagnosticString;
        }

        /// <summary>
        /// Gets the Transport Address Uri health status diagnostics as a read-only <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> containing the health status diagnostics information.</returns>
        public IEnumerable<string> GetHealthStatusDiagnosticsAsReadOnlyEnumerable()
        {
            return this.healthStatusDiagnosticEnumerable;
        }

        /// <summary>
        /// Gets the last known timestamp of the uri health status.
        /// </summary>
        /// <param name="healthStatus">The current health status of the transport address.</param>
        /// <returns>The last known timestamp of the uri health status</returns>
        internal DateTime? GetLastKnownTimestampByHealthStatus(HealthStatus healthStatus)
        {
            return healthStatus switch
            {
                HealthStatus.Unhealthy => this.lastUnhealthyTimestamp,
                HealthStatus.UnhealthyPending => this.lastUnhealthyPendingTimestamp,
                HealthStatus.Unknown => this.lastUnknownTimestamp,
                _ => throw new ArgumentException(
                    message: $"Unsupported Health Status: {healthStatus}"),
            };
        }

        /// <summary>
        /// Enum containing the health statuses of <see cref="TransportAddressUri"/>
        /// </summary>
        public enum HealthStatus
        {
            /// <summary>
            /// Indicates a healthy status.
            /// </summary>
            Connected = 100,

            /// <summary>
            /// Indicates an unknown status.
            /// </summary>
            Unknown = 200,

            /// <summary>
            /// Indicates an unhealthy pending status.
            /// </summary>
            UnhealthyPending = 300,

            /// <summary>
            /// Indicates an unhealthy status.
            /// </summary>
            Unhealthy = 400,
        }
    }
}
