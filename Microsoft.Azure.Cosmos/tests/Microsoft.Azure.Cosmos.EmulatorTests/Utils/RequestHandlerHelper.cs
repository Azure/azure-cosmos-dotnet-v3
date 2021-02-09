//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class RequestHandlerHelper : RequestHandler
    {
        public Action<RequestMessage> UpdateRequestMessage = null;
        public Func<RequestMessage, ResponseMessage, ResponseMessage> CallBackOnResponse = null;
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            this.UpdateRequestMessage?.Invoke(request);
            ResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);
            if (this.CallBackOnResponse != null)
            {
                responseMessage = this.CallBackOnResponse.Invoke(request, responseMessage);
            }

            return responseMessage;
        }
    }
}
