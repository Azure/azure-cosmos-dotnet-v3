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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(this.RequestCallBack != null)
            {
                Task<HttpResponseMessage> response = this.RequestCallBack(request, cancellationToken);
                if(response != null)
                {
                    return response;
                }
            }
            
            return base.SendAsync(request, cancellationToken);
        }
    }
}
