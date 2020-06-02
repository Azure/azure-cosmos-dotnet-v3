//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for retrieving an access token for Active Directory authentication.
    /// </summary>
    internal interface IAADTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }
}
