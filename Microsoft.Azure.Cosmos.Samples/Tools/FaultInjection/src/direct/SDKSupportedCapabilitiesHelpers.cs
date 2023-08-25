//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    internal static class SDKSupportedCapabilitiesHelpers
    {
        private static readonly ulong sdkSupportedCapabilities;

        static SDKSupportedCapabilitiesHelpers()
        {
            SDKSupportedCapabilities capabilities = SDKSupportedCapabilities.None;
            capabilities |= SDKSupportedCapabilities.PartitionMerge;

            SDKSupportedCapabilitiesHelpers.sdkSupportedCapabilities = (ulong)capabilities;
        }

        internal static ulong GetSDKSupportedCapabilities()
        {
            return SDKSupportedCapabilitiesHelpers.sdkSupportedCapabilities;
        }
    }
}
