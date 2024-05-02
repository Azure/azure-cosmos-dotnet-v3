//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Retry params that every service can configure
    /// Add more api's that control retry as needed, currently only exposes overall timeout
    /// </summary>
    internal interface IServiceRetryParams
    {
        public bool TryGetRetryTimeoutInSeconds(out int retryTimeoutInSeconds);
    }
}
