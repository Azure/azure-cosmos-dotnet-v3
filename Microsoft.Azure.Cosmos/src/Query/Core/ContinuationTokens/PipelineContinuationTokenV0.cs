// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;

    /// <summary>
    /// Pipelined continuation token before we started versioning continuation tokens.
    /// </summary>
    internal sealed class PipelineContinuationTokenV0 : PipelineContinuationToken
    {
        public static readonly Version VersionNumber = new Version(major: 0, minor: 0);

        public PipelineContinuationTokenV0(string sourceContinuationToken)
            : base(PipelineContinuationTokenV0.VersionNumber)
        {
            this.SourceContinuationToken = sourceContinuationToken ?? throw new ArgumentNullException(nameof(sourceContinuationToken));
        }

        public string SourceContinuationToken { get; }

        public override string ToString()
        {
            return this.SourceContinuationToken;
        }

        public static bool TryParse(
            string rawContinuationToken,
            out PipelineContinuationTokenV0 pipelinedContinuationTokenV0)
        {
            if (rawContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(rawContinuationToken));
            }

            pipelinedContinuationTokenV0 = new PipelineContinuationTokenV0(rawContinuationToken);
            return true;
        }
    }
}
