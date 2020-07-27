//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using global::Azure.Core.Pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrieveTenantIdPipelinePolicy"/> class.
    /// This helps out in building a pipeline policy which is passed to KeyClient and in turn parsing out required information in Response message.
    /// Reusing this class from Azure Key Vault as implemented <see href="https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/keyvault/Azure.Security.KeyVault.Shared/src/ChallengeBasedAuthenticationPolicy.cs#L240-L256"> here </see>.
    /// </summary>
    internal sealed class RetrieveTenantIdPipelinePolicy : HttpPipelinePolicy
    {
        public string TenantId { get; private set; }

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            this.ProcessCoreAsync(message, pipeline, false).GetAwaiter().GetResult();
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            return this.ProcessCoreAsync(message, pipeline, true);
        }

        private async ValueTask ProcessCoreAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline, bool async)
        {
            if (message.Request.Uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Bearer token authentication is not permitted for non TLS protected (https) endpoints.");
            }

            RequestContent originalContent = message.Request.Content;

            if (async)
            {
                // go through the pipeline.
                await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
            }
            else
            {
                ProcessNext(message, pipeline);
            }

            // Start processing each of the response and if we get a 401 go through the response and get the Authority and Tenant ID
            if (message.Response.Status == 401)
            {
                // set the content to the original content in case it was cleared
                message.Request.Content = originalContent;

                // get the tenant ID
                this.TenantId = AuthenticationChallenge.GetTenantIdFromResponse(message.Response);

                // Return an OK Response only when we have a probable TenantId else fallback to the original response
                // which will result in KeyVault throwing the required Exception on the way back.
                if (!string.IsNullOrEmpty(this.TenantId) && !string.IsNullOrWhiteSpace(this.TenantId))
                {
                    // we are done with this response,signal an 200/OK message back to prevent exception.
                    HttpResponseMessage okResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);

                    // use the original RequestID and Content Stream and build a response.
                    KeyVaultClientPipelineResponse pipelineResponse = new KeyVaultClientPipelineResponse(message.Response.ClientRequestId, okResponseMessage, message.Response.ContentStream);
                    message.Response = pipelineResponse;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultClientPipelineResponse"/> class.
        /// </summary>
        private class KeyVaultClientPipelineResponse : Response
        {
            private readonly HttpResponseMessage responseMessage;

            private readonly HttpContent responseContent;

            private Stream? contentStream;

            public KeyVaultClientPipelineResponse(string requestId, HttpResponseMessage responseMessage, Stream? contentStream)
            {
                this.ClientRequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
                this.responseMessage = responseMessage ?? throw new ArgumentNullException(nameof(responseMessage));
                this.contentStream = contentStream;
                this.responseContent = this.responseMessage.Content;
            }

            public override int Status => (int)this.responseMessage.StatusCode;

            public override string ReasonPhrase => this.responseMessage.ReasonPhrase;

            public override Stream? ContentStream
            {
                get => this.contentStream;
                set
                {
                    // Make sure we don't dispose the content if the stream was replaced
                    this.responseMessage.Content = null;

                    this.contentStream = value;
                }
            }

            protected override bool ContainsHeader(string name)
            {
                if (this.responseMessage.Headers.TryGetValues(name, out _))
                {
                    return true;
                }

                return this.responseContent?.Headers.TryGetValues(name, out _) == true;
            }

            public override string ClientRequestId { get; set; }

            public override void Dispose()
            {
                this.responseMessage?.Dispose();
            }

            public override string ToString() => this.responseMessage.ToString();

            protected override IEnumerable<HttpHeader> EnumerateHeaders()
            {
                throw new NotImplementedException();
            }

            protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
            {
                values = null;
                return false;
            }

            protected override bool TryGetHeader(string name, out string value)
            {
                value = null;
                return false;
            }
        }

        private sealed class AuthenticationChallenge
        {
            private static readonly string[] ChallengeDelimiters = new string[] { "," };

            public static string GetTenantIdFromResponse(Response response)
            {
                string tenantID = null;

                if (response.Headers.TryGetValue(KeyVaultConstants.AuthenticationResponseHeaderName, out string challengeValue) && challengeValue.StartsWith(KeyVaultConstants.AuthenticationChallengePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    tenantID = ParseBearerChallengeHeaderValue(challengeValue);
                }

                return tenantID;
            }

            private static string ParseBearerChallengeHeaderValue(string challengeValue)
            {
                string tenantId;

                // remove the bearer challenge prefix
                string trimmedChallenge = challengeValue.Substring(KeyVaultConstants.AuthenticationChallengePrefix.Length);

                // Split the trimmed challenge into a set of name=value strings that
                // are comma separated. The value fields are expected to be within
                // quotation characters that are stripped here.
                string[] pairs = trimmedChallenge.Split(ChallengeDelimiters, StringSplitOptions.RemoveEmptyEntries);

                if (pairs.Length > 0)
                {
                    // Process the name=value string
                    for (int i = 0; i < pairs.Length; i++)
                    {
                        string[] pair = pairs[i].Split('=');

                        if (pair.Length == 2)
                        {
                            // We have a key and a value, now need to trim and decode
                            string key = pair[0].AsSpan().Trim().Trim('\"').ToString();
                            string value = pair[1].AsSpan().Trim().Trim('\"').ToString();

                            if (!string.IsNullOrEmpty(key))
                            {
                                if (string.Equals(key, "authorization", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "authorization_uri", StringComparison.OrdinalIgnoreCase))
                                {
                                    // extract the tenant ID and return it.
                                    return tenantId = value.Substring(value.LastIndexOf('/') + 1);
                                }
                            }
                        }
                    }
                }

                return null;
            }
        }
    }
}
