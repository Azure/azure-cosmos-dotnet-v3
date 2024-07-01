// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ThinClient
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// TODO WOLOLO
    /// </summary>
    public sealed class ThinClientProxySdkHandler : RequestHandler
    {
        /// <summary>
        /// TODO wololo
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>TODO wololo2</returns>
        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            ProxyExtensions.AddProxyProperties(request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
