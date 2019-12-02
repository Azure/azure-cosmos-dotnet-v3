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

        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            this.UpdateRequestMessage?.Invoke(request);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
