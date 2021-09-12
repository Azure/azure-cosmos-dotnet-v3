//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class RequestChargeHandlerHelper : RequestHandler
    {
        public double TotalRequestCharges { get; set;}

        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            ResponseMessage response = await base.SendAsync(request, cancellationToken);
            this.TotalRequestCharges += response.Headers.RequestCharge;
            return response;
        }
    }
}
