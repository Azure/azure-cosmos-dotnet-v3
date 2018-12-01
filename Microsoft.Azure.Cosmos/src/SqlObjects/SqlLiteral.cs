//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlLiteral : SqlObject
    {
        public SqlLiteral(SqlObjectKind kind)
            : base(kind)
        {}
    }
}
