//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Coordinate Reference System which is identified by link in the Azure Cosmos DB service. 
    /// </summary>
    [DataContract]
    public sealed class LinkedCrs : Crs, IEquatable<LinkedCrs>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LinkedCrs"/> class in the Azure Cosmos DB service. 
        /// </summary>
        /// <param name="href">
        /// Link which identifies the Coordinate Reference System.
        /// </param>
        /// <param name="hrefType">
        /// Optional string which hints at the format used to represent CRS parameters at the provided <paramref name="href"/>.
        /// </param>
        internal LinkedCrs(string href, string hrefType = null)
            : base(CrsType.Linked)
        {
            if (href == null)
            {
                throw new ArgumentNullException(nameof(href));
            }

            this.Href = href;
            this.HrefType = hrefType;
        }

        /// <summary>
        /// Gets the link which identifies the Coordinate Reference System in the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// Link which identifies the Coordinate Reference System.
        /// </value>
        [DataMember(Name = "href")]
        public string Href { get; private set; }

        /// <summary>
        /// Gets optional string which hints at the format used to represent CRS parameters at the provided <see cref="Href"/> in the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// Optional string which hints at the format used to represent CRS parameters at the provided <see cref="Href"/>.
        /// </value>
        [DataMember(Name = "hrefType")]
        public string HrefType { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="LinkedCrs"/> is equal to the current <see cref="LinkedCrs"/> in the Azure Cosmos DB service. 
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as LinkedCrs);
        }

        /// <summary>
        /// Serves as a hash function for <see cref="LinkedCrs"/> in the Azure Cosmos DB service. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="LinkedCrs"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.Href != null ? this.Href.GetHashCode() : 0) * 397) ^ (this.HrefType != null ? this.HrefType.GetHashCode() : 0);
            }
        }

        /// <summary>
        /// Determines if this <see cref="LinkedCrs"/> is equal to <paramref name="other" /> in the Azure Cosmos DB service. 
        /// </summary>
        /// <param name="other"><see cref="LinkedCrs"/> to compare to this <see cref="LinkedCrs"/>.</param>
        /// <returns><c>true</c> if CRSs are equal. <c>false</c> otherwise.</returns>
        public bool Equals(LinkedCrs other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Href, other.Href) && string.Equals(this.HrefType, other.HrefType);
        }
    }
}
