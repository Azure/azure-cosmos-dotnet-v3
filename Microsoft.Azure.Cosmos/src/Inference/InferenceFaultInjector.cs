//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection;

    /// <summary>
    /// Encapsulates fault injection logic for the inference service,
    /// hiding the synthetic DocumentServiceRequest creation and FaultInjectionId management
    /// from the InferenceService class.
    /// </summary>
    internal sealed class InferenceFaultInjector
    {
        private const string FaultInjectionIdKey = "FaultInjectionId";

        private readonly IChaosInterceptor chaosInterceptor;
        private readonly Uri inferenceEndpoint;

        // Per-call fault injection ID, set at the start of each request cycle
        private Guid currentFaultInjectionId;

        public InferenceFaultInjector(IChaosInterceptor chaosInterceptor, Uri inferenceEndpoint)
        {
            this.chaosInterceptor = chaosInterceptor ?? throw new ArgumentNullException(nameof(chaosInterceptor));
            this.inferenceEndpoint = inferenceEndpoint ?? throw new ArgumentNullException(nameof(inferenceEndpoint));
        }

        /// <summary>
        /// Attempts to inject a fault before sending the real HTTP request.
        /// Creates a synthetic DocumentServiceRequest internally for fault injection rule matching.
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source for the request.</param>
        /// <param name="headers">The request headers (auth, user-agent) used for condition matching. Not mutated.</param>
        /// <param name="requestMessage">The HTTP request message (attached to fault response if injected).</param>
        /// <returns>A tuple indicating if a fault was injected and the fault response message.</returns>
        public async Task<(bool hasFault, HttpResponseMessage response)> TryInjectFaultAsync(
            CancellationTokenSource cancellationTokenSource,
            INameValueCollection headers,
            HttpRequestMessage requestMessage)
        {
            CancellationToken fiToken = cancellationTokenSource.Token;
            fiToken.ThrowIfCancellationRequested();

            this.currentFaultInjectionId = Guid.NewGuid();

            using DocumentServiceRequest documentServiceRequest = this.CreateSyntheticRequest(headers);

            await this.chaosInterceptor.OnBeforeHttpSendAsync(documentServiceRequest, fiToken);

            (bool hasFault, HttpResponseMessage fiResponseMessage) =
                await this.chaosInterceptor.OnHttpRequestCallAsync(documentServiceRequest, fiToken);

            if (hasFault)
            {
                fiResponseMessage.RequestMessage = requestMessage;
            }

            return (hasFault, fiResponseMessage);
        }

        /// <summary>
        /// Attempts to inject a response delay after the real HTTP request completes.
        /// Uses the same FaultInjectionId as the preceding TryInjectFaultAsync call.
        /// </summary>
        /// <param name="headers">The request headers used for condition matching. Not mutated.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task TryInjectResponseDelayAsync(
            INameValueCollection headers,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using DocumentServiceRequest documentServiceRequest = this.CreateSyntheticRequest(headers);

            await this.chaosInterceptor.OnAfterHttpSendAsync(documentServiceRequest, cancellationToken);
        }

        /// <summary>
        /// Creates a synthetic DocumentServiceRequest with the FaultInjectionId and
        /// copies the provided headers for fault injection condition matching.
        /// </summary>
        private DocumentServiceRequest CreateSyntheticRequest(INameValueCollection headers)
        {
            DocumentServiceRequest documentServiceRequest = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.AadToken);

            documentServiceRequest.Headers.Set(
                InferenceFaultInjector.FaultInjectionIdKey,
                this.currentFaultInjectionId.ToString());

            foreach (string key in headers.AllKeys())
            {
                documentServiceRequest.Headers.Set(key, headers[key]);
            }

            documentServiceRequest.RequestContext.RouteToLocation(this.inferenceEndpoint);

            return documentServiceRequest;
        }
    }
}
