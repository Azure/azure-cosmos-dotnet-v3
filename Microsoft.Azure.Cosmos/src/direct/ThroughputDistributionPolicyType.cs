//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    internal enum ThroughputDistributionPolicyType
    {
        Invalid = -1,
        Default = 0,
        Custom = 1,
        ProportionalToStorage = 2,
        ProportionalToThroughputUtilization = 3,
        EqualDistributionAcrossPartitions = 4,
        CustomWithEqualOnScaleOp = 5
    }

    internal static class ThroughputDistributionPolicyTypeExtensions
    {
        /// <summary>
        /// True for redistribute policies (Custom, CustomWithEqualOnScaleOp) that can leave non-equal throughput fractions across partitions.
        /// </summary>
        public static bool IsRedistributePolicy(this ThroughputDistributionPolicyType? policy)
        {
            return policy == ThroughputDistributionPolicyType.Custom ||
                policy == ThroughputDistributionPolicyType.CustomWithEqualOnScaleOp;
        }
    }
}