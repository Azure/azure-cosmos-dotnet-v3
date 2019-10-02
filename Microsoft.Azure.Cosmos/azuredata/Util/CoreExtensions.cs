//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using global::Azure;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Documents;

    internal static class CoreExtensions
    {
        private static readonly char[] NewLineCharacters = new[] { '\r', '\n' };

        internal static Response ToResponse(this DocumentClientException dce, RequestMessage request)
        {
            // if StatusCode is null it is a client business logic error and it never hit the backend, so throw
            if (dce.StatusCode == null)
            {
                throw dce;
            }

            // if there is a status code then it came from the backend, return error as http error instead of throwing the exception
            ResponseMessage response = new ResponseMessage(dce.StatusCode ?? HttpStatusCode.InternalServerError, request);
            string reasonPhraseString = string.Empty;
            if (!string.IsNullOrEmpty(dce.Message))
            {
                if (dce.Message.IndexOfAny(CoreExtensions.NewLineCharacters) >= 0)
                {
                    StringBuilder sb = new StringBuilder(dce.Message);
                    sb = sb.Replace("\r", string.Empty);
                    sb = sb.Replace("\n", string.Empty);
                    reasonPhraseString = sb.ToString();
                }
                else
                {
                    reasonPhraseString = dce.Message;
                }
            }

            response.ErrorMessage = reasonPhraseString;
            response.Error = dce.Error;

            if (dce.Headers != null)
            {
                foreach (string header in dce.Headers.AllKeys())
                {
                    response.Headers.Add(header, dce.Headers[header]);
                }
            }

            if (request != null)
            {
                request.Properties.Remove(nameof(DocumentClientException));
                request.Properties.Add(nameof(DocumentClientException), dce);
            }

            return response;
        }

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
    }
}
