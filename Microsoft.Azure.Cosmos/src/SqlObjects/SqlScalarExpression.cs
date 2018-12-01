//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlScalarExpression : SqlObject
    {
        protected SqlScalarExpression(SqlObjectKind kind)
            : base(kind)
        { }
    }
}
