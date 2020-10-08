// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Tokens
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Pipelined continuation where we start versioning.
    /// </summary>
    internal sealed class PipelineContinuationTokenV1 : PipelineContinuationToken
    {
        public static readonly Version VersionNumber = new Version(major: 1, minor: 0);

        private static readonly string SourceContinuationTokenPropertyName = "SourceContinuationToken";

        public PipelineContinuationTokenV1(CosmosElement sourceContinuationToken)
            : base(PipelineContinuationTokenV1.VersionNumber)
        {
            this.SourceContinuationToken = sourceContinuationToken ?? throw new ArgumentNullException(nameof(sourceContinuationToken));
        }

        public CosmosElement SourceContinuationToken { get; }

        public override string ToString()
        {
            return CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                {
                    PipelineContinuationToken.VersionPropertyName,
                    CosmosString.Create(this.Version.ToString())
                },
                {
                    PipelineContinuationTokenV1.SourceContinuationTokenPropertyName,
                    this.SourceContinuationToken
                },
            }).ToString();
        }

        public static bool TryCreateFromCosmosElement(
            CosmosObject parsedContinuationToken,
            out PipelineContinuationTokenV1 pipelinedContinuationTokenV1)
        {
            if (parsedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedContinuationToken));
            }

            if (!PipelineContinuationToken.TryParseVersion(
                parsedContinuationToken,
                out Version version))
            {
                pipelinedContinuationTokenV1 = default;
                return false;
            }

            if (version != PipelineContinuationTokenV1.VersionNumber)
            {
                pipelinedContinuationTokenV1 = default;
                return false;
            }

            if (!parsedContinuationToken.TryGetValue(
                SourceContinuationTokenPropertyName,
                out CosmosElement sourceContinuationToken))
            {
                pipelinedContinuationTokenV1 = default;
                return false;
            }

            pipelinedContinuationTokenV1 = new PipelineContinuationTokenV1(sourceContinuationToken);
            return true;
        }

        public static PipelineContinuationTokenV1 CreateFromV0Token(
            PipelineContinuationTokenV0 pipelinedContinuationTokenV0)
        {
            if (pipelinedContinuationTokenV0 == null)
            {
                throw new ArgumentNullException(nameof(pipelinedContinuationTokenV0));
            }

            return new PipelineContinuationTokenV1(pipelinedContinuationTokenV0.SourceContinuationToken);
        }
    }
}
