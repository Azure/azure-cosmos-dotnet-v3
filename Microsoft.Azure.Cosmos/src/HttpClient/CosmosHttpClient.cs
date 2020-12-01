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

    internal abstract class CosmosHttpClient : IDisposable
    {
        public abstract HttpMessageHandler HttpMessageHandler { get; }

        public enum TimeoutPolicy
        {
            Standard,
            ControlPlaneGet,
            ControlPlaneHotPath,
        }

        public static TimeoutPolicy GetTimeoutPolicy(
            DocumentServiceRequest documentServiceRequest)
        {
            if (documentServiceRequest.ResourceType == ResourceType.Document
                && documentServiceRequest.OperationType == OperationType.QueryPlan)
            {
                return TimeoutPolicy.ControlPlaneHotPath;
            }

            if (documentServiceRequest.ResourceType == ResourceType.PartitionKeyRange)
            {
                return TimeoutPolicy.ControlPlaneHotPath;
            }

            return TimeoutPolicy.Standard;
        }

        public abstract Task<HttpResponseMessage> GetAsync(
            Uri uri,
            INameValueCollection additionalHeaders,
            ResourceType resourceType,
            TimeoutPolicy timeoutPolicy,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken);

        public abstract Task<HttpResponseMessage> SendHttpAsync(
            Func<ValueTask<HttpRequestMessage>> createRequestMessageAsync,
            ResourceType resourceType,
            TimeoutPolicy timeoutPolicy,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken);

        protected abstract void Dispose(bool disposing);

        public abstract void Dispose();
    }
}
