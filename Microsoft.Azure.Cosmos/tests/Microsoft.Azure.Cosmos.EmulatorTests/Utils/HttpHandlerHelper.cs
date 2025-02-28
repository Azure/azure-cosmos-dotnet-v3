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

        public Func<HttpResponseMessage, HttpRequestMessage, Task<HttpResponseMessage>> ResponseIntercepter { get; set; }

        public Action<HttpRequestMessage, Exception> ExceptionIntercepter { get; set; }

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
                            httpResponse = await this.ResponseIntercepter(httpResponse, request);
                        }
                        return httpResponse;
                    }
                }
            }

            try
            {
                httpResponse = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) {

                if (this.ExceptionIntercepter == null)
                {
                    throw;
                }
                this.ExceptionIntercepter.Invoke(request, ex);

                throw; // Anyway throw this exception
            }
           
            if (this.ResponseIntercepter != null)
            {
                httpResponse = await this.ResponseIntercepter(httpResponse, request);
            }

            return httpResponse;
        }
    }
}
