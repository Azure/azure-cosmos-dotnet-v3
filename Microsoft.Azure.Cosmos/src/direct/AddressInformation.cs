//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Microsoft.Azure.Documents.Client;

    internal sealed class AddressInformation : IEquatable<AddressInformation>, IComparable<AddressInformation>
    {
        private readonly Lazy<int> lazyHashCode;

        public AddressInformation(
            string physicalUri,
            bool isPublic,
            bool isPrimary,
            Protocol protocol)
        {
            this.IsPublic = isPublic;
            this.IsPrimary = isPrimary;
            this.Protocol = protocol;
            this.PhysicalUri = physicalUri;
            this.lazyHashCode = new(() =>
            {
                int hashCode = 17;
                hashCode = (hashCode * 397) ^ Protocol.GetHashCode();
                hashCode = (hashCode * 397) ^ IsPublic.GetHashCode();
                hashCode = (hashCode * 397) ^ IsPrimary.GetHashCode();
                if (this.PhysicalUri != null)
                {
                    hashCode = (hashCode * 397) ^ PhysicalUri.GetHashCode();
                }
                return hashCode;
            });
        }

        public bool IsPublic { get; }

        public bool IsPrimary { get; }

        public Protocol Protocol { get; }

        public string PhysicalUri { get; }

        public int CompareTo(AddressInformation other)
        {
            if(other == null)
            {
                return -1;
            }

            int comp = this.IsPrimary.CompareTo(other.IsPrimary);
            if(comp != 0)
            {
                // Put primary first
                return -1 * comp;
            }

            comp = this.IsPublic.CompareTo(other.IsPublic);
            if (comp != 0)
            {
                return comp;
            }

            comp = this.Protocol.CompareTo(other.Protocol);
            if (comp != 0)
            {
                return comp;
            }

            return string.Compare(this.PhysicalUri, other.PhysicalUri, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(AddressInformation other)
        {
            return this.CompareTo(other) == 0;
        }

        public override int GetHashCode()
        {
            return this.lazyHashCode.Value;
        }
    }
}
