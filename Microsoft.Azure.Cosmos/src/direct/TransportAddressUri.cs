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
    internal sealed class TransportAddressUri : IEquatable<TransportAddressUri>, IDisposable
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
        /// A read-only <see cref="ReaderWriterLockSlim"/> to implement the locking mechanism.
        /// </summary>
        private readonly ReaderWriterLockSlim healthStatusLock;

        /// <summary>
        /// A <see cref="DateTime"/> field containing the failed timestamp.
        /// </summary>
        private DateTime? lastFailedRequestUtc;

        /// <summary>
        /// A read-only <see cref="DateTime"/> field containing the last unknown timestamp.
        /// </summary>
        private DateTime? lastUnknownTimestamp;

        /// <summary>
        /// A <see cref="DateTime"/> field containing the last unhealthy pending timestamp.
        /// </summary>
        private DateTime? lastUnhealthyPendingTimestamp;

        /// <summary>
        /// A <see cref="DateTime"/> field containing the last unhealthy timestamp.
        /// </summary>
        private DateTime? lastUnhealthyTimestamp;

        /// <summary>
        /// The health status Enum containing the transport address health status.
        /// </summary>
        private HealthStatus healthStatus;

        /// <summary>
        /// The health status diagnostic string containing the URI and health status information.
        /// </summary>
        private string healthStatusDiagnosticString;

        /// <summary>
        /// A booolean flag indicating if the current instance of RntbdOpenConnectionHandler
        /// has been disposed.
        /// </summary>
        private bool disposed;

        /// <summary>
        /// The constructor for initializing the <see cref="TransportAddressUri"/>.
        /// </summary>
        /// <param name="addressUri">An <see cref="Uri"/> containing the physical uri of the replica.</param>
        public TransportAddressUri(Uri addressUri)
        {
            if (addressUri == null)
            {
                throw new ArgumentNullException(nameof(addressUri));
            }

            this.disposed = false;
            this.Uri = addressUri;
            this.uriToString = addressUri.ToString();
            this.PathAndQuery = addressUri.PathAndQuery.TrimEnd(TransportSerialization.UrlTrim);
            this.healthStatus = HealthStatus.Unknown;
            this.lastUnknownTimestamp = DateTime.UtcNow;
            this.lastUnhealthyPendingTimestamp = null;
            this.lastUnhealthyTimestamp = null;
            this.lastFailedRequestUtc = null;
            this.healthStatusLock = new(LockRecursionPolicy.NoRecursion);
            this.UpdateHealthStatusDiagnosticString();
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
        /// Gets the last known timestamp of the uri health status.
        /// </summary>
        /// <param name="healthStatus">The current health status of the transport address.</param>
        /// <returns>The last known timestamp of the uri health status</returns>
        public DateTime? GetLastKnownTimestampByHealthStatus(HealthStatus healthStatus)
        {
            this.healthStatusLock.EnterReadLock();
            try
            {
                return healthStatus switch
                {
                    HealthStatus.Unhealthy => this.lastUnhealthyTimestamp,
                    HealthStatus.UnhealthyPending => this.lastUnhealthyPendingTimestamp,
                    HealthStatus.Unknown => this.lastUnknownTimestamp,
                    _ => throw new ArgumentException($"Unsupported Health Status: {healthStatus}"),
                };
            }
            finally
            {
                this.healthStatusLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Sets the current health status to unhealthy and logs the unhealthy timestamp.
        /// </summary>
        public void SetUnhealthy()
        {
            this.SetHealthStatus(
                status: HealthStatus.Unhealthy);
            this.lastFailedRequestUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the current health status to connected.
        /// </summary>
        public void SetConnected()
        {
            this.healthStatusLock.EnterUpgradeableReadLock();
            try
            {
                if (this.healthStatus != HealthStatus.Connected)
                {
                    this.SetHealthStatus(
                        status: HealthStatus.Connected);
                }
            }
            finally
            {
                this.healthStatusLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Sets the current health status to unhealthy pending and logs the timestamp.
        /// </summary>
        public void SetRefreshedIfUnhealthy()
        {
            if (this.healthStatus == HealthStatus.Unhealthy)
            {
                this.SetHealthStatus(
                    status: HealthStatus.UnhealthyPending);
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
            HealthStatus status,
            DateTime? lastUnknownTimestamp,
            DateTime? lastUnhealthyPendingTimestamp,
            DateTime? lastUnhealthyTimestamp)
        {
            this.healthStatusLock.EnterWriteLock();
            try
            {
                this.healthStatus = status;
                this.lastUnknownTimestamp = lastUnknownTimestamp;
                this.lastUnhealthyPendingTimestamp = lastUnhealthyPendingTimestamp;
                this.lastUnhealthyTimestamp = lastUnhealthyTimestamp;
                this.UpdateHealthStatusDiagnosticString();
            }
            finally
            {
                this.healthStatusLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets the current health status of the transport address uri.
        /// </summary>
        /// <returns>An instance of <see cref="HealthStatus"/> containing
        /// the current health status.</returns>
        public HealthStatus GetHealthStatus()
        {
            this.healthStatusLock.EnterReadLock();
            try
            {
                return this.healthStatus;
            }
            finally
            {
                this.healthStatusLock.ExitReadLock();
            }
        }

        /// <summary>
        /// In <see cref="AddressEnumerator"/>, it could de-prioritize the TransportAddressUri in UnhealthyPending/Unhealthy health status (depending on
        /// whether replica validation is enabled). If the replica is stuck in those statuses for too long (more than a minute in reality),
        /// then in order to avoid replica usage skew, we are going to mark them into healthy category, so it's status can be re-validated
        /// by requests again.
        /// </summary>
        /// <returns>An instance of <see cref="HealthStatus"/> indicating the effective health status.</returns>
        public HealthStatus GetEffectiveHealthStatus()
        {
            this.healthStatusLock.EnterReadLock();
            try
            {
                HealthStatus currentHealthStatus = this.healthStatus;
                switch (currentHealthStatus)
                {
                    case HealthStatus.Connected:
                    case HealthStatus.Unhealthy:
                        return currentHealthStatus;

                    case HealthStatus.Unknown:
                        if (DateTime.UtcNow > this.lastUnknownTimestamp + TransportAddressUri.idleTimeInMinutes)
                        {
                            return HealthStatus.Connected;
                        }
                        return currentHealthStatus;

                    case HealthStatus.UnhealthyPending:
                        if (DateTime.UtcNow > this.lastUnhealthyPendingTimestamp + TransportAddressUri.idleTimeInMinutes)
                        {
                            return HealthStatus.Connected;
                        }
                        return currentHealthStatus;

                    default:
                        throw new ArgumentException($"Unknown status :{currentHealthStatus}");
                }
            }
            finally
            {
                this.healthStatusLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns a boolean flag indicating if a health status refresh could possibly
        /// be done for any replica that remains unhealthy for more than one minute.
        /// </summary>
        /// <returns>A boolean flag indicating if the health status could be refreshed.</returns>
        public bool ShouldRefreshHealthStatus()
        {
            return this.healthStatus == HealthStatus.Unhealthy
                    && DateTime.UtcNow >= (this.lastUnhealthyTimestamp + TransportAddressUri.idleTimeInMinutes);
        }

        /// <summary>
        /// Gets a disgnostic string containing the current uri and it's corresponding health status.
        /// </summary>
        /// <returns>A string containing the current health status.</returns>
        public string GetHealthStatusDiagnosticString()
        {
            return this.healthStatusDiagnosticString;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.healthStatusLock.Dispose();
                this.disposed = true;
            }
            else
            {
                DefaultTrace.TraceWarning("Failed to dispose the instance of: {0}, because it is already disposed. '{1}'",
                    nameof(TransportAddressUri),
                    Trace.CorrelationManager.ActivityId);
            }
        }

        /// <summary>
        /// Sets the transport address uri health status.
        /// </summary>
        /// <param name="status">The current health status of the transport address.</param>
        private void SetHealthStatus(
            HealthStatus status)
        {
            this.healthStatusLock.EnterWriteLock();
            try
            {
                HealthStatus previousStatus = this.healthStatus;
                switch (status)
                {
                    case HealthStatus.Unhealthy:
                        this.lastUnhealthyTimestamp = DateTime.UtcNow;
                        this.healthStatus = status;
                        break;

                    case HealthStatus.UnhealthyPending:
                        if (previousStatus == HealthStatus.Unhealthy || previousStatus == HealthStatus.UnhealthyPending)
                        {
                            this.lastUnhealthyPendingTimestamp = DateTime.UtcNow;
                            this.healthStatus = status;
                        }
                        else
                        {
                            Debug.Assert(
                                condition: false,
                                message: $"Invalid state transition. Previous status: {previousStatus}, current status: {status}");
                        }
                        break;

                    case HealthStatus.Connected:
                        this.healthStatus = status;
                        break;

                    case HealthStatus.Unknown:
                    default:
                        throw new ArgumentException($"Cannot set an unsupported health status: {status}");
                }
            }
            finally
            {
                this.UpdateHealthStatusDiagnosticString();
                this.healthStatusLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the Transport Address Uri health status diagnostic string
        /// with the current health status information.
        /// </summary>
        private void UpdateHealthStatusDiagnosticString() => this.healthStatusDiagnosticString = $"{this.Uri}:{this.healthStatus}";

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
