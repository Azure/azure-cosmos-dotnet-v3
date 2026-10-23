//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a sequence of literal property names.
    /// </summary>
    internal sealed class PropertyPath : IEquatable<PropertyPath>
    {
        public PropertyPath(IEnumerable<string> segments)
        {
            this.Segments = segments;
        }

        public IEnumerable<string> Segments { get; }

        public bool Equals(PropertyPath other)
        {
            return other != null && this.Segments.SequenceEqual(other.Segments, StringComparer.Ordinal);
        }

        public override bool Equals(object obj) => obj is PropertyPath other && this.Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            foreach (string segment in this.Segments)
            {
                hash.Add(segment, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
