//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal static class SDKSupportedCapabilitiesHelpers
    {
        public static ulong GetSDKSupportedCapabilities()
        {
            return (ulong)(Microsoft.Azure.Documents.SDKSupportedCapabilities.PartitionMerge
                | Microsoft.Azure.Documents.SDKSupportedCapabilities.ChangeFeedWithStartTimePostMerge
                | Microsoft.Azure.Documents.SDKSupportedCapabilities.IgnoreUnknownRntbdTokens);
        }
    }
}
