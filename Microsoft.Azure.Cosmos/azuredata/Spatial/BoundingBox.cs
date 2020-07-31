//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a coordinate range for geometries in the Azure Cosmos DB service.
    /// </summary>
    /// <see link="https://tools.ietf.org/html/rfc7946#section-5"/>
    [DataContract]
    internal class BoundingBox : IEquatable<BoundingBox>, IEnumerable<double>
    {
        private readonly IReadOnlyList<(double, double)> points;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoundingBox" /> class in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="southwesterlyPoint">The lower left point of the bounding box.</param>
        /// <param name="northeasterlyPoint">The upper right point of the bounding box.</param>
        public BoundingBox((double, double) southwesterlyPoint, (double, double) northeasterlyPoint)
            : this(southwesterlyPoint, northeasterlyPoint, moreNortheasertlyPoints: null)
        {
        }

        public BoundingBox(
            (double, double) southwesterlyPoint,
            (double, double) northeasterlyPoint,
            IReadOnlyList<(double, double)> moreNortheasertlyPoints)
        {
            List<(double, double)> points = new List<(double, double)>()
            {
                southwesterlyPoint,
                northeasterlyPoint,
            };

            if (moreNortheasertlyPoints != null)
            {
                points.AddRange(moreNortheasertlyPoints);
            }

            this.points = points;
        }

        public double this[int index] => index % 2 == 0 ? this.points[index / 2].Item1 : this.points[index / 2].Item2;

        public (double, double) SouthwesterlyPoint => this.points[0];

        public (double, double) NortheasertlyPoint => this.NortheasertlyPoints(index: 0);

        public (double, double) NortheasertlyPoints(int index) => this.points[index + 1];

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

            return this.points.SequenceEqual(other.points);
        }

        /// <summary>
        /// Determines whether the specified <see cref="BoundingBox"/> is equal to the current <see cref="BoundingBox"/> in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj) => obj is BoundingBox boundingBox && this.Equals(boundingBox);

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
                return this.points.Aggregate(0, (current, value) => (current * 397) ^ value.GetHashCode());
            }
        }

        public IEnumerator<double> GetEnumerator() => new BoundingBoxEnumerator(this.points);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private sealed class BoundingBoxEnumerator : IEnumerator<double>
        {
            private readonly IReadOnlyList<(double, double)> points;
            private int index;

            public BoundingBoxEnumerator(IReadOnlyList<(double, double)> points)
            {
                this.points = points ?? throw new ArgumentNullException(nameof(points));
                this.Reset();
            }

            public double Current => this.index % 2 == 0 ? this.points[this.index / 2].Item1 : this.points[this.index / 2].Item2;

            object IEnumerator.Current => this.Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                this.index++;
                return this.index < this.points.Count * 2;
            }

            public void Reset()
            {
                this.index = -1;
            }
        }
    }
}
