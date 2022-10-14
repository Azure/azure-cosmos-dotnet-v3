//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class HttpClientHandlerHelper : DelegatingHandler
    {
        public HttpClientHandlerHelper() : base(new HttpClientHandler())
        {
        }

        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> RequestCallBack { get; set; }

        public Func<HttpResponseMessage, Task<HttpResponseMessage>> ResponseIntercepter { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage httpResponse = null;
            if (this.RequestCallBack != null)
            {
                Task<HttpResponseMessage> response = this.RequestCallBack(request, cancellationToken);
                if(response != null)
                {
                    httpResponse = await response;
                    if (httpResponse != null)
                    {
                        if (this.ResponseIntercepter != null)
                        {
                            httpResponse = await this.ResponseIntercepter(httpResponse);
                        }
                        return httpResponse;
                    }
                }
            }

            httpResponse =  await base.SendAsync(request, cancellationToken);
            if (this.ResponseIntercepter != null)
            {
                httpResponse = await this.ResponseIntercepter(httpResponse);
            }

            return httpResponse;
        }
    }
}
