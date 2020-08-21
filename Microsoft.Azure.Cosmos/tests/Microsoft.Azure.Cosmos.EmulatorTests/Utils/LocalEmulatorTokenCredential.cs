//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using IdentityModel.Tokens;

    public class LocalEmulatorTokenCredential : TokenCredential
    {
        private string masterKey;
        private readonly Action<TokenRequestContext, CancellationToken> GetTokenCallback;

        internal LocalEmulatorTokenCredential(string masterKey = null,
            Action<TokenRequestContext, CancellationToken> getTokenCallback = null)
        {
            this.masterKey = masterKey;
            this.GetTokenCallback = getTokenCallback;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return this.GetAccessToken(requestContext, cancellationToken);
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(this.GetAccessToken(requestContext, cancellationToken));
        }

        private AccessToken GetAccessToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            this.GetTokenCallback(
                requestContext,
                cancellationToken);

            DateTimeOffset dateTimeOffsetStart = DateTimeOffset.UtcNow;
            DateTimeOffset dateTimeOffsetExpiration = dateTimeOffsetStart.AddHours(1);

            string nbfValue = dateTimeOffsetStart.ToUnixTimeSeconds().ToString();
            string expValue = dateTimeOffsetExpiration.ToUnixTimeSeconds().ToString();

            string header = @"{
                ""alg"":""RS256"",
                ""kid"":""x_9KSusKU5YcHf4"",
                ""typ"":""JWT""
            }";

            string payload = @"{
                ""oid"":""96313034-4739-43cb-93cd-74193adbe5b6"",
                ""scp"":""user_impersonation"",
                ""groups"":[
                    ""7ce1d003-4cb3-4879-b7c5-74062a35c66e"",
                    ""e99ff30c-c229-4c67-ab29-30a6aebc3e58"",
                    ""5549bb62-c77b-4305-bda9-9ec66b85d9e4"",
                    ""c44fd685-5c58-452c-aaf7-13ce75184f65"",
                    ""be895215-eab5-43b7-9536-9ef8fe130330""
                ],
                ""nbf"":" + nbfValue + @",
                ""exp"":" + expValue + @",
                ""iat"":1596592335,
                ""iss"":""https://sts.fake-issuer.net/7b1999a1-dfd7-440e-8204-00170979b984"",
                ""aud"":""https://localhost.localhost""
            }";

            string headerBase64 = Base64UrlEncoder.Encode(header);
            string payloadBase64 = Base64UrlEncoder.Encode(payload);

            string token = headerBase64 + "." + payloadBase64 + "." + Base64UrlEncoder.Encode(this.masterKey);

            return new AccessToken(token, dateTimeOffsetExpiration);
        }
    }
}
