//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.SystemUsage
{
    using Microsoft.Azure.Documents.Rntbd;

    internal class MinThreads : ISystemUsage
    {
        private readonly SystemUsageLoad systemUsage;

        public MinThreads(SystemUsageLoad systemUsage)
        {
            this.systemUsage = systemUsage;
        }

        public override long? ValueToRecord()
        {
            return (long?)this.systemUsage.ThreadInfo?.MinThreads;
        }
    }
}
