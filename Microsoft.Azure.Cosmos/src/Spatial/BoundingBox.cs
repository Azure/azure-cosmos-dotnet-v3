//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Spatial.Converters;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a coordinate range for geometries in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    [JsonConverter(typeof(BoundingBoxJsonConverter))]
    public sealed class BoundingBox : IEquatable<BoundingBox>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoundingBox" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="min">
        /// Lowest values for all axes of the bounding box.
        /// </param>
        /// <param name="max">
        /// Highest values for all axes of the bounding box.
        /// </param>
        public BoundingBox(Position min, Position max)
        {
            if (max == null)
            {
                throw new ArgumentException("Max");
            }

            if (min == null)
            {
                throw new ArgumentException("Min");
            }

            if (max.Coordinates.Count != min.Coordinates.Count)
            {
                throw new ArgumentException("Max and min must have same cardinality.");
            }

            this.Max = max;
            this.Min = min;
        }

        /// <summary>
        /// Gets lowest values for all axes of the bounding box in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Lowest values for all axes of the bounding box.
        /// </value>
        [DataMember(Name = "min")]
        public Position Min { get; private set; }

        /// <summary>
        /// Gets highest values for all axes of the bounding box in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// Highest values for all axes of the bounding box.
        /// </value>
        [DataMember(Name = "max")]
        public Position Max { get; private set; }

        /// <summary>
        /// Determines if this <see cref="BoundingBox"/> is equal to the <paramref name="other" /> in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="other"><see cref="BoundingBox"/> to compare to this bounding box.</param>
        /// <returns><c>true</c> if bounding boxes are equal. <c>false</c> otherwise.</returns>
        public bool Equals(BoundingBox other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Min.Equals(other.Min) && this.Max.Equals(other.Max);
        }

        /// <summary>
        /// Determines whether the specified <see cref="BoundingBox"/> is equal to the current <see cref="BoundingBox"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as BoundingBox);
        }

        /// <summary>
        /// Serves as a hash function for <see cref="BoundingBox"/> type in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="BoundingBox"/>.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Min.GetHashCode() * 397) ^ this.Max.GetHashCode();
            }
        }
    }
}
