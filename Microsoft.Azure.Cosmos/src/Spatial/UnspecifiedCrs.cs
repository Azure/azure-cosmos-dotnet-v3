//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Unspecified CRS. If a geometry has this CRS, no CRS can be assumed for it according to GeoJSON spec.
    /// </summary>
    [DataContract]
    internal class UnspecifiedCrs : Crs, IEquatable<UnspecifiedCrs>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnspecifiedCrs"/> class.
        /// </summary>
        public UnspecifiedCrs()
            : base(CrsType.Unspecified)
        {
        }

        /// <summary>
        /// Determines whether the specified <see cref="LinkedCrs"/> is equal to the current <see cref="LinkedCrs"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as UnspecifiedCrs);
        }

        /// <summary>
        /// Serves as a hash function for <see cref="LinkedCrs"/>. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="LinkedCrs"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return 0;
            }
        }

        /// <summary>
        /// Determines if this <see cref="LinkedCrs"/> is equal to <paramref name="other" />.
        /// </summary>
        /// <param name="other"><see cref="LinkedCrs"/> to compare to this <see cref="LinkedCrs"/>.</param>
        /// <returns><c>true</c> if CRSs are equal. <c>false</c> otherwise.</returns>
        public bool Equals(UnspecifiedCrs other)
        {
            if (other is null)
            {
                return false;
            }

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }

            return true;
        }
    }
}
