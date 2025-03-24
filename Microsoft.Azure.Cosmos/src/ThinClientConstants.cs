//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal static class ThinClientConstants
    {
        public const string RoutedViaProxy = "x-ms-thinclient-route-via-proxy";
        public const string ProxyStartEpk = "x-ms-thinclient-range-min";
        public const string ProxyEndEpk = "x-ms-thinclient-range-max";

        public const string ProxyOperationType = "x-ms-thinclient-proxy-operation-type";
        public const string ProxyResourceType = "x-ms-thinclient-proxy-resource-type";
        public const string EffectivePartitionKey = "x-ms-effective-partition-key";
    }
}
