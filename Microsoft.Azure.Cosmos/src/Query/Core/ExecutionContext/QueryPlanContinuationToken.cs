// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using Newtonsoft.Json;

    internal sealed class QueryPlanContinuationToken
    {
        public QueryPlanContinuationToken(
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            string sourceContinuationToken)
        {
            // partitionedQueryExecutionInfo is allowed to be null.
            if (string.IsNullOrWhiteSpace(sourceContinuationToken))
            {
                throw new ArgumentException($"{nameof(sourceContinuationToken)} is not allowed to be null empty or whitespace.");
            }

            this.PartitionedQueryExecutionInfo = partitionedQueryExecutionInfo;
            this.SourceContinuationToken = sourceContinuationToken;
        }

        public PartitionedQueryExecutionInfo PartitionedQueryExecutionInfo { get; }

        public string SourceContinuationToken { get; }

        public static string Serialize(
            QueryPlanContinuationToken queryPlanContinuationToken,
            int responseContinuationTokenLengthLimit = int.MaxValue)
        {
            if (queryPlanContinuationToken == null)
            {
                throw new ArgumentNullException(nameof(queryPlanContinuationToken));
            }

            if (responseContinuationTokenLengthLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(responseContinuationTokenLengthLimit));
            }

            string serializedContinuationToken = JsonConvert.SerializeObject(queryPlanContinuationToken);

            // TODO: in the future this check can be more optimized by just summing up the length of the components.
            if (serializedContinuationToken.Length >= responseContinuationTokenLengthLimit)
            {
                // If the query plan is too long,
                // then don't serialize it.
                // But we still need to keep the required properties.
                QueryPlanContinuationToken minimalToken = new QueryPlanContinuationToken(
                    partitionedQueryExecutionInfo: null,
                    sourceContinuationToken: queryPlanContinuationToken.SourceContinuationToken);
                serializedContinuationToken = JsonConvert.SerializeObject(minimalToken);
            }

            return serializedContinuationToken;
        }

        public static bool TryParse(string value, out QueryPlanContinuationToken queryPlanContinuationToken)
        {
            queryPlanContinuationToken = default(QueryPlanContinuationToken);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                queryPlanContinuationToken = JsonConvert.DeserializeObject<QueryPlanContinuationToken>(value);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
