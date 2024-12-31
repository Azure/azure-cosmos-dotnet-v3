//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;

    /// <summary>
    /// https://azure.microsoft.com/en-us/regions/
    /// </summary>
    internal static class LocationHelper
    {
        private static readonly char[] hostSeparators = new char[] { '.' };

        /// <summary>
        /// For example, for https://contoso.documents.azure.com:443/ and "West US", this will return https://contoso-westus.documents.azure.com:443/
        /// NOTE: This ONLY called by client first boot when the input endpoint is not available.
        /// </summary>
        internal static Uri GetLocationEndpoint(Uri serviceEndpoint, string location)
        {
            UriBuilder builder = new UriBuilder(serviceEndpoint);

            // Split the host into 2 parts seperated by '.'
            // For example, "contoso.documents.azure.com" is separated into "contoso" and "documents.azure.com"
            // If the host doesn't contains '.', this will return the host as is, as the only element
            string[] hostParts = builder.Host.Split(hostSeparators, 2);

            if (hostParts.Length != 0)
            {
                // hostParts[0] will be the global account name
                hostParts[0] = hostParts[0] + "-" + location.DataCenterToUriPostfix();

                // if hostParts has only one element, '.' is not included in the returned string
                builder.Host = string.Join(".", hostParts);
            }

            return builder.Uri;
        }

        private static string DataCenterToUriPostfix(this string datacenter)
        {
            return datacenter.Replace(" ", String.Empty);
        }
    }
}