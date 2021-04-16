//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;

    internal interface IGlobalEndpointManager : IDisposable
    {
        public ReadOnlyCollection<Uri> ReadEndpoints { get; }

        public ReadOnlyCollection<Uri> WriteEndpoints { get; }

        public bool CanUseMultipleWriteLocations(DocumentServiceRequest request);
    }
}
