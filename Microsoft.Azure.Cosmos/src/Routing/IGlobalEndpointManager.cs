//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal interface IGlobalEndpointManager : IDisposable
    {
        ReadOnlyCollection<Uri> ReadEndpoints { get; }

        ReadOnlyCollection<Uri> WriteEndpoints { get; }

        int PreferredLocationCount { get; }

        Uri ResolveServiceEndpoint(DocumentServiceRequest request);

        string GetLocation(Uri endpoint);

        void MarkEndpointUnavailableForRead(Uri endpoint);

        void MarkEndpointUnavailableForWrite(Uri endpoint);

        bool CanUseMultipleWriteLocations(DocumentServiceRequest request);

        Task RefreshLocationAsync(AccountProperties databaseAccount, bool forceRefresh = false);
    }
}