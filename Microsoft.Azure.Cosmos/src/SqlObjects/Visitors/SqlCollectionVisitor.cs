//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlCollectionVisitor
    {
        public abstract void Visit(SqlInputPathCollection collection);

        public abstract void Visit(SqlLiteralArrayCollection collection);

        public abstract void Visit(SqlSubqueryCollection collection);
    }

    internal abstract class SqlCollectionVisitor<TResult>
    {
        public abstract TResult Visit(SqlInputPathCollection collection);

        public abstract TResult Visit(SqlLiteralArrayCollection collection);

        public abstract TResult Visit(SqlSubqueryCollection collection);
    }

    internal abstract class SqlCollectionVisitor<TInput, TOuput>
    {
        public abstract TOuput Visit(SqlInputPathCollection collection, TInput input);

        public abstract TOuput Visit(SqlLiteralArrayCollection collection, TInput input);

        public abstract TOuput Visit(SqlSubqueryCollection collection, TInput input);
    }
}
