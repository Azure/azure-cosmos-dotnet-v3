//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// A type that represents a parent range and a child range that overlaps, not a subset.
    /// </summary>
    internal class Overlaps
    {
        public Documents.Routing.Range<string> ParentRange { get; }

        public Documents.Routing.Range<string> ChildRange { get; }

        public static Overlaps Create(Documents.Routing.Range<string> parentRange, Documents.Routing.Range<string> childRange)
        {
            return new Overlaps(parentRange, childRange);
        }

        private Overlaps(Documents.Routing.Range<string> parentRange, Documents.Routing.Range<string> childRange)
        {
            this.ParentRange = parentRange;
            this.ChildRange = childRange;
        }
    }
}
