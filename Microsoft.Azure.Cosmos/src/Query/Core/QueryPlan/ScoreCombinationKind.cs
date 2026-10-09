//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    /// <summary>
    /// Specifies how a hybrid query combines component scores.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    enum ScoreCombinationKind
    {
        /// <summary>
        /// Combines reciprocal component ranks.
        /// </summary>
        Rrf,

        /// <summary>
        /// Combines direction-adjusted component scores using a weighted sum.
        /// </summary>
        CombinedScore,
    }
}
