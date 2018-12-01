//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlPathExpression : SqlObject
    {
        public SqlPathExpression ParentPath
        {
            get;
            private set;
        }

        protected SqlPathExpression(SqlObjectKind kind, SqlPathExpression parentPath)
            : base(kind)
        {
            this.ParentPath = parentPath;
        }
    }
}
