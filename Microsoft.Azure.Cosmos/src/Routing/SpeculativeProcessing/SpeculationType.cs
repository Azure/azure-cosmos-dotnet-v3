// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing.SpeculativeProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Enumeration for the type of speculation.
    /// </summary>
    public enum SpeculationType
    {
        /// <summary>
        /// No speculation.
        /// </summary>
        NONE,

        /// <summary>
        /// Threshold based speculation.
        /// </summary>
        THRESHOLD_BASED,
    }
}
