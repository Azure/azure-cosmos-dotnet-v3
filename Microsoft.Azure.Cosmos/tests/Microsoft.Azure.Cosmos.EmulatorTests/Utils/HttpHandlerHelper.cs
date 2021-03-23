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

        public Func<HttpRequestMessage, HttpResponseMessage, HttpResponseMessage> ResponseCallBack { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(this.RequestCallBack != null)
            {
                Task<HttpResponseMessage> response = this.RequestCallBack(request, cancellationToken);
                if(response != null)
                {
                    return await response;
                }
            }
            
            HttpResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);

            if (this.ResponseCallBack != null)
            {
                HttpResponseMessage response = this.ResponseCallBack(request, responseMessage);
                if (response != null)
                {
                    return response;
                }
            }

            return responseMessage;
        }
    }
}
