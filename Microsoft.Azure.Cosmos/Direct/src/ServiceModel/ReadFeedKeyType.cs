//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Type of Start and End key for ReadFeedKey
    /// </summary>
    internal enum ReadFeedKeyType
    {
        /// <summary>
        ///  Use ResourceName
        /// </summary>
        ResourceId,

        /// <summary>
        /// Use EffectivePartitionKey
        /// </summary>
        EffectivePartitionKey,

        /// <summary>
        /// Use EffectivePartitionKeyRange 
        /// </summary>
        EffectivePartitionKeyRange
    }
}