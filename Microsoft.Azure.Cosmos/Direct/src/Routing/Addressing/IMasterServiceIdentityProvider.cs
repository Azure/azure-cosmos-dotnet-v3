//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IMasterServiceIdentityProvider
    {
        ServiceIdentity MasterServiceIdentity { get; }

        Task RefreshAsync(
            ServiceIdentity previousMasterService, 
            CancellationToken cancellationToken);
    }
}
