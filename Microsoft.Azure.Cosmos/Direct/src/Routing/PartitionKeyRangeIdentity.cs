//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;

    internal sealed class PartitionKeyRangeIdentity : IEquatable<PartitionKeyRangeIdentity>
    {
        public PartitionKeyRangeIdentity(string collectionRid, string partitionKeyRangeId)
        {
            if (collectionRid == null)
            {
                throw new ArgumentNullException("collectionRid");
            }

            if (partitionKeyRangeId == null)
            {
                throw new ArgumentNullException("partitionKeyRangeId");
            }

            this.CollectionRid = collectionRid;
            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        /// <summary>
        /// This should only be used for user provided partitionKeyRangeId, because in this case
        /// he knows what he is doing. If collection was deleted/created with same name - it is his responsibility.
        /// 
        /// If our code infers partitionKeyRangeId automatically and uses collection information from collection cache,
        /// we need to ensure that request will reach correct collection. In this case constructor which takes collectionRid MUST
        /// be used.
        /// </summary>
        public PartitionKeyRangeIdentity(string partitionKeyRangeId)
        {
            if (partitionKeyRangeId == null)
            {
                throw new ArgumentNullException("partitionKeyRangeId");
            }

            this.PartitionKeyRangeId = partitionKeyRangeId;
        }

        public static PartitionKeyRangeIdentity FromHeader(string header)
        {
            // this gets called a lot, so avoid using string.Split() and
            // the like

            // header is either
            //  - partitionKeyRangeId (no commas)
            // or
            //  - collectionId,partitionKeyRangeId (one comma)

            int commaIndex = header.IndexOf(',');
            if (commaIndex == -1)
            {
                // reuse the header string
                return new PartitionKeyRangeIdentity(header);
            }

            int secondCommaIndex = header.IndexOf(',', commaIndex + 1);
            if (secondCommaIndex != -1)
            {
                throw new BadRequestException(RMResources.InvalidPartitionKeyRangeIdHeader);
            }

            string collectionRid = header.Substring(0, commaIndex);
            string partionKeyRangeId = header.Substring(commaIndex + 1);

            return new PartitionKeyRangeIdentity(collectionRid, partionKeyRangeId);
        }

        public string ToHeader()
        {
            if (this.CollectionRid != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1}",
                    this.CollectionRid,
                    this.PartitionKeyRangeId);
            }

            return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}",
                    this.PartitionKeyRangeId);
        }

        public string CollectionRid
        {
            get;
            private set;
        }

        public string PartitionKeyRangeId
        {
            get;
            private set;
        }

        public bool Equals(PartitionKeyRangeIdentity other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return StringComparer.Ordinal.Equals(this.CollectionRid, other.CollectionRid) && StringComparer.Ordinal.Equals(this.PartitionKeyRangeId, other.PartitionKeyRangeId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            return obj is PartitionKeyRangeIdentity && Equals((PartitionKeyRangeIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.CollectionRid != null ? this.CollectionRid.GetHashCode() : 0) * 397) ^ (this.PartitionKeyRangeId != null ? this.PartitionKeyRangeId.GetHashCode() : 0);
            }
        }
    }
}
