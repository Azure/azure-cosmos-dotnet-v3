//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// AddressEnumerator iterates a list of TransportAddressUris.
    /// </summary>
    internal interface IAddressEnumerator
    {
        IEnumerable<TransportAddressUri> GetTransportAddresses(IReadOnlyList<TransportAddressUri> transportAddressUris,
                                                               Lazy<HashSet<TransportAddressUri>> failedEndpoints);
    }
}