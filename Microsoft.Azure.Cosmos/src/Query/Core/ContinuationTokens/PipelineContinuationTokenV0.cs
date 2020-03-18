// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Pipelined continuation token before we started versioning continuation tokens.
    /// </summary>
    internal sealed class PipelineContinuationTokenV0 : PipelineContinuationToken
    {
        public static readonly Version VersionNumber = new Version(major: 0, minor: 0);

        public PipelineContinuationTokenV0(CosmosElement sourceContinuationToken)
            : base(PipelineContinuationTokenV0.VersionNumber)
        {
            this.SourceContinuationToken = sourceContinuationToken ?? throw new ArgumentNullException(nameof(sourceContinuationToken));
        }

        public CosmosElement SourceContinuationToken { get; }

        public override string ToString()
        {
            return this.SourceContinuationToken.ToString();
        }

        public static bool TryCreateFromCosmosElement(
            CosmosElement cosmosElement,
            out PipelineContinuationTokenV0 pipelineContinuationTokenV0)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException(nameof(cosmosElement));
            }

            pipelineContinuationTokenV0 = new PipelineContinuationTokenV0(cosmosElement);
            return true;
        }
    }
}
