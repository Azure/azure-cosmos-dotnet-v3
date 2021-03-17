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
        private readonly string uriToString;

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
