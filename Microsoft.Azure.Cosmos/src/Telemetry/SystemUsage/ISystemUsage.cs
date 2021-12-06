//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal abstract class ISystemUsage
    {
        public abstract long? ValueToRecord();

        public virtual int AggregationAdjustment => 1;
    }
}
