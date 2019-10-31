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
    internal sealed class PipelineContinuationTokenV1_1 : PipelineContinuationToken
    {
        public static readonly Version VersionNumber = new Version(major: 1, minor: 1);

        private const string SourceContinuationTokenPropertyName = "SourceContinuationToken";
        private const string QueryPlanPropertyName = "QueryPlan";

        public PipelineContinuationTokenV1_1(
            PartitionedQueryExecutionInfo queryPlan,
            string sourceContinuationToken)
            : base(PipelineContinuationTokenV1_1.VersionNumber)
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
            return this.Serialize(16 * 1024);
        }

        public string Serialize(int lengthLimitInBytes)
        {
            string queryPlanString = this.QueryPlan?.ToString();
            bool shouldSerializeQueryPlan;
            if (queryPlanString == null)
            {
                shouldSerializeQueryPlan = false;
            }
            else
            {
                shouldSerializeQueryPlan = (queryPlanString.Length + this.SourceContinuationToken.Length) < lengthLimitInBytes;
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
                    CosmosString.Create(this.SourceContinuationToken)
                },
            }).ToString();
        }

        public static bool TryParse(
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
                pipelinedContinuationToken = default(PipelineContinuationTokenV1_1);
                return false;
            }

            if (version != PipelineContinuationTokenV1_1.VersionNumber)
            {
                pipelinedContinuationToken = default(PipelineContinuationTokenV1_1);
                return false;
            }

            if (!PipelineContinuationTokenV1_1.TryParseQueryPlan(
                parsedContinuationToken,
                out PartitionedQueryExecutionInfo queryPlan))
            {
                pipelinedContinuationToken = default(PipelineContinuationTokenV1_1);
                return false;
            }

            if (!PipelineContinuationTokenV1_1.TryParseSourceContinuationToken(
                parsedContinuationToken,
                out string sourceContinuationToken))
            {
                pipelinedContinuationToken = default(PipelineContinuationTokenV1_1);
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
                PipelineContinuationTokenV1_1.SourceContinuationTokenPropertyName,
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
