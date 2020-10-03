// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using Microsoft.Azure.Documents;

    internal sealed class DocumentContainerState : State
    {
        public DocumentContainerState(ResourceIdentifier resourceIdentifier)
        {
            this.ResourceIdentifer = resourceIdentifier;
        }

        public ResourceIdentifier ResourceIdentifer { get; }
    }
}
