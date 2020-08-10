// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    internal sealed class SqlError
    {
        public SqlLocation Location { get; }

        public SqlErrorCode ErrorCode { get; }
    }
}
