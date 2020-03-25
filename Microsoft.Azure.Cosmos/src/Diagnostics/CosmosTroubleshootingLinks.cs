//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosTroubleshootingLinks
    {
        private static readonly IReadOnlyDictionary<(int, int), CosmosTroubleshootingLinks> StatusCodeToLink;

        internal string Link { get; }
        internal int StatusCode { get; }
        internal int SubStatusCode { get; }
        internal bool IsServiceException { get; }

        private CosmosTroubleshootingLinks(
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

        private void AddToDictionary(Dictionary<(int, int), CosmosTroubleshootingLinks> dictionary)
        {
            dictionary.Add((this.StatusCode, this.SubStatusCode), this);
        }

        static CosmosTroubleshootingLinks()
        {
            Dictionary<(int, int), CosmosTroubleshootingLinks> linkMap = new Dictionary<(int, int), CosmosTroubleshootingLinks>();
            NotFound.AddToDictionary(linkMap);
            RequestRateTooLarge.AddToDictionary(linkMap);
            NotModified.AddToDictionary(linkMap);
            ClientTransportRequestTimeout.AddToDictionary(linkMap);
            ServiceTransportRequestTimeout.AddToDictionary(linkMap);
            TransportExceptionHighCpu.AddToDictionary(linkMap);

            CosmosTroubleshootingLinks.StatusCodeToLink = linkMap;
        }

        internal static bool TryGetTroubleshootingLinks(
            CosmosException cosmosException,
            out CosmosTroubleshootingLinks troubleshootingLink)
        {
            if (TryGetTransportException(cosmosException, out troubleshootingLink))
            {
                return true;
            }

            return CosmosTroubleshootingLinks.StatusCodeToLink.TryGetValue(
                ((int)cosmosException.StatusCode, cosmosException.SubStatusCode),
                out troubleshootingLink);
        }

        private static bool TryGetTransportException(CosmosException exception, out CosmosTroubleshootingLinks troubleshootingLink)
        {
            Exception innerException = exception.InnerException;
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

        private static readonly CosmosTroubleshootingLinks NotFound = new CosmosTroubleshootingLinks(
            statusCode: (int)HttpStatusCode.NotFound,
            subStatusCode: default,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgNotFound");

        private static readonly CosmosTroubleshootingLinks RequestRateTooLarge = new CosmosTroubleshootingLinks(
            statusCode: 429,
            subStatusCode: 3200,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgRequestRateTooLarge");

        private static readonly CosmosTroubleshootingLinks NotModified = new CosmosTroubleshootingLinks(
            statusCode: (int)HttpStatusCode.NotModified,
            subStatusCode: default,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgNotModified");

        private static readonly CosmosTroubleshootingLinks ClientTransportRequestTimeout = new CosmosTroubleshootingLinks(
            statusCode: (int)HttpStatusCode.RequestTimeout,
            subStatusCode: 8000,
            isServiceException: false,
            link: "https://aka.ms/CosmosTsgClientTransportRequestTimeout");

        private static readonly CosmosTroubleshootingLinks ServiceTransportRequestTimeout = new CosmosTroubleshootingLinks(
            statusCode: (int)HttpStatusCode.RequestTimeout,
            subStatusCode: 9000,
            isServiceException: true,
            link: "https://aka.ms/CosmosTsgServiceTransportRequestTimeout");

        private static readonly CosmosTroubleshootingLinks TransportExceptionHighCpu = new CosmosTroubleshootingLinks(
            statusCode: (int)HttpStatusCode.ServiceUnavailable,
            subStatusCode: 9001,
            isServiceException: false,
            link: "https://aka.ms/CosmosTsgTransportExceptionHighCpu");
    }
}