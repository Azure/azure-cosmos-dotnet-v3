//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    [Serializable]
    internal sealed class ServiceUnavailableException : DocumentClientException
    {         
        public static ServiceUnavailableException Create(SubStatusCodes? subStatusCode, Exception innerException = null, HttpResponseHeaders headers = null, Uri requestUri = null)
        {
            return new ServiceUnavailableException(GetExceptionMessage(subStatusCode), innerException, headers, subStatusCode);
        }

        public static ServiceUnavailableException Create(INameValueCollection headers, SubStatusCodes? subStatusCode, Uri requestUri = null)
        {
            return new ServiceUnavailableException(GetExceptionMessage(subStatusCode), headers, subStatusCode, requestUri);
        }

        public ServiceUnavailableException()
            : this(RMResources.ServiceUnavailable, null, null, SubStatusCodes.Unknown)
        {
        }

        public ServiceUnavailableException(string message)
            : this(message, null, null, SubStatusCodes.Unknown)
        {
        }
        public ServiceUnavailableException(string message, SubStatusCodes subStatusCode, Uri requestUri = null)
            : this(message, null, null, subStatusCode, requestUri)
        {
        }

        public ServiceUnavailableException(string message, Exception innerException, SubStatusCodes subStatusCode, Uri requestUri = null)
            : this(message, innerException, null, subStatusCode, requestUri)
        {
        }

        public ServiceUnavailableException(string message, HttpResponseHeaders headers, SubStatusCodes? subStatusCode, Uri requestUri = null)
            : this(message, null, headers, subStatusCode, requestUri)
        {
        }

        public ServiceUnavailableException(Exception innerException, SubStatusCodes subStatusCode)
            : this(RMResources.ServiceUnavailable, innerException, null, subStatusCode)
        {
        }
        public ServiceUnavailableException(string message, INameValueCollection headers, SubStatusCodes? subStatusCode, Uri requestUri = null)
            : base(message, null, headers, HttpStatusCode.ServiceUnavailable, subStatusCode, requestUri)
        {
            SetDescription();
        }

        public ServiceUnavailableException(string message,
            Exception innerException,
            HttpResponseHeaders headers,
            SubStatusCodes? subStatusCode,            
            Uri requestUri = null)
            : base(message, innerException, headers, HttpStatusCode.ServiceUnavailable, requestUri, subStatusCode)
        {
            SetDescription();
        }
       
#if !NETSTANDARD16
        private ServiceUnavailableException(SerializationInfo info, StreamingContext context)
            : base(info, context, HttpStatusCode.ServiceUnavailable)
        {
            SetDescription();
        }
#endif

        private void SetDescription()
        {
            this.StatusDescription = HttpConstants.HttpStatusDescriptions.ServiceUnavailable;
        }

        private static string GetExceptionMessage(SubStatusCodes? subStatusCode)
        {
            switch (subStatusCode)
            {
                case SubStatusCodes.TransportGenerated410:
                    return RMResources.TransportGenerated410;
                case SubStatusCodes.TimeoutGenerated410:
                    return RMResources.TimeoutGenerated410;
                case SubStatusCodes.Client_CPUOverload:
                    return RMResources.Client_CPUOverload;
                case SubStatusCodes.Client_ThreadStarvation:
                    return RMResources.Client_ThreadStarvation;
                case SubStatusCodes.TransportGenerated503:
                    return RMResources.TransportGenerated503;
                case SubStatusCodes.ServerGenerated410:
                    return RMResources.ServerGenerated410;
                case SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet:
                    return RMResources.Server_GlobalStrongWriteBarrierNotMet;
                case SubStatusCodes.Server_ReadQuorumNotMet:
                    return RMResources.Server_ReadQuorumNotMet;
                case SubStatusCodes.ServerGenerated503:
                    return RMResources.ServerGenerated503;
                case SubStatusCodes.Server_NameCacheIsStaleExceededRetryLimit:
                    return RMResources.Server_NameCacheIsStaleExceededRetryLimit;
                case SubStatusCodes.Server_PartitionKeyRangeGoneExceededRetryLimit:
                    return RMResources.Server_PartitionKeyRangeGoneExceededRetryLimit;
                case SubStatusCodes.Server_CompletingSplitExceededRetryLimit:
                    return RMResources.Server_CompletingSplitExceededRetryLimit;
                case SubStatusCodes.Server_CompletingPartitionMigrationExceededRetryLimit:
                    return RMResources.Server_CompletingPartitionMigrationExceededRetryLimit;
                case SubStatusCodes.Server_NoValidStoreResponse:
                    return RMResources.Server_NoValidStoreResponse;
                case SubStatusCodes.Channel_Closed:
                    return RMResources.ChannelClosed;
                default:
                    return RMResources.ServiceUnavailable;
            }
        }
    }
}
