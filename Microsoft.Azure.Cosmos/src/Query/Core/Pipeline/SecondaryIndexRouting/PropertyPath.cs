//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents an immutable sequence of literal property names or a wildcard projection.
    /// </summary>
    internal sealed class PropertyPath : IEquatable<PropertyPath>
    {
        public PropertyPath(IEnumerable<string> segments)
        {
            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            string[] copy = segments.ToArray();
            if (copy.Length == 0 || copy.Any(segment => segment == null))
            {
                throw new ArgumentException(
                    "A property path must contain at least one segment and no null segments.",
                    nameof(segments));
            }

            this.IsWildcard = false;
            this.Segments = Array.AsReadOnly(copy);
        }

        private PropertyPath()
        {
            this.IsWildcard = true;
            this.Segments = Array.Empty<string>();
        }

        public static PropertyPath Wildcard { get; } = new PropertyPath();

        public bool IsWildcard { get; }

        public IReadOnlyList<string> Segments { get; }

        public bool Equals(PropertyPath other)
        {
            if (other == null || this.IsWildcard != other.IsWildcard || this.Segments.Count != other.Segments.Count)
            {
                return false;
            }

            for (int i = 0; i < this.Segments.Count; i++)
            {
                if (!StringComparer.Ordinal.Equals(this.Segments[i], other.Segments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => obj is PropertyPath other && this.Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = this.IsWildcard ? 1 : 0;
                foreach (string segment in this.Segments)
                {
                    hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(segment);
                }

                return hash;
            }
        }
    }
}
