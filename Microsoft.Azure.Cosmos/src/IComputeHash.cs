//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Security;

    internal interface IComputeHash : IDisposable
    {
        byte[] ComputeHash(MemoryStream bytesToHash);

        SecureString Key
        {
            get;
        }
    }
}
