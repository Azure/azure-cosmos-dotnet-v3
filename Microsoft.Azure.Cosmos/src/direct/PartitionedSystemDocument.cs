//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a Partitioned SystemDocument.
    /// It is partitioned across the server partitions like a regular document.
    /// </summary>
    internal class PartitionedSystemDocument : Resource
    {
        public PartitionedSystemDocument()
        {
        }
    }
}