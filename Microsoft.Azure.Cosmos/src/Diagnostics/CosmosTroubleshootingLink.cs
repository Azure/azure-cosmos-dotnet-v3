//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosTroubleshootingLink
    {
        private static readonly IReadOnlyDictionary<(int, int), CosmosTroubleshootingLink> StatusCodeToLink;

        internal string Link { get; }
        internal int StatusCode { get; }
        internal int SubStatusCode { get; }
        internal bool IsServiceException { get; }

        private CosmosTroubleshootingLink(
            int statusCode,
            int subStatusCode,
            bool isServiceException,
            string link)
        {
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.IsServiceException = isServiceException;
            this.Link = link ?? throw new ArgumentNullException(nameof(link));
        }

        private void AddToDictionary(Dictionary<(int, int), CosmosTroubleshootingLink> dictionary)
        {
            dictionary.Add((this.StatusCode, this.SubStatusCode), this);
        }

        static CosmosTroubleshootingLink()
        {
            Dictionary<(int, int), CosmosTroubleshootingLink> linkMap = new Dictionary<(int, int), CosmosTroubleshootingLink>();
            NotFound.AddToDictionary(linkMap);
            RequestRateTooLarge.AddToDictionary(linkMap);
            NotModified.AddToDictionary(linkMap);
            ClientTransportRequestTimeout.AddToDictionary(linkMap);
            ServiceTransportRequestTimeout.AddToDictionary(linkMap);
            TransportExceptionHighCpu.AddToDictionary(linkMap);

            CosmosTroubleshootingLink.StatusCodeToLink = linkMap;
        }

        internal static bool TryGetTroubleshootingLinks(
            int statusCodes,
            int subStatusCode,
            Exception innerException,
            out CosmosTroubleshootingLink troubleshootingLink)
        {
            if (TryGetTransportException(innerException, out troubleshootingLink))
            {
                return true;
            }

            return CosmosTroubleshootingLink.StatusCodeToLink.TryGetValue(
                (statusCodes, subStatusCode),
                out troubleshootingLink);
        }

        private static bool TryGetTransportException(Exception innerException, out CosmosTroubleshootingLink troubleshootingLink)
        {
            while (innerException != null)
            {
                if (innerException is TransportException transportException)
                {
                    if (transportException.IsClientCpuOverloaded)
                    {
                        troubleshootingLink = TransportExceptionHighCpu;
                        return true;
                    }

                    if (TransportException.IsTimeout(transportException.ErrorCode))
                    {
                        if (transportException.UserRequestSent)
                        {
                            troubleshootingLink = ServiceTransportRequestTimeout;
                            return true;
                        }
                        else
                        {
                            troubleshootingLink = ClientTransportRequestTimeout;
                            return true;
                        }
                    }
                    
                }
                else
                {
                    innerException = innerException.InnerException;
                }
            }

            troubleshootingLink = null;
            return false;
        }

        private static readonly CosmosTroubleshootingLink NotFound = new CosmosTroubleshootingLink(
            statusCode: (int)HttpStatusCode.NotFound,
            subStatusCode: default,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgNotFound");

        private static readonly CosmosTroubleshootingLink RequestRateTooLarge = new CosmosTroubleshootingLink(
            statusCode: 429,
            subStatusCode: 3200,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgRequestRateTooLarge");

        private static readonly CosmosTroubleshootingLink NotModified = new CosmosTroubleshootingLink(
            statusCode: (int)HttpStatusCode.NotModified,
            subStatusCode: default,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgNotModified");

        private static readonly CosmosTroubleshootingLink ClientTransportRequestTimeout = new CosmosTroubleshootingLink(
            statusCode: (int)HttpStatusCode.RequestTimeout,
            subStatusCode: 8000,
            isServiceException: false,
            link: "https://aka.ms/CosmosTsgClientTransportRequestTimeout");

        private static readonly CosmosTroubleshootingLink ServiceTransportRequestTimeout = new CosmosTroubleshootingLink(
            statusCode: (int)HttpStatusCode.RequestTimeout,
            subStatusCode: 9000,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgServiceTransportRequestTimeout");

        private static readonly CosmosTroubleshootingLink TransportExceptionHighCpu = new CosmosTroubleshootingLink(
            statusCode: (int)HttpStatusCode.ServiceUnavailable,
            subStatusCode: 9001,
            isServiceException: false,
            link: "https://aka.ms/CosmosTsgTransportExceptionHighCpu");
    }
}