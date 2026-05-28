//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents.Routing;

    internal class RangeComparerProvider
    {
        /// <summary>
        /// Gets the minimum and maximum range comparers based on the current configuration.
        /// </summary>
        /// <returns>Tuple of (minComparer, maxComparer)</returns>
        public static (IComparer<Range<string>> minComparer, IComparer<Range<string>> maxComparer) GetComparers(bool useLengthAwareComparison)
        {
            return (
                useLengthAwareComparison ? Range<string>.LengthAwareMinComparer.Instance : Range<string>.MinComparer.Instance,
                useLengthAwareComparison ? Range<string>.LengthAwareMaxComparer.Instance : Range<string>.MaxComparer.Instance
            );
        }
    }
}
