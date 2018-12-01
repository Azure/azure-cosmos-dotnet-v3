//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;

    internal abstract class SqlCollectionVisitor
    {
        protected void Visit(SqlCollection collection)
        {
            switch(collection.Kind)
            {
                case SqlObjectKind.InputPathCollection:
                    this.Visit(collection as SqlInputPathCollection);
                    return;
                case SqlObjectKind.LiteralArrayCollection:
                    this.Visit(collection as SqlLiteralArrayCollection);
                    return;
                case SqlObjectKind.SubqueryCollection:
                    this.Visit(collection as SqlSubqueryCollection);
                    return;
                default:
                    throw new InvalidProgramException(
                        string.Format(CultureInfo.InvariantCulture, "Unexpected SqlObjectKind {0}", collection.Kind));
            }
        }

        protected abstract void Visit(SqlInputPathCollection collection);
        protected abstract void Visit(SqlLiteralArrayCollection collection);
        protected abstract void Visit(SqlSubqueryCollection collection);
    }

}
