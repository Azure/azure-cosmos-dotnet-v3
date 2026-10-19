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
        EqualDistributionAcrossPartitions = 4
    }
}