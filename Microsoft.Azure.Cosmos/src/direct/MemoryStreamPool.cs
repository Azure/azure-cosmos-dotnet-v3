//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.IO;

    internal interface MemoryStreamPool
    {
        public bool TryGetMemoryStream(int capacity, out MemoryStream memoryStream);
    }
}
