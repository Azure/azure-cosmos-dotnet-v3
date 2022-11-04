//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Security.Cryptography;
    using System.Threading;
    using Antlr4.Runtime.Sharpen;
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
        private static readonly TimeSpan oneMinute = TimeSpan.FromMinutes(1);
        private readonly string uriToString;
        private readonly DateTime? lastUnknownTimestamp = null;
        private DateTime? lastFailedRequestUtc = null;
        private DateTime? lastUnhealthyPendingTimestamp = null;
        private DateTime? lastUnhealthyTimestamp = null;
        private HealthStatus healthStatus;
        private readonly ReaderWriterLockSlim healthStatusLock = new (LockRecursionPolicy.NoRecursion);

        /// <summary>
        /// test
        /// </summary>
        /// <param name="addressUri"></param>
        public TransportAddressUri(Uri addressUri)
        {
            this.Uri = addressUri ?? throw new ArgumentNullException(nameof(addressUri));
            this.uriToString = addressUri.ToString();
            this.PathAndQuery = addressUri.PathAndQuery.TrimEnd(TransportSerialization.UrlTrim);
            this.healthStatus = HealthStatus.Unknown;
            this.lastUnknownTimestamp = DateTime.UtcNow;
            this.lastUnhealthyPendingTimestamp = null;
            this.lastUnhealthyTimestamp = null;
        }

        public Uri Uri { get; }

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
            if (dateTime.Value + TransportAddressUri.oneMinute > DateTime.UtcNow)
            {
                return true;
            }

            // The Uri has been marked unhealthy for over 1 minute.
            // Remove the flag.
            this.lastFailedRequestUtc = null;
            return false;
        }

        public void SetUnhealthy()
        {
            this.SetHealthStatus(HealthStatus.Unhealthy);
            this.lastFailedRequestUtc = DateTime.UtcNow;
        }

        public void SetConnected()
        {
            this.SetHealthStatus(HealthStatus.Connected);
        }

        public void SetRefreshed()
        {
            if (this.healthStatus == HealthStatus.Unhealthy)
            {
                this.SetHealthStatus(HealthStatus.UnhealthyPending);
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
        /// Sets the health status.
        /// </summary>
        /// <param name="status"></param>
        public void SetHealthStatus(HealthStatus status)
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
                        break;

                    case HealthStatus.Connected:
                        if (previousStatus != HealthStatus.Unhealthy
                            || (previousStatus == HealthStatus.Unhealthy &&
                                DateTime.UtcNow > this.lastUnhealthyTimestamp + TransportAddressUri.oneMinute))
                        {
                            this.healthStatus = status;
                        }
                        break;

                    case HealthStatus.Unknown:
                        // there is no reason we are going to reach here
                        throw new ArgumentException("It is impossible to set to unknown status");

                    default:
                        throw new ArgumentException("Unsupported health status: " + status);
                }
            }
            finally
            {
                this.healthStatusLock.ExitWriteLock();
            }

        }

        /// <summary>
        /// blabla
        /// </summary>
        /// <returns>An instance of <see cref="HealthStatus"/>.</returns>
        public HealthStatus GetHealthStatus()
        {
            return this.healthStatus;
        }

        /// <summary>
        ///  In AddressEnumerator, it could de-prioritize uri in unhealthyPending/unhealthy health status (depending on whether replica validation is enabled)
        /// If the replica stuck in those statuses for too long, in order to avoid replica usage skew,
        /// we are going to rolling them into healthy category, so it is status can be validated by requests again
        /// </summary>
        /// <returns>the HealthStatus.</returns>
        public HealthStatus GetEffectiveHealthStatus()
        {
            HealthStatus snapshot = this.healthStatus;
            switch (snapshot)
            {
                case HealthStatus.Connected:
                case HealthStatus.Unhealthy:
                    return snapshot;

                case HealthStatus.Unknown:
                    if (DateTime.UtcNow > this.lastUnknownTimestamp + TransportAddressUri.oneMinute)
                    {
                        return HealthStatus.Connected;
                    }
                    return snapshot;

                case HealthStatus.UnhealthyPending:
                    if (DateTime.UtcNow > this.lastUnhealthyPendingTimestamp + TransportAddressUri.oneMinute)
                    {
                        return HealthStatus.Connected;
                    }
                    return snapshot;

                default:
                    throw new ArgumentException("Unknown status " + snapshot);
            }
        }

        public bool ShouldRefreshHealthStatus()
        {
            return this.healthStatus == HealthStatus.Unhealthy
                    && DateTime.UtcNow >= (this.lastUnhealthyTimestamp + TransportAddressUri.oneMinute);
        }

        public String GetHealthStatusDiagnosticString()
        {
            return this.Uri.Port + ":" + this.healthStatus.ToString();
        }

        /// <summary>
        /// blabla
        /// </summary>
        public enum HealthStatus
        {
            /// <summary>
            /// blabla
            /// </summary>
            Connected = 100,

            /// <summary>
            /// blabla
            /// </summary>
            Unknown = 200,

            /// <summary>
            /// blabla
            /// </summary>
            UnhealthyPending = 300,

            /// <summary>
            /// blabla
            /// </summary>
            Unhealthy = 400,
        }
    }
}
