//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
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
        private DateTime? lastFailedRequestUtc = null;

        public TransportAddressUri(Uri addressUri)
        {
            if (addressUri == null)
            {
                throw new ArgumentNullException(nameof(addressUri));
            }

            this.Uri = addressUri;
            this.uriToString = addressUri.ToString();
            this.PathAndQuery = addressUri.PathAndQuery.TrimEnd(TransportSerialization.UrlTrim);
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
            if(dateTime.Value + TransportAddressUri.oneMinute > DateTime.UtcNow)
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
            this.lastFailedRequestUtc = DateTime.UtcNow;
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
    }
}
