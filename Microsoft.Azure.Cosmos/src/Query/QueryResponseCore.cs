//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;

    internal struct QueryResponseCore
    {
        private static readonly IReadOnlyList<CosmosElement> EmptyList = new List<CosmosElement>();

        private QueryResponseCore(
            IReadOnlyList<CosmosElement> result,
            bool isSuccess,
            HttpStatusCode statusCode,
            double requestCharge,
            string activityId,
            string queryMetricsText,
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics,
            ClientSideRequestStatistics requestStatistics,
            long responseLengthBytes,
            string disallowContinuationTokenMessage,
            string continuationToken,
            string errorMessage,
            SubStatusCodes? subStatusCode)
        {
            this.IsSuccess = isSuccess;
            this.CosmosElements = result;
            this.StatusCode = statusCode;
            this.ActivityId = activityId;
            this.QueryMetricsText = queryMetricsText;
            this.QueryMetrics = queryMetrics;
            this.ResponseLengthBytes = responseLengthBytes;
            this.RequestCharge = requestCharge;
            this.DisallowContinuationTokenMessage = disallowContinuationTokenMessage;
            this.ContinuationToken = continuationToken;
            this.ErrorMessage = errorMessage;
            this.SubStatusCode = subStatusCode;
            this.RequestStatistics = requestStatistics;
        }

        internal IReadOnlyList<CosmosElement> CosmosElements { get; }

        internal string ErrorMessage { get; }

        internal SubStatusCodes? SubStatusCode { get; }

        internal HttpStatusCode StatusCode { get; }

        internal string DisallowContinuationTokenMessage { get; }

        internal string ContinuationToken { get; }

        internal double RequestCharge { get; }

        internal string ActivityId { get; }

        internal ClientSideRequestStatistics RequestStatistics { get; }

        internal string QueryMetricsText { get; }

        internal IReadOnlyDictionary<string, QueryMetrics> QueryMetrics { get; set; }

        internal long ResponseLengthBytes { get; }

        internal bool IsSuccess { get; }

        internal static QueryResponseCore CreateSuccess(
            IReadOnlyList<CosmosElement> result,
            double requestCharge,
            string activityId,
            string queryMetricsText,
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics,
            ClientSideRequestStatistics requestStatistics,
            long responseLengthBytes,
            string disallowContinuationTokenMessage,
            string continuationToken)
        {
            QueryResponseCore cosmosQueryResponse = new QueryResponseCore(
               result: result,
               isSuccess: true,
               statusCode: HttpStatusCode.OK,
               requestCharge: requestCharge,
               activityId: activityId,
               queryMetricsText: queryMetricsText,
               queryMetrics: queryMetrics,
               requestStatistics: requestStatistics,
               responseLengthBytes: responseLengthBytes,
               disallowContinuationTokenMessage: disallowContinuationTokenMessage,
               continuationToken: continuationToken,
               errorMessage: null,
               subStatusCode: null);

            return cosmosQueryResponse;
        }

        internal static QueryResponseCore CreateFailure(
            HttpStatusCode statusCode,
            SubStatusCodes? subStatusCodes,
            string errorMessage,
            double requestCharge,
            string activityId,
            string queryMetricsText,
            IReadOnlyDictionary<string, QueryMetrics> queryMetrics)
        {
            QueryResponseCore cosmosQueryResponse = new QueryResponseCore(
                result: QueryResponseCore.EmptyList,
                isSuccess: false,
                statusCode: statusCode,
                requestCharge: requestCharge,
                activityId: activityId,
                queryMetricsText: queryMetricsText,
                queryMetrics: queryMetrics,
                requestStatistics: null,
                responseLengthBytes: 0,
                disallowContinuationTokenMessage: null,
                continuationToken: null,
                errorMessage: errorMessage,
                subStatusCode: subStatusCodes);

            return cosmosQueryResponse;
        }
    }
}
