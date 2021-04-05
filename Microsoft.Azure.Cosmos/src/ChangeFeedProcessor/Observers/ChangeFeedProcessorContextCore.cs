//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using Microsoft.Azure.Cosmos;

    internal sealed class ChangeFeedProcessorContextCore : ChangeFeedProcessorContext
    {
        private readonly ChangeFeedObserverContextCore changeFeedObserverContextCore;

        public ChangeFeedProcessorContextCore(ChangeFeedObserverContextCore changeFeedObserverContextCore)
        {
            this.changeFeedObserverContextCore = changeFeedObserverContextCore;
        }

        public override string LeaseToken => this.changeFeedObserverContextCore.LeaseToken;

        public override CosmosDiagnostics Diagnostics => this.changeFeedObserverContextCore.Diagnostics;

        public override Headers Headers => this.changeFeedObserverContextCore.Headers;
    }
}