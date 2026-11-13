//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// This enum specifies the types of geo links across regions, 
    /// with strong links having the lowest latency and best reliability, 
    /// and weak links having the highest latency and worst reliability.
    /// </summary>
    internal enum GeoLinkTypes
    {
        Strong, // < 100ms RTT; 5000 mile radius
        Medium, // < 200ms RTT; 10000 mile radius
        Weak    // > 200ms RTT; > 10000 mile radius
    }
}
