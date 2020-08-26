//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Utils
{
    using System;
    using System.Net;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class DNSHelper
    {
        public static Uri GetResolvedUri(string endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri endpointAsUri))
            {
                return null;
            }

            IPHostEntry hostEntry = Dns.GetHostEntry(endpointAsUri.Host);
            if (hostEntry.AddressList.Length == 0)
            {
                return endpointAsUri;
            }

            try
            {
                return new Uri($"{endpointAsUri.Scheme}{Uri.SchemeDelimiter}{hostEntry.AddressList[hostEntry.AddressList.Length - 1]}:{endpointAsUri.Port}/");
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceInformation($"endpoint:{endpoint}. endpointAsUri:{endpointAsUri}");
                Extensions.TraceException(ex);
                return endpointAsUri;
            }
        }
    }
}
