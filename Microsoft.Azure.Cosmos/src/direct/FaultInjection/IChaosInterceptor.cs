//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Interface for Chaos Interceptor
    /// </summary>
    internal interface IChaosInterceptor
    {
        /// <summary>
        /// Used to inject faults on request call
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public Task<(bool, StoreResponse)> OnRequestCallAsync(ChannelCallArguments args);

        /// <summary>
        /// Used to inject faults on request call for GatewayCalls
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task<(bool, HttpResponseMessage)> OnHttpRequestCallAsync(DocumentServiceRequest request, CancellationToken token = default);

        /// <summary>
        /// Used to inject faults on channel open
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="connectionCorrelationId"></param>
        /// <param name="serverUri"></param>
        /// <param name="openingRequest"></param>
        /// <param name="channel"></param>
        public Task OnChannelOpenAsync(Guid activityId, Guid connectionCorrelationId, Uri serverUri, DocumentServiceRequest openingRequest, Channel channel);

        /// <summary>
        /// Used to update internal active channel store on channel close
        /// </summary>
        /// <param name="connectionCorrelationId"></param>
        public void OnChannelDispose(Guid connectionCorrelationId);

        /// <summary>
        /// Used to inject faults before connection writes
        /// </summary>
        /// <param name="args"></param>
        public Task OnBeforeConnectionWriteAsync(ChannelCallArguments args);

        /// <summary>
        /// Used to inject faults before connection writes for gateway
        /// </summary>
        public Task OnBeforeHttpSendAsync(DocumentServiceRequest request, CancellationToken token = default);

        /// <summary>
        /// Used to inject faults after connection writes
        /// </summary>
        /// <param name="args"></param>
        public Task OnAfterConnectionWriteAsync(ChannelCallArguments args);

        /// <summary>
        /// Used to inject faults after connection writes for gateway
        /// </summary>
        public Task OnAfterHttpSendAsync(DocumentServiceRequest request, CancellationToken token = default);

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId);
    }
}
