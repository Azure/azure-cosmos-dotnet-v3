//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Security;

    /// <summary>
    /// Interface for token providers whose key can be updated dynamically.
    /// </summary>
    internal interface IDynamicKeyTokenProvider
    {
        /// <summary>
        /// Updates the key used by this token provider.
        /// </summary>
        /// <param name="authKey">New auth key.</param>
        void UpdateKey(string authKey);
    }
}
