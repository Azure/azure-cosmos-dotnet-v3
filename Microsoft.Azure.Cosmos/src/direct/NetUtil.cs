//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if !(NETSTANDARD15 || NETSTANDARD16)
    using System.Configuration;
#endif

    internal static class NetUtil
    {
        // IPv6 Service Tunnel destination prefix for PaasV1 is 2603:10e1:100:2/64
        private static readonly byte[] paasV1Prefix = new byte[] { 0x26, 0x03, 0x10, 0xe1, 0x01, 0x00, 0x00, 0x02 };

        // IPv6 Service Tunnel destination prefix for PaasV2 is ace:cab:deca::/48
        private static readonly byte[] paasV2Prefix = new byte[] { 0x0a, 0xce, 0x0c, 0xab, 0xde, 0xca };

        /// <summary>
        /// Get a single non-loopback (i.e., not 127.0.0.0/8)
        /// IP address of the local machine.
        /// </summary>
        /// <returns></returns>
        public static string GetNonLoopbackIpV4Address()
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                // We skip loopback, tunnel adapters etc ...
                if ((adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                    (adapter.OperationalStatus == OperationalStatus.Up))
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    foreach (IPAddressInformation ipAddress in properties.UnicastAddresses)
                    {
                        if (ipAddress.IsDnsEligible &&
                            (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork))
                        {
                            return ipAddress.Address.ToString();
                        }
                    }
                }
            }

            // If we reach here, we couldn't find a usable IP address.
            string message = "ERROR: Could not locate any usable IPv4 address";
            DefaultTrace.TraceCritical(message);
            throw new ConfigurationErrorsException(message);
        }

        /// <summary>
        /// Get a single non-loopback (i.e., not 127.0.0.0/8)
        /// IP address of the local machine.  Similar to GetNonLoopbackIpV4Address but allows
        /// non-dns eligible adapters
        /// </summary>
        /// <returns></returns>
        public static string GetLocalEmulatorIpV4Address()
        {
            string bestAddress = null;

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                // We skip loopback, tunnel adapters etc ...
                if ((adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                    (adapter.OperationalStatus == OperationalStatus.Up))
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    foreach (IPAddressInformation ipAddress in properties.UnicastAddresses)
                    {
                        if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            if (ipAddress.IsDnsEligible)
                            {
                                return ipAddress.Address.ToString();
                            }
                            if (bestAddress == null)
                            {
                                bestAddress = ipAddress.Address.ToString();
                            }
                        }
                    }
                }
            }

            if (bestAddress != null)
            {
                return bestAddress;
            }

            // If we reach here, we couldn't find a usable IP address.
            string message = "ERROR: Could not locate any usable IPv4 address for local emulator";
            DefaultTrace.TraceCritical(message);
            throw new ConfigurationErrorsException(message);
        }

        public static bool GetIPv6ServiceTunnelAddress(bool isEmulated, out IPAddress ipv6LoopbackAddress)
        {
            if (isEmulated)
            {
                ipv6LoopbackAddress = IPAddress.IPv6Loopback;
                return true;
            }

            NetworkInterface[] niList = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in niList)
            {
                foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                        NetUtil.IsServiceTunneledIPAddress(ip.Address))
                    {
                        DefaultTrace.TraceInformation("Found VNET service tunnel destination: {0}", ip.Address.ToString());
                        ipv6LoopbackAddress = ip.Address;
                        return true;
                    }
                    else
                    {
                        DefaultTrace.TraceInformation("{0} is skipped because it is not IPv6 or is not a service tunneled IP address.", ip.Address.ToString());
                    }
                }
            }

            DefaultTrace.TraceInformation("Cannot find the IPv6 address of the Loopback NetworkInterface.");
            ipv6LoopbackAddress = null;
            return false;
        }

        private static bool IsServiceTunneledIPAddress(IPAddress ipAddress)
        {
            byte[] ipAddressBytes = ipAddress.GetAddressBytes();

            if (BitConverter.ToUInt64(ipAddressBytes, 0) == BitConverter.ToUInt64(paasV1Prefix, 0))
            {
                return true;
            }

            for (int i = 0; i < paasV2Prefix.Length; i++)
            {
                if (paasV2Prefix[i] != ipAddressBytes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
