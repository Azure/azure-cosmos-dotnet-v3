﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents.Collections;

    [Serializable]
    internal sealed class ServiceUnavailableException : DocumentClientException
    {         
        public static ServiceUnavailableException Create(SubStatusCodes? subStatusCode, Exception innerException = null, HttpResponseHeaders headers = null, Uri requestUri = null)
        {
            subStatusCode ??= GetSubStatus(headers);
            return new ServiceUnavailableException(GetExceptionMessage(subStatusCode), innerException, headers, subStatusCode);
        }

        public static ServiceUnavailableException Create(INameValueCollection headers, SubStatusCodes? subStatusCode, Uri requestUri = null)
        {
            subStatusCode ??= GetSubStatus(headers);
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
        
        public ServiceUnavailableException(string message, bool rawErrorMessageOnly)
            : this(message, null, null, SubStatusCodes.Unknown, rawErrorMessageOnly: rawErrorMessageOnly)
        {
        }
        
        public ServiceUnavailableException(string message, SubStatusCodes subStatusCode, Uri requestUri = null, bool rawErrorMessageOnly = false)
            : this(message, null, null, subStatusCode, requestUri, rawErrorMessageOnly: rawErrorMessageOnly)
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
            Uri requestUri = null,
            bool rawErrorMessageOnly = false)
            : base(message, innerException, headers, HttpStatusCode.ServiceUnavailable, requestUri, subStatusCode, rawErrorMessageOnly: rawErrorMessageOnly)
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
                case SubStatusCodes.Server_NRegionCommitWriteBarrierNotMet:
                    return RMResources.Server_NRegionCommitWriteBarrierNotMet;
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

        static internal SubStatusCodes GetSubStatus(INameValueCollection responseHeaders)
        {
            SubStatusCodes? substatus = SubStatusCodes.Unknown;

            string valueSubStatus = responseHeaders.Get(WFConstants.BackendHeaders.SubStatus);
            if (!string.IsNullOrEmpty(valueSubStatus))
            {
                uint nSubStatus;
                if (uint.TryParse(valueSubStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
                {
                    substatus = (SubStatusCodes)nSubStatus;
                }
            }

            return substatus.Value;
        }

        static internal SubStatusCodes GetSubStatus(HttpResponseHeaders responseHeaders)
        {
            if (responseHeaders != null)
            {
                IEnumerable<string> substatusCodes;
                if (responseHeaders.TryGetValues(WFConstants.BackendHeaders.SubStatus, out substatusCodes))
                {
                    uint nSubStatus;
                    if (uint.TryParse(substatusCodes.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out nSubStatus))
                    {
                       return (SubStatusCodes)nSubStatus;
                    }
                }
            }

            return SubStatusCodes.Unknown;
        }
    }
}
