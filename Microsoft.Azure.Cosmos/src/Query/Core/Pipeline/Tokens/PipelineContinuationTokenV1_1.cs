// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Tokens
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    /// <summary>
    /// Pipelined continuation where we start versioning.
    /// </summary>
    internal sealed class PipelineContinuationTokenV1_1 : PipelineContinuationToken
    {
        public static readonly Version VersionNumber = new Version(major: 1, minor: 1);

        private const string SourceContinuationTokenPropertyName = "SourceContinuationToken";
        private const string QueryPlanPropertyName = "QueryPlan";

        public PipelineContinuationTokenV1_1(
            PartitionedQueryExecutionInfo queryPlan,
            CosmosElement sourceContinuationToken)
            : base(PipelineContinuationTokenV1_1.VersionNumber)
        {
            this.QueryPlan = queryPlan;
            this.SourceContinuationToken = sourceContinuationToken ?? throw new ArgumentNullException(nameof(sourceContinuationToken));
        }

        public CosmosElement SourceContinuationToken { get; }

        public PartitionedQueryExecutionInfo QueryPlan { get; }

        public override string ToString()
        {
            return this.ToString(int.MaxValue);
        }

        public string ToString(int lengthLimitInBytes)
        {
            string queryPlanString = this.QueryPlan?.ToString();
            bool shouldSerializeQueryPlan;
            if (queryPlanString == null)
            {
                shouldSerializeQueryPlan = false;
            }
            else
            {
                shouldSerializeQueryPlan = (queryPlanString.Length + this.SourceContinuationToken.ToString().Length) < lengthLimitInBytes;
            }

            return CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                {
                    PipelineContinuationToken.VersionPropertyName,
                    CosmosString.Create(this.Version.ToString())
                },
                {
                    PipelineContinuationTokenV1_1.QueryPlanPropertyName,
                    shouldSerializeQueryPlan ? (CosmosElement)CosmosString.Create(queryPlanString) : (CosmosElement)CosmosNull.Create()
                },
                {
                    PipelineContinuationTokenV1_1.SourceContinuationTokenPropertyName,
                    this.SourceContinuationToken
                },
            }).ToString();
        }

        public static bool TryCreateFromCosmosElement(
            CosmosObject parsedContinuationToken,
            out PipelineContinuationTokenV1_1 pipelinedContinuationToken)
        {
            if (parsedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedContinuationToken));
            }

            if (!PipelineContinuationToken.TryParseVersion(
                parsedContinuationToken,
                out Version version))
            {
                pipelinedContinuationToken = default;
                return false;
            }

            if (version != PipelineContinuationTokenV1_1.VersionNumber)
            {
                pipelinedContinuationToken = default;
                return false;
            }

            if (!PipelineContinuationTokenV1_1.TryParseQueryPlan(
                parsedContinuationToken,
                out PartitionedQueryExecutionInfo queryPlan))
            {
                pipelinedContinuationToken = default;
                return false;
            }

            if (!parsedContinuationToken.TryGetValue(
                SourceContinuationTokenPropertyName,
                out CosmosElement sourceContinuationToken))
            {
                pipelinedContinuationToken = default;
                return false;
            }

            pipelinedContinuationToken = new PipelineContinuationTokenV1_1(queryPlan, sourceContinuationToken);
            return true;
        }

        private static bool TryParseQueryPlan(
            CosmosObject parsedContinuationToken,
            out PartitionedQueryExecutionInfo queryPlan)
        {
            if (parsedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedContinuationToken));
            }

            if (!parsedContinuationToken.TryGetValue(
                PipelineContinuationTokenV1_1.QueryPlanPropertyName,
                out CosmosElement parsedQueryPlan))
            {
                queryPlan = default;
                return false;
            }

            if (parsedQueryPlan is CosmosNull)
            {
                queryPlan = null;
                return true;
            }
            else if (parsedQueryPlan is CosmosString queryPlanString)
            {
                if (!PartitionedQueryExecutionInfo.TryParse(queryPlanString.Value, out queryPlan))
                {
                    queryPlan = default;
                    return false;
                }
            }
            else
            {
                queryPlan = default;
                return false;
            }

            return true;
        }
    }
}
