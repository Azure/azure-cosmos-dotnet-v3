//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Interface extension for IServiceConfigurationReader for new boolean flag for N-Region Synchronous Commit.
    /// Instead of directly adding to the interface this is needed because the way compute 
    /// uses SDK is using a V3 OSS clone of our public v3 repo. So any change in the interface triggers a change in OSS/V3 codepath.
    /// </summary>
    internal interface IServiceConfigurationReaderVnext : IServiceConfigurationReader
    {
        /// <summary>
        /// Enable N-Region Synchronous Commit Feature 
        /// </summary>
        bool EnableNRegionSynchronousCommit { get; }
    }
}
