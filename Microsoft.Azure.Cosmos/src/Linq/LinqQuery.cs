//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Represents a linq expression as a combination of sql query and client operation.
    /// </summary>
    internal class LinqQuery
    {
        public LinqQuery(SqlQuerySpec sqlQuerySpec, ClientOperation clientOperation)
        {
            this.SqlQuerySpec = sqlQuerySpec;
            this.ClientOperation = clientOperation;
        }

        public SqlQuerySpec SqlQuerySpec { get; }

        public ClientOperation ClientOperation { get; }
    }
}
