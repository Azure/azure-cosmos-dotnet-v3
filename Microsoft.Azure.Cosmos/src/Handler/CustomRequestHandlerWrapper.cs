//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Dynamic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This class is used to track the time any custom handlers take.
    /// </summary>
    internal class CustomRequestHandlerWrapper : RequestHandler
    {
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            using (request.DiagnosticsCore.CreateScope("CustomHandlerWrapper"))
            {
                return await base.SendAsync(request, cancellationToken);
            } 
        }
    }
}
