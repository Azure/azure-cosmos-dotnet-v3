//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.IO;
    using Azure;
    using Azure.Core.Http;
    using Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;

    internal static class CoreExtensions
    {
        internal static Stream GetStream(this HttpPipelineRequestContent content)
        {
            if (content == null)
            {
                return null;
            }

            CosmosStreamContent cosmosContent = content as CosmosStreamContent;
            if (cosmosContent != null)
            {
                return cosmosContent.Detach();
            }

            // Return stream
            throw new NotImplementedException();
        }

        internal static Response EnsureSuccessStatusCode(this Response response)
        {
            if (response.Status < 200 || response.Status >= 300)
            {
                ResponseMessage responseMessage = response as ResponseMessage;
                if (responseMessage != null)
                {
                    return responseMessage.EnsureSuccessStatusCode();
                }

                string message = $"Response status code does not indicate success: {response.Status} Reason: ({response.ReasonPhrase}).";
                throw new CosmosException(
                        response,
                        message);
            }

            return response;
        }

        internal static bool IsSuccessStatusCode(this Response response) => response.Status >= 200 && response.Status <= 299;

        internal static SubStatusCodes GetSubStatusCode(this ResponseHeaders httpHeaders)
        {
            if (httpHeaders.TryGetValue(WFConstants.BackendHeaders.SubStatus, out string subStatusCodeString))
            {
                return Headers.GetSubStatusCodes(subStatusCodeString);
            }

            return SubStatusCodes.Unknown;
        }

        internal static string GetContinuationToken(this ResponseHeaders httpHeaders)
        {
            httpHeaders.TryGetValue(HttpConstants.HttpHeaders.Continuation, out string continuationToken);
            return continuationToken;
        }

        internal static string GetActivityId(this ResponseHeaders httpHeaders)
        {
            httpHeaders.TryGetValue(HttpConstants.HttpHeaders.ActivityId, out string activityId);
            return activityId;
        }

        internal static string GetSession(this ResponseHeaders httpHeaders)
        {
            httpHeaders.TryGetValue(HttpConstants.HttpHeaders.SessionToken, out string session);
            return session;
        }

        internal static double GetRequestCharge(this ResponseHeaders httpHeaders)
        {
            if (httpHeaders.TryGetValue(HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
            {
                return string.IsNullOrEmpty(requestChargeString) ? 0 : double.Parse(requestChargeString, CultureInfo.InvariantCulture);
            }

            return 0;
        }
    }
}
