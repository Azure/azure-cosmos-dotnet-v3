//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    /// <summary>
    /// The health severity level
    /// </summary>
    internal enum HealthSeverity
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