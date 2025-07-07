//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Query.Core;

    /// <summary>
    /// Represents a linq expression as a combination of sql query and client operation.
    /// </summary>
    internal class LinqQueryOperation
    {
        public LinqQueryOperation(SqlQuerySpec sqlQuerySpec, ScalarOperationKind scalarOperationKind)
        {
            this.SqlQuerySpec = sqlQuerySpec;
            this.ScalarOperationKind = scalarOperationKind;
        }

        public SqlQuerySpec SqlQuerySpec { get; }

        public ScalarOperationKind ScalarOperationKind { get; }
    }
}
