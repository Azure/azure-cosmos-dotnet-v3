//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class AuthorizationTokenProviderMasterKey : AuthorizationTokenProvider
    {
        ////The MAC signature found in the HTTP request is not the same as the computed signature.Server used following string to sign
        ////The input authorization token can't serve the request. Please check that the expected payload is built as per the protocol, and check the key being used. Server used the following payload to sign
        private const string MacSignatureString = "to sign";
        private const string EnableAuthFailureTracesConfig = "enableAuthFailureTraces";
        private readonly Lazy<bool> enableAuthFailureTraces;
        private readonly IComputeHash authKeyHashFunction;
        private bool isDisposed = false;

        public AuthorizationTokenProviderMasterKey(IComputeHash computeHash)
        {
            this.authKeyHashFunction = computeHash ?? throw new ArgumentNullException(nameof(computeHash));
            this.enableAuthFailureTraces = new Lazy<bool>(() =>
            {
#if NETSTANDARD20
                // GetEntryAssembly returns null when loaded from native netstandard2.0
                if (System.Reflection.Assembly.GetEntryAssembly() == null)
                {
                        return false;
                }
#endif
                string enableAuthFailureTracesString = System.Configuration.ConfigurationManager.AppSettings[EnableAuthFailureTracesConfig];
                if (string.IsNullOrEmpty(enableAuthFailureTracesString) || 
                    !bool.TryParse(enableAuthFailureTracesString, out bool enableAuthFailureTracesFlag))
                {
                    return false;
                }

                return enableAuthFailureTracesFlag;
            });
        }

        public AuthorizationTokenProviderMasterKey(SecureString authKey)
            : this(new SecureStringHMACSHA256Helper(authKey))
        {
        }

        public AuthorizationTokenProviderMasterKey(string authKey)
            : this(new StringHMACSHA256Hash(authKey))
        {
        }

        public override ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType)
        {
            // this is masterkey authZ
            headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

            string authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                requestVerb,
                resourceAddress,
                resourceType,
                headers,
                this.authKeyHashFunction,
                out AuthorizationHelper.ArrayOwner arrayOwner);

            using (arrayOwner)
            {
                string payload = null;
                if (arrayOwner.Buffer.Count > 0)
                {
                    payload = Encoding.UTF8.GetString(arrayOwner.Buffer.Array, arrayOwner.Buffer.Offset, (int)arrayOwner.Buffer.Count);
                }

                return new ValueTask<(string token, string payload)>((authorizationToken, payload));
            }
        }

        public override ValueTask<string> GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace)
        {
            // this is masterkey authZ
            headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

            string authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                requestVerb,
                resourceAddress,
                resourceType,
                headers,
                this.authKeyHashFunction,
                out AuthorizationHelper.ArrayOwner arrayOwner);

            using (arrayOwner)
            {
                return new ValueTask<string>(authorizationToken);
            }
        }

        public override ValueTask AddAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType)
        {
            string dateTime = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            headersCollection[HttpConstants.HttpHeaders.XDate] = dateTime;

            string token = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                            verb,
                            requestAddress,
                            headersCollection,
                            this.authKeyHashFunction);

            headersCollection.Add(HttpConstants.HttpHeaders.Authorization, token);
            return default;
        }

        public override void TraceUnauthorized(
            DocumentClientException dce,
            string authorizationToken,
            string payload)
        {
            if (payload != null
                && dce.Message != null
                && dce.StatusCode.HasValue
                && dce.StatusCode.Value == HttpStatusCode.Unauthorized
                && dce.Message.Contains(AuthorizationTokenProviderMasterKey.MacSignatureString))
            {
                // The following code is added such that we get trace data on unexpected 401/HMAC errors and it is
                //   disabled by default. The trace will be trigger only when "enableAuthFailureTraces" named configuration 
                //   is set to true (currently true for CTL runs).
                //   For production we will work directly with specific customers in order to enable this configuration.
                string normalizedPayload = AuthorizationTokenProviderMasterKey.NormalizeAuthorizationPayload(payload);
                if (this.enableAuthFailureTraces.Value)
                {
                    string tokenFirst5 = HttpUtility.UrlDecode(authorizationToken).Split('&')[2].Split('=')[1].Substring(0, 5);
                    ulong authHash = 0;
                    if (this.authKeyHashFunction?.Key != null)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(this.authKeyHashFunction?.Key?.ToString());
                        authHash = Documents.Routing.MurmurHash3.Hash64(bytes, bytes.Length);
                    }
                    DefaultTrace.TraceError("Un-expected authorization payload mis-match. Actual payload={0}, token={1}..., hash={2:X}..., error={3}",
                        normalizedPayload, tokenFirst5, authHash, dce.Message);
                }
                else
                {
                    DefaultTrace.TraceError("Un-expected authorization payload mis-match. Actual {0} service expected {1}", normalizedPayload, dce.Message);
                }
            }
        }

        public override void Dispose()
        {
            if (!this.isDisposed)
            {
                this.authKeyHashFunction.Dispose();
                this.isDisposed = true;
            }
        }

        private static string NormalizeAuthorizationPayload(string input)
        {
            const int expansionBuffer = 12;
            StringBuilder builder = new StringBuilder(input.Length + expansionBuffer);
            for (int i = 0; i < input.Length; i++)
            {
                switch (input[i])
                {
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '/':
                        builder.Append("\\/");
                        break;
                    default:
                        builder.Append(input[i]);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
