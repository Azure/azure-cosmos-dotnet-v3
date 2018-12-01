using System;
using System.Net;
using Microsoft.Azure.Cosmos.Internal;

namespace Microsoft.Azure.Cosmos.Utils
{
    internal static class DNSHelper
    {
        public static Uri GetResolvedUri(string endpoint)
        {
            Uri endpointAsUri;
            if(!Uri.TryCreate(endpoint, UriKind.Absolute, out endpointAsUri))
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
                DefaultTrace.TraceException(ex);
                return endpointAsUri;
            }
        }
    }
}
