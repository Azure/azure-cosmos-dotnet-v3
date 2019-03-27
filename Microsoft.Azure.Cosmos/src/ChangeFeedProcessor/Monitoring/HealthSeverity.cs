//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring
{
    /// <summary>
    /// The health severity level
    /// </summary>
    public enum HealthSeverity
    {
        /// <summary>
        /// Critical level.
        /// </summary>
        Critical = 10,

        /// <summary>
        /// Error level.
        /// </summary>
        Error = 20,

        /// <summary>
        /// Information level.
        /// </summary>
        Informational = 30,
    }
}