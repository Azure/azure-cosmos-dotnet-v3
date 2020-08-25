//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Client;

    internal sealed class AddressInformation
    {
        public bool IsPublic { get; set; }

        public bool IsPrimary { get; set; }

        public Protocol Protocol { get; set; }

        public string PhysicalUri { get; set; }
    }
}
