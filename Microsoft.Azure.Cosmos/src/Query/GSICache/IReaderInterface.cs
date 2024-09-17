//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal interface IReaderInterface
    {
        List<Documents.PartitionKeyRange> GetPartitionKeyRanges(string secondaryIndex, string secondaryIndexValue);
    }
}
