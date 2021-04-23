//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class HttpHandlerMetaDataValidator : DelegatingHandler
    {
        private readonly ConcurrentDictionary<Uri, int> numOfPkRangeCachePerCollection = new ConcurrentDictionary<Uri, int>();
        public HttpHandlerMetaDataValidator() : base(new HttpClientHandler())
        {
        }

        public Action<HttpRequestMessage, HttpResponseMessage> RequestCallBack { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage httpResponseMessage = await base.SendAsync(request, cancellationToken); ;
            if (request.Method == HttpMethod.Get)
            {
                if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotModified
                    && request.RequestUri.ToString().EndsWith("/pkranges"))
                {
                    int numOfPkRangeCachCalls = this.numOfPkRangeCachePerCollection.AddOrUpdate(
                        request.RequestUri,
                        0,
                       (uri, original) => original + 1);

                    if (numOfPkRangeCachCalls >= 1)
                    {
                        Console.WriteLine("The partition key range cache is getting refreshed unnecessarily.");
                        throw new Exception("The partition key range cache is getting refreshed unnecessarily.");
                    }
                }
            }

            this.RequestCallBack?.Invoke(request, httpResponseMessage);
            return httpResponseMessage;
        }
    }
}
