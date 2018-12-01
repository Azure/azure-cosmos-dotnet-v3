//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlCollectionExpression : SqlObject
    {
        protected SqlCollectionExpression(SqlObjectKind kind)
            : base(kind)
        { }
    }
}
