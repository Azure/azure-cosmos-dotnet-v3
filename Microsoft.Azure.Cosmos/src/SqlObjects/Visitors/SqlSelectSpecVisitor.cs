//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlSelectSpecVisitor
    {
        public abstract void Visit(SqlSelectListSpec selectSpec);
        public abstract void Visit(SqlSelectStarSpec selectSpec);
        public abstract void Visit(SqlSelectValueSpec selectSpec);
    }

    internal abstract class SqlSelectSpecVisitor<TResult>
    {
        public abstract TResult Visit(SqlSelectListSpec selectSpec);
        public abstract TResult Visit(SqlSelectStarSpec selectSpec);
        public abstract TResult Visit(SqlSelectValueSpec selectSpec);
    }

    internal abstract class SqlSelectSpecVisitor<TArg, TOutput>
    {
        public abstract TOutput Visit(SqlSelectListSpec selectSpec, TArg input);
        public abstract TOutput Visit(SqlSelectStarSpec selectSpec, TArg input);
        public abstract TOutput Visit(SqlSelectValueSpec selectSpec, TArg input);
    }
}
