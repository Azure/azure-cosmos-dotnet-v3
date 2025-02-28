//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Class storing Configuration Account Properties 
    /// </summary>
    internal class AccountConfigurationProperties
    {
        public AccountConfigurationProperties(bool EnableNRegionSynchronousCommit)
        {
            this.EnableNRegionSynchronousCommit = EnableNRegionSynchronousCommit;
        }

        /// <summary>
        /// Enable N-Region Synchronous Commit Feature 
        /// </summary>
        public bool EnableNRegionSynchronousCommit 
        { 
            get; private set;
        }
    }
}
