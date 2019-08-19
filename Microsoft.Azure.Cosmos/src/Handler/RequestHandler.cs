//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;

    /// <summary>
    /// Abstraction which allows defining of custom message handlers.
    /// </summary>
    /// <remarks>
    /// Custom implementations are required to be stateless.
    /// </remarks>
    public abstract class RequestHandler
    {
        /// <summary>
        /// Defines a next handler to be called in the chain.
        /// </summary>
        public RequestHandler InnerHandler { get; set; }

        /// <summary>
        /// Processes the current <see cref="RequestMessage"/> in the current handler and sends the current <see cref="RequestMessage"/> to the next handler in the chain.
        /// </summary>
        /// <param name="request"><see cref="RequestMessage"/> received by the handler.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> received by the handler.</param>
        /// <returns>An instance of <see cref="ResponseMessage"/>.</returns>
        public virtual Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (this.InnerHandler == null)
            {
                throw new ArgumentNullException(nameof(this.InnerHandler));
            }

            updateCustomerHandlerStatistics(request);
            return this.InnerHandler.SendAsync(request, cancellationToken);
        }

        private void updateCustomerHandlerStatistics(RequestMessage request)
        {
            if (request.requestDiagnosticContext.handlerRequestStartTime != null &&
                    request.requestDiagnosticContext.handlerRequestEndTime == null)
            {
                request.requestDiagnosticContext.handlerRequestEndTime = DateTime.UtcNow;
                request.requestDiagnosticContext.customHandlerLatency.Add(Tuple.Create(
                    request.requestDiagnosticContext.currentHandlerName,
                    (request.requestDiagnosticContext.handlerRequestEndTime.Value - request.requestDiagnosticContext.handlerRequestStartTime.Value)));
                request.requestDiagnosticContext.handlerRequestStartTime = null;
                request.requestDiagnosticContext.handlerRequestEndTime = null;
            }

            //  if (!this.InnerHandler.GetType().Namespace.Equals(typeof(RequestInvokerHandler).Namespace))
            //{
            request.requestDiagnosticContext.handlerRequestStartTime = DateTime.UtcNow;
            request.requestDiagnosticContext.currentHandlerName = this.InnerHandler.GetType().Name;
            // }
        }
    }
}
