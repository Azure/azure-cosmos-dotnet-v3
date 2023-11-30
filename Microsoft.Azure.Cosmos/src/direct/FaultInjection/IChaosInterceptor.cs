//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.FaultInjection
{
    using Microsoft.Azure.Documents.Rntbd;
    using System;

    /// <summary>
    /// Interface for Chaos Interceptor
    /// </summary>
    internal interface IChaosInterceptor
    {
        public void ConfigureInterceptor(dynamic client, TimeSpan timeout);
        /// <summary>
        /// Used to inject faults on request call
        /// </summary>
        /// <param name="args"></param>
        /// <param name="faultyResponse"></param>
        /// <returns></returns>
        public bool OnRequestCall(ChannelCallArguments args, out StoreResponse faultyResponse);

        /// <summary>
        /// Used to inject faults on channel open
        /// </summary>
        /// <param name="activityId"></param>
        /// <param name="connectionCorrilationId"></param>
        /// <param name="serverUri"></param>
        /// <param name="openingRequest"></param>
        /// <param name="channel"></param>
        public void OnChannelOpen(Guid activityId, Guid connectionCorrilationId, Uri serverUri, DocumentServiceRequest openingRequest, Channel channel);

        /// <summary>
        /// Used to update internal active channel store on channel close
        /// </summary>
        /// <param name="connectionCorrelationId"></param>
        public void OnChannelDispose(Guid connectionCorrelationId);

        /// <summary>
        /// Used to inject faults before connection writes
        /// </summary>
        /// <param name="args"></param>
        public void OnBeforeConnectionWrite(ChannelCallArguments args);

        /// <summary>
        /// Used to inject faults after connection writes
        /// </summary>
        /// <param name="args"></param>
        public void OnAfterConnectionWrite(ChannelCallArguments args);

        /// <summary>
        /// Gets the fault injection rule id for the given activity id
        /// </summary>
        /// <param name="activityId"></param>
        /// <returns>the fault injection rule id</returns>
        public string GetFaultInjectionRuleId(Guid activityId);
    }
}
