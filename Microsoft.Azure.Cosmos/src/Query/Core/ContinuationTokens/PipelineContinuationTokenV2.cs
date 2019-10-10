// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Pipelined continuation where we start versioning.
    /// </summary>
    internal sealed class PipelineContinuationTokenV2 : PipelineContinuationToken
    {
        public static readonly Version Version2 = new Version(major: 2, minor: 0);

        private static readonly string SourceContinuationTokenPropertyName = "SourceContinuationToken";
        private static readonly string QueryPlanPropertyName = "QueryPlan";

        public PipelineContinuationTokenV2(
            PartitionedQueryExecutionInfo queryPlan,
            string sourceContinuationToken)
            : base(PipelineContinuationTokenV2.Version2)
        {
            // Query Plan is allowed to be null.
            if (sourceContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(sourceContinuationToken));
            }

            this.QueryPlan = queryPlan;
            this.SourceContinuationToken = sourceContinuationToken;
        }

        public string SourceContinuationToken { get; }

        public PartitionedQueryExecutionInfo QueryPlan { get; }

        public override string ToString()
        {
            return CosmosObject.Create(new Dictionary<string, CosmosElement>()
            {
                {
                    PipelineContinuationTokenV2.QueryPlanPropertyName,
                    this.QueryPlan == null ? (CosmosElement)CosmosNull.Create() : (CosmosElement)CosmosString.Create(this.QueryPlan.ToString())
                },
                {
                    PipelineContinuationToken.VersionPropertyName,
                    CosmosString.Create(this.Version.ToString())
                },
                {
                    PipelineContinuationTokenV2.SourceContinuationTokenPropertyName,
                    CosmosString.Create(this.SourceContinuationToken)
                },
            }).ToString();
        }

        public static bool TryParse(
            CosmosObject parsedContinuationToken,
            out PipelineContinuationTokenV2 pipelinedContinuationTokenV2)
        {
            if (parsedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedContinuationToken));
            }

            if (!PipelineContinuationToken.TryParseVersion(
                parsedContinuationToken,
                out Version version))
            {
                pipelinedContinuationTokenV2 = default(PipelineContinuationTokenV2);
                return false;
            }

            if (version != PipelineContinuationTokenV2.Version2)
            {
                pipelinedContinuationTokenV2 = default(PipelineContinuationTokenV2);
                return false;
            }

            if (!PipelineContinuationTokenV2.TryParseQueryPlan(
                parsedContinuationToken,
                out PartitionedQueryExecutionInfo queryPlan))
            {
                pipelinedContinuationTokenV2 = default(PipelineContinuationTokenV2);
                return false;
            }

            if (!PipelineContinuationTokenV2.TryParseSourceContinuationToken(
                parsedContinuationToken,
                out string sourceContinuationToken))
            {
                pipelinedContinuationTokenV2 = default(PipelineContinuationTokenV2);
                return false;
            }

            pipelinedContinuationTokenV2 = new PipelineContinuationTokenV2(queryPlan, sourceContinuationToken);
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
                PipelineContinuationTokenV2.QueryPlanPropertyName,
                out CosmosElement parsedQueryPlan))
            {
                queryPlan = default(PartitionedQueryExecutionInfo);
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
                    queryPlan = default(PartitionedQueryExecutionInfo);
                    return false;
                }
            }
            else
            {
                queryPlan = default(PartitionedQueryExecutionInfo);
                return false;
            }

            return true;
        }

        private static bool TryParseSourceContinuationToken(
            CosmosObject parsedContinuationToken,
            out string sourceContinuationToken)
        {
            if (parsedContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(parsedContinuationToken));
            }

            if (!parsedContinuationToken.TryGetValue<CosmosString>(
                PipelineContinuationTokenV2.SourceContinuationTokenPropertyName,
                out CosmosString parsedSourceContinuationToken))
            {
                sourceContinuationToken = default(string);
                return false;
            }

            sourceContinuationToken = parsedSourceContinuationToken.Value;
            return true;
        }
    }
}
