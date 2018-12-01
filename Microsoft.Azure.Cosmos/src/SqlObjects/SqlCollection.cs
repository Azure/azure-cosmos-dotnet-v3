//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlCollection : SqlObject
    {
        public SqlCollection(SqlObjectKind kind)
            : base(kind)
        { }
    }
}