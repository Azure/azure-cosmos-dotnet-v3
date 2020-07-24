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
    /// Initializes a new instance of the <see cref="KeyVaultClientPipelinePolicy"/> class.
    /// This helps out in building a pipeline policy which is passed to KeyClient and in turn parsing out required information in Response message.
    /// Reusing this class from Azure Key Vault as implemented <see href="https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/keyvault/Azure.Security.KeyVault.Shared/src/ChallengeBasedAuthenticationPolicy.cs#L240-L256"> here </see>.
    /// </summary>
    internal sealed class KeyVaultClientPipelinePolicy : HttpPipelinePolicy
    {
        private readonly AuthenticationChallenge challenge = null;

        public string TenantID { get;  set; }

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            _ = this.ProcessCoreAsync(message, pipeline, false);
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

            // if this policy doesn't have _challenge cached try to get it from the static challenge cache
            AuthenticationChallenge challenge = this.challenge;

            // if we still don't have the challenge for the endpoint
            // remove the content from the request and send without authentication to get the challenge
            if (challenge == null)
            {
                message.Request.Content = null;
            }

            if (async)
            {
                // go through the pipeline.
                await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
            }

            // Start processing each of the response and if we get a 401 go through the response and get the Authority and Tenant ID
            if (message.Response.Status == 401)
            {
                // set the content to the original content in case it was cleared
                message.Request.Content = originalContent;

                // update the cached challenge
                this.TenantID = AuthenticationChallenge.GetChallengeFromResponse(message.Response);

                // we are done with this response,signal an 200/OK message back to prevent exception.
                HttpResponseMessage okresponse = new HttpResponseMessage(HttpStatusCode.OK);

                // use the original RequestID and Content Stream and build a response.
                KeyVaultClientPipelineResponse pipelineResponse = new KeyVaultClientPipelineResponse(message.Response.ClientRequestId, okresponse, message.Response.ContentStream);
                message.Response = pipelineResponse;
                return;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultClientPipelineResponse"/> class.
        /// This PipelineResponse is based on <see href="https://github.com/Azure/azure-sdk-for-net/blob/bbc7e0d6334eec629960164960084cf2b6f068d4/sdk/core/Azure.Core/src/Pipeline/HttpClientTransport.cs"> this implementation </see>.
        /// This is required to send back a response to avoid an exception when GetKeyAsync
        /// is called with an Empty TokenCredenial with an intent to retreive the Tenant ID for the KeyVault-Key URI.
        /// </summary>
        internal sealed class KeyVaultClientPipelineResponse : Response
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

            public override string ClientRequestId { get; set; }

            public override void Dispose()
            {
                this.responseMessage?.Dispose();
            }

            public override string ToString() => this.responseMessage.ToString();

            protected override bool ContainsHeader(string name)
            {
                throw new NotImplementedException();
            }

            protected override IEnumerable<HttpHeader> EnumerateHeaders()
            {
                throw new NotImplementedException();
            }

            protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
            {
                throw new NotImplementedException();
            }

            protected override bool TryGetHeaderValues(string name, [NotNullWhenAttribute(true), NullableAttribute(new[] { 2, 1 })] out IEnumerable<string> values)
            {
                throw new NotImplementedException();
            }

            internal class NullableAttribute : Attribute
            {
                private int[] vs;

                public NullableAttribute(int[] vs)
                {
                    this.vs = vs;
                }
            }

            internal class NotNullWhenAttribute : Attribute
            {
                private bool vs;

                public NotNullWhenAttribute(bool vs)
                {
                    this.vs = vs;
                }
            }
        }

        internal sealed class AuthenticationChallenge
        {
            private static readonly Dictionary<string, AuthenticationChallenge> Cache = new Dictionary<string, AuthenticationChallenge>();
            private static readonly object CacheLock = new object();
            private static readonly string[] ChallengeDelimiters = new string[] { "," };

            public static string GetChallengeFromResponse(Response response)
            {
                string tenantID = null;

                if (response.Headers.TryGetValue("WWW-Authenticate", out string challengeValue) && challengeValue.StartsWith(KeyVaultConstants.Bearer, StringComparison.OrdinalIgnoreCase))
                {
                    tenantID = ParseBearerChallengeHeaderValue(challengeValue);
                }

                return tenantID;
            }

            private static string ParseBearerChallengeHeaderValue(string challengeValue)
            {
                string tenantId;

                // remove the bearer challenge prefix
                string trimmedChallenge = challengeValue.Substring(KeyVaultConstants.Bearer.Length);

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
                                // Ordered by current likelihood.
                                if (string.Equals(key, "authorization", StringComparison.OrdinalIgnoreCase))
                                {
                                    // extract the tenant ID and return it.
                                    tenantId = value.Substring(value.LastIndexOf('/') + 1);

                                    if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrWhiteSpace(tenantId))
                                    {
                                        return tenantId;
                                    }
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
