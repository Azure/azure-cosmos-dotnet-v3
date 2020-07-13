//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for retrieving an access token for Active Directory authentication.
    /// </summary>
    internal abstract class IAADTokenProvider
    {
        public abstract ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }
}
