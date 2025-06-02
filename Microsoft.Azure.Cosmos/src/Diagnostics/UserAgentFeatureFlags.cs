//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// User agent feature flags.
    /// Each new feature flag will represent a bit in a number to encode what features are enabled.
    /// Therefore, the first feature flag will be 1, the second 2, the third 4, etc.
    /// When constructing the user agent suffix, the feature flags will be used to encode a unique number representing the features enabled.
    /// This number will be converted into a hex string following the prefix "F" to save space in the user agent as it is limited and appended to the user agent suffix.
    /// This number will then be used to determine what features are enabled by decoding the hex string back to a number and checking what bits are set.
    /// For example if the user agent suffix has F7, this means that the feature flags 1, 2 and 4 are enabled (the first three feature flags).
    /// </summary>
    internal enum UserAgentFeatureFlags
    {
        PerPartitionAutomaticFailover = 1,

        PerPartitionCircuitBreaker = 2,
    }
}
