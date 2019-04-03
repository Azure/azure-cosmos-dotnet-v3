//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// State across Change Feed requests
    /// </summary>
    internal class ChangeFeedState
    {
        public string StartEffectivePartitionKeyString { get; set; }

        public string EndEffectivePartitionKeyString { get; set; }

        public bool StartFromBeginning { get; set; }

        public DateTime? StartTime { get; set; }
    }
}
