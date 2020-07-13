//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;

    /// <summary>
    /// Default implementation of <see cref="IAADTokenProvider"/>.
    /// </summary>
    internal sealed class AADTokenProvider : IAADTokenProvider
    {
        private readonly string defaultResource;  // Key Vault Resource End Point
        private readonly ClientCertificateCredential clientCertificateCredential;
        private readonly TimeSpan retryInterval;
        private readonly int retryCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="AADTokenProvider"/> class.
        /// </summary>
        public AADTokenProvider(
            string defaultResource,
            ClientCertificateCredential clientCertificateCredential,
            TimeSpan retryInterval,
            int retryCount)
        {

            if (String.IsNullOrEmpty(defaultResource))
            {
                throw new ArgumentException("defaultResource empty");
            }

            this.defaultResource = defaultResource;
            this.clientCertificateCredential = clientCertificateCredential;
            this.retryInterval = retryInterval;
            this.retryCount = retryCount;
        }

        /// <summary>
        /// Helper function to get an Access Token.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            /// keyvault URI
            String[] scopes = new String[] { this.defaultResource + "/.default" };
            String accessToken = string.Empty;
            int currentRetry = 0;

            for (;;)
            {
                try
                {
                    AccessToken at = await this.clientCertificateCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken);
                    accessToken = at.Token.ToString();
                    break;
                }
                catch (Exception exception)
                {
                    if ((exception is AuthenticationFailedException || WebExceptionUtility.IsWebExceptionRetriable(exception)) && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("GetAccessTokenAsync Caught ex {0},current retry count {1},retry limit {2} retry interval {3}", exception, currentRetry, this.retryCount, this.retryInterval);

                        currentRetry++;
                        // verify,retry with a delay.
                        if (currentRetry > this.retryCount)
                        {
                            //rethrow. 
                            throw;
                        }
                    }
                }
                // Wait to retry the operation.               
                await Task.Delay(this.retryInterval);
            }
            return accessToken;
        }
    }
}