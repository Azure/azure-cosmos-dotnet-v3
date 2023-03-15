//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Wraps around the 'uri' for RNTBD requests.
    /// </summary>
    /// <remarks>
    /// RNTBD calls many heavily allocating methods for Uri Path and query etc
    /// for every request. This caches the result of that for any given URI and returns
    /// the post-processed value.
    /// This improves performance as this can be cached in the AddressSelector (which is long lived).
    /// </remarks>
    internal sealed class TransportAddressUri : IEquatable<TransportAddressUri>
    {
        /// <summary>
        /// A read-only <see cref="TimeSpan"/> indicating the idle time in minutes for address state transitions.
        /// The default value for idle time is one minute.
        /// </summary>
        private static readonly TimeSpan idleTimeInMinutes = TimeSpan.FromMinutes(1);

        /// <summary>
        /// A read-only string containing the physical uri.
        /// </summary>
        private readonly string uriToString;

        /// <summary>
        /// A <see cref="DateTime"/> field containing the failed timestamp.
        /// </summary>
        private DateTime? lastFailedRequestUtc;

        /// <summary>
        /// The health status Enum containing the transport address health status.
        /// </summary>
        private TransportAddressHealthState healthState;

        /// <summary>
        /// The constructor for initializing the <see cref="TransportAddressUri"/>.
        /// </summary>
        /// <param name="addressUri">An <see cref="Uri"/> containing the physical uri of the replica.</param>
        public TransportAddressUri(Uri addressUri)
        {
            this.Uri = addressUri ?? throw new ArgumentNullException(paramName: nameof(addressUri));
            this.serverKey = new (uri: addressUri);
            this.uriToString = addressUri.ToString();
            this.PathAndQuery = addressUri.PathAndQuery.TrimEnd(TransportSerialization.UrlTrim);
            this.healthState = new (
                transportUri: addressUri,
                healthStatus: TransportAddressHealthState.HealthStatus.Unknown,
                lastUnknownTimestamp: DateTime.UtcNow,
                lastUnhealthyPendingTimestamp: null,
                lastUnhealthyTimestamp: null);
            this.lastFailedRequestUtc = null;
        }

        /// <summary>
        /// Gets the backend physical Uri.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the current path and query as string.
        /// </summary>
        public string PathAndQuery { get; }

        /// <summary>
        /// An instance of <see cref="ServerKey"/> containing the host and port details.
        /// </summary>
        public ServerKey serverKey;

        /// <summary>
        /// Is a flag to determine if the replica the URI is pointing to is unhealthy.
        /// The unhealthy status is reset after 1 minutes to prevent a replica from
        /// being permenatly marked as unhealthy.
        /// </summary>
        public bool IsUnhealthy()
        {
            DateTime? dateTime = this.lastFailedRequestUtc;
            if (dateTime == null || !dateTime.HasValue)
            {
                return false;
            }

            // The 1 minutes give it a buffer for the multiple retries to succeed.
            // Worst case a future request will fail from stale cache and mark it unhealthy
            if (dateTime.Value + TransportAddressUri.idleTimeInMinutes > DateTime.UtcNow)
            {
                return true;
            }

            // The Uri has been marked unhealthy for over 1 minute.
            // Remove the flag.
            this.lastFailedRequestUtc = null;
            return false;
        }

        /// <summary>
        /// Sets the current health status to unhealthy and logs the unhealthy timestamp.
        /// </summary>
        public void SetUnhealthy()
        {
            TransportAddressHealthState snapshot = this.healthState;
            this.SetHealthStatus(
                previousState: snapshot,
                status: TransportAddressHealthState.HealthStatus.Unhealthy);
            this.lastFailedRequestUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the current health status to connected.
        /// </summary>
        public void SetConnected()
        {
            TransportAddressHealthState snapshot = this.healthState;
            if (snapshot.GetHealthStatus() != TransportAddressHealthState.HealthStatus.Connected)
            {
                this.SetHealthStatus(
                    previousState: snapshot,
                    status: TransportAddressHealthState.HealthStatus.Connected);
            }
        }

        /// <summary>
        /// Sets the current health status to unhealthy pending and logs the timestamp.
        /// </summary>
        public void SetRefreshedIfUnhealthy()
        {
            TransportAddressHealthState snapshot = this.healthState;
            if (snapshot.GetHealthStatus() == TransportAddressHealthState.HealthStatus.Unhealthy)
            {
                this.SetHealthStatus(
                    previousState: snapshot,
                    status: TransportAddressHealthState.HealthStatus.UnhealthyPending);
            }
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Uri.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.uriToString;
        }

        /// <inheritdoc />
        public bool Equals(TransportAddressUri other)
        {
            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Uri.Equals(other?.Uri);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj) || (obj is TransportAddressUri other && this.Equals(other));
        }

        /// <summary>
        /// Resets the health status and timestamps of the transport address uri with that of the
        /// provided health status and timestamps.
        /// </summary>
        /// <param name="status">The requested health status to be updated.</param>
        /// <param name="lastUnknownTimestamp">The requested last unknown timestamp to be updated.</param>
        /// <param name="lastUnhealthyPendingTimestamp">The requested last unhealthy pending timestamp to be updated.</param>
        /// <param name="lastUnhealthyTimestamp">The requested last unhealthy timestamp to be updated.</param>
        public void ResetHealthStatus(
            TransportAddressHealthState.HealthStatus status,
            DateTime? lastUnknownTimestamp,
            DateTime? lastUnhealthyPendingTimestamp,
            DateTime? lastUnhealthyTimestamp)
        {
            this.CreateAndUpdateCurrentHealthState(
                healthStatus: status,
                lastUnknownTimestamp: lastUnknownTimestamp,
                lastUnhealthyPendingTimestamp: lastUnhealthyPendingTimestamp,
                lastUnhealthyTimestamp: lastUnhealthyTimestamp,
                previousState: this.healthState);
        }

        /// <summary>
        /// Gets the current health state of the transport address uri.
        /// </summary>
        /// <returns>An instance of <see cref="TransportAddressHealthState"/> containing
        /// the current health state of the uri.</returns>
        public TransportAddressHealthState GetCurrentHealthState()
        {
            return this.healthState;
        }

        /// <summary>
        /// In <see cref="AddressEnumerator"/>, it could de-prioritize the TransportAddressUri in UnhealthyPending/Unhealthy health status (depending on
        /// whether replica validation is enabled). If the replica is stuck in those statuses for too long (more than a minute in reality),
        /// then in order to avoid replica usage skew, we are going to mark them into healthy category, so it's status can be re-validated
        /// by requests again.
        /// </summary>
        /// <returns>An instance of <see cref="TransportAddressHealthState.HealthStatus"/> indicating the effective health status.</returns>
        public TransportAddressHealthState.HealthStatus GetEffectiveHealthStatus()
        {
            TransportAddressHealthState snapshot = this.healthState;
            switch (snapshot.GetHealthStatus())
            {
                case TransportAddressHealthState.HealthStatus.Connected:
                case TransportAddressHealthState.HealthStatus.Unhealthy:
                    return snapshot.GetHealthStatus();

                case TransportAddressHealthState.HealthStatus.Unknown:
                    if (DateTime.UtcNow > snapshot.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown) + TransportAddressUri.idleTimeInMinutes)
                    {
                        return TransportAddressHealthState.HealthStatus.Connected;
                    }
                    return snapshot.GetHealthStatus();

                case TransportAddressHealthState.HealthStatus.UnhealthyPending:
                    if (DateTime.UtcNow > snapshot.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending) + TransportAddressUri.idleTimeInMinutes)
                    {
                        return TransportAddressHealthState.HealthStatus.Connected;
                    }
                    return snapshot.GetHealthStatus();

                default:
                    throw new ArgumentException(
                        message: $"Unknown status :{snapshot.GetHealthStatus()}");
            }
        }

        /// <summary>
        /// Returns a boolean flag indicating if a health status refresh could possibly
        /// be done for any replica that remains unhealthy for more than one minute.
        /// </summary>
        /// <returns>A boolean flag indicating if the health status could be refreshed.</returns>
        public bool ShouldRefreshHealthStatus()
        {
            TransportAddressHealthState snapshot = this.healthState;
            return snapshot.GetHealthStatus() == TransportAddressHealthState.HealthStatus.Unhealthy
                    && DateTime.UtcNow >= (snapshot.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy) + TransportAddressUri.idleTimeInMinutes);
        }

        /// <summary>
        /// Sets the transport address uri health status.
        /// </summary>
        /// <param name="previousState">The previous health state of the transport address.</param>
        /// <param name="status">The current health status of the transport address.</param>
        private void SetHealthStatus(
            TransportAddressHealthState previousState,
            TransportAddressHealthState.HealthStatus status)
        {
            switch (status)
            {
                case TransportAddressHealthState.HealthStatus.Unhealthy:

                    this.CreateAndUpdateCurrentHealthState(
                        healthStatus: TransportAddressHealthState.HealthStatus.Unhealthy,
                        lastUnknownTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                        lastUnhealthyPendingTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending),
                        lastUnhealthyTimestamp: DateTime.UtcNow,
                        previousState: previousState);
                    break;

                case TransportAddressHealthState.HealthStatus.UnhealthyPending:
                    if (previousState.GetHealthStatus() == TransportAddressHealthState.HealthStatus.Unhealthy || previousState.GetHealthStatus() == TransportAddressHealthState.HealthStatus.UnhealthyPending)
                    {
                        this.CreateAndUpdateCurrentHealthState(
                            healthStatus: TransportAddressHealthState.HealthStatus.UnhealthyPending,
                            lastUnknownTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                            lastUnhealthyPendingTimestamp: DateTime.UtcNow,
                            lastUnhealthyTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy),
                            previousState: previousState);
                    }
                    else
                    {
                        Debug.Assert(
                            condition: false,
                            message: $"Invalid state transition. Previous status: {previousState.GetHealthStatus()}, current status: {status}");
                    }
                    break;

                case TransportAddressHealthState.HealthStatus.Connected:

                    this.CreateAndUpdateCurrentHealthState(
                        healthStatus: TransportAddressHealthState.HealthStatus.Connected,
                        lastUnknownTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unknown),
                        lastUnhealthyPendingTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.UnhealthyPending),
                        lastUnhealthyTimestamp: previousState.GetLastKnownTimestampByHealthStatus(TransportAddressHealthState.HealthStatus.Unhealthy),
                        previousState: previousState);
                    break;

                case TransportAddressHealthState.HealthStatus.Unknown:
                default:
                    throw new ArgumentException(
                        message: $"Cannot set an unsupported health status: {status}");
            }
        }

        /// <summary>
        /// Creates a new <see cref="TransportAddressHealthState"/> health state and
        /// attempts to update the current state atomically with the newly created one.
        /// </summary>
        /// <param name="healthStatus">The desired health status to be updated.</param>
        /// <param name="lastUnknownTimestamp">The requested last unknown timestamp to be updated.</param>
        /// <param name="lastUnhealthyPendingTimestamp">The requested last unhealthy pending timestamp to be updated.</param>
        /// <param name="lastUnhealthyTimestamp">The requested last unhealthy timestamp to be updated.</param>
        /// <param name="previousState">The previous health state of the transport address uri.</param>
        private void CreateAndUpdateCurrentHealthState(
            TransportAddressHealthState.HealthStatus healthStatus,
            DateTime? lastUnknownTimestamp,
            DateTime? lastUnhealthyPendingTimestamp,
            DateTime? lastUnhealthyTimestamp,
            TransportAddressHealthState previousState)
        {
            TransportAddressHealthState snapshot = previousState;
            TransportAddressHealthState nextState = new (
                transportUri: this.Uri,
                healthStatus: healthStatus,
                lastUnknownTimestamp: lastUnknownTimestamp,
                lastUnhealthyPendingTimestamp: lastUnhealthyPendingTimestamp,
                lastUnhealthyTimestamp: lastUnhealthyTimestamp);

            while (true)
            {
                TransportAddressHealthState currentState = Interlocked.CompareExchange(
                    location1: ref this.healthState,
                    value: nextState,
                    comparand: snapshot);

                if (currentState == snapshot || currentState == nextState)
                {
                    break;
                }

                snapshot = currentState;
                DefaultTrace.TraceVerbose(
                    "Re-attempting to update the current health state. Previous health status: {0}, current health status: {1}",
                    previousState.GetHealthStatus(),
                    currentState.GetHealthStatus());
            }
        }
    }
}
