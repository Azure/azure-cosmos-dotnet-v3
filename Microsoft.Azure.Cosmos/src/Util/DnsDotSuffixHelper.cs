//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper for DNS dot-suffix (FQDN trailing dot) resolution.
    /// Appending a trailing dot to a hostname signals the DNS resolver that the name is
    /// fully qualified (absolute) and must not be subject to search-domain expansion.
    /// This avoids multiple unnecessary DNS lookups on Kubernetes where the default
    /// ndots:5 configuration causes search-domain attempts for Cosmos DB endpoints.
    /// </summary>
    internal static class DnsDotSuffixHelper
    {
        /// <summary>
        /// Appends a trailing dot to the hostname to make it a fully qualified domain name (FQDN).
        /// Returns the hostname unchanged if it is null/empty, already ends with a dot,
        /// or is an IP address (IPv4 or IPv6).
        /// </summary>
        /// <param name="hostName">The hostname to convert to FQDN form.</param>
        /// <returns>The hostname with a trailing dot appended, or unchanged if not applicable.</returns>
        internal static string ToFqdnHostName(string hostName)
        {
            if (string.IsNullOrEmpty(hostName)
                || hostName.EndsWith(".")
                || IPAddress.TryParse(hostName, out _))
            {
                return hostName;
            }

            return hostName + ".";
        }

        /// <summary>
        /// Creates a DNS resolution function that appends a trailing dot to the hostname
        /// before resolving, to bypass Kubernetes ndots search-domain expansion.
        /// Intended for use with <c>StoreClientFactory.dnsResolutionFunction</c>.
        /// </summary>
        /// <returns>A function that resolves a dot-suffixed hostname to an <see cref="IPAddress"/>.</returns>
        internal static Func<string, Task<IPAddress>> CreateDnsResolutionFunction()
        {
            return async (string hostName) =>
            {
                string fqdnHost = DnsDotSuffixHelper.ToFqdnHostName(hostName);
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(fqdnHost);
                return addresses[0];
            };
        }
    }
}
