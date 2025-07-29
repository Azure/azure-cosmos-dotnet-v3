//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using Microsoft.Azure.Documents.Rntbd;
    using System;
#pragma warning disable SA1210
    using System.Threading.Tasks;
#pragma warning restore SA1210
    using System.Net.Http;
    using System.Threading;

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

#pragma warning disable CS1572 // XML comment has a param tag, but there is no parameter by that name
#pragma warning disable SA1612 // Element parameter documentation should match element parameters
#pragma warning disable CS1573
#pragma warning disable SA1616
        /// <summary>
        /// Used to inject faults on request call for GatewayCalls
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public Task<(bool, HttpResponseMessage)> OnHttpRequestCallAsync(DocumentServiceRequest request, CancellationToken token = default);
#pragma warning restore SA1612 // Element parameter documentation should match element parameters
#pragma warning restore CS1572 // XML comment has a param tag, but there is no parameter by that name
#pragma warning disable CS1573
#pragma warning disable SA1616

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
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row


#pragma warning disable CS1572 // XML comment has a param tag, but there is no parameter by that name
#pragma warning disable SA1612 // Element parameter documentation should match element parameters
#pragma warning disable CS1573
        /// <summary>
        /// Used to inject faults before connection writes for gateway
        /// </summary>
        /// <param name="args"></param>
        public Task OnBeforeHttpSendAsync(DocumentServiceRequest request, CancellationToken token = default);
#pragma warning restore SA1507 // Code should not contain multiple blank lines in a row
#pragma warning restore SA1612 // Element parameter documentation should match element parameters
#pragma warning restore CS1572 // XML comment has a param tag, but there is no parameter by that name
#pragma warning disable CS1573

        /// <summary>
        /// Used to inject faults after connection writes
        /// </summary>
        /// <param name="args"></param>
        public Task OnAfterConnectionWriteAsync(ChannelCallArguments args);
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row


#pragma warning disable CS1572 // XML comment has a param tag, but there is no parameter by that name
#pragma warning disable SA1612 // Element parameter documentation should match element parameters
#pragma warning disable CS1573
        /// <summary>
        /// Used to inject faults after connection writes for gateway
        /// </summary>
        /// <param name="args"></param>
        public Task OnAfterHttpSendAsync(DocumentServiceRequest request, CancellationToken token = default);
#pragma warning restore SA1507 // Code should not contain multiple blank lines in a row
#pragma warning restore SA1612 // Element parameter documentation should match element parameters
#pragma warning restore CS1572 // XML comment has a param tag, but there is no parameter by that name
#pragma warning disable CS1573

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId);
    }
}