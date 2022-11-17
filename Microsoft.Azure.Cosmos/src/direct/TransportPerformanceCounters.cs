//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// The Transport performance counter.
    /// As sdk-MDM integration is currently not available, this will ignore all metrics
    /// </summary>
    internal class TransportPerformanceCounters
    {
        internal virtual void IncrementRntbdRequestCount(ResourceType resourceType, OperationType operationType)
        {
        }

        internal virtual void IncrementRntbdResponseCount(ResourceType resourceType, OperationType operationType, int statusCode)
        {
        }

        internal virtual void IncrementRntbdConnectionEstablishedCount()
        {
        }

        internal virtual void IncrementRntbdConnectionClosedCount()
        {
        }

        internal virtual void LogRntbdBytesSentCount(ResourceType resourceType, OperationType operationType, long? bytes)
        {
        }

        internal virtual void LogRntbdBytesReceivedCount(ResourceType resourceType, OperationType operationType, long? bytes)
        {
        }
    }
}
