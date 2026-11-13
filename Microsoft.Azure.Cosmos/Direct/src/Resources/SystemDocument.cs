//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Represents a NonPartitioned SystemDocument.
    /// It is replicated on all the server partitions.
    /// </summary>
    internal class SystemDocument : Resource
    {
        public SystemDocument()
        {
        }
    }
}