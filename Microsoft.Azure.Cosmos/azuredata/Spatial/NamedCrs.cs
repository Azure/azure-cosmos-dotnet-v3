//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Runtime.Serialization;
    
    /// <summary>
    /// Coordinate Reference System which is identified by name in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal sealed class NamedCrs : Crs, IEquatable<NamedCrs>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NamedCrs" /> class in the Azure Cosmos DB service. 
        /// </summary>
        /// <param name="name">
        /// Name identifying a coordinate reference system.
        /// </param>
        internal NamedCrs(string name)
            : base(CrsType.Named)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            this.Name = name;
        }

        /// <summary>
        /// Gets a name identifying a coordinate reference system in the Azure Cosmos DB service. For example "urn:ogc:def:crs:OGC:1.3:CRS84".
        /// </summary>
        /// <value>
        /// Name identifying a coordinate reference system. For example "urn:ogc:def:crs:OGC:1.3:CRS84".
        /// </value>
        [DataMember(Name = "name")]
        public string Name { get; private set; }

        /// <summary>
        /// Determines whether the specified <see cref="NamedCrs" /> is equal to the current <see cref="NamedCrs" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as NamedCrs);
        }

        /// <summary>
        /// Serves as a hash function for the name identifying a coordinate reference system in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="NamedCrs" />.
        /// </returns>
        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        /// <summary>
        /// Determines if this CRS is equal to <paramref name="other" /> CRS in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other">CRS to compare to this CRS.</param>
        /// <returns><c>true</c> if CRSs are equal. <c>false</c> otherwise.</returns>
        public bool Equals(NamedCrs other)
        {
            if (object.ReferenceEquals(null, other))
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(this.Name, other.Name);
        }
    }
}
