//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;

    internal sealed class SqlRootNameExtractor : SqlCollectionExpressionVisitor
    {
        private string rootName;

        private SqlRootNameExtractor()
        {
            this.rootName = default(string);
        }

        public static string ExtractRootName(SqlCollectionExpression expression)
        {
            SqlRootNameExtractor extractor = new SqlRootNameExtractor();
            extractor.Visit(expression);
            return extractor.rootName;
        }

        protected override void Visit(SqlAliasedCollectionExpression expression)
        {
            if (expression.Alias != null)
            {
                this.rootName = expression.Alias.Value;
            }
            else
            {
                this.rootName = SqlCollectionRootNameExtractor.ExtractRootName(expression.Collection);
            }
        }

        protected override void Visit(SqlArrayIteratorCollectionExpression expression)
        {
            this.rootName = SqlCollectionRootNameExtractor.ExtractRootName(expression.Collection);
        }

        protected override void Visit(SqlJoinCollectionExpression expression)
        {
            this.Visit(expression.LeftExpression);
        }

        protected override void Visit(SqlSubqueryCollectionExpression expression)
        {
            this.rootName = SqlCollectionRootNameExtractor.ExtractRootName(expression.Query);
        }

        private sealed class SqlCollectionRootNameExtractor : SqlCollectionVisitor
        {
            private string rootName;

            private SqlCollectionRootNameExtractor()
            {
                this.rootName = string.Empty;
            }

            public static string ExtractRootName(SqlCollection collection)
            {
                SqlCollectionRootNameExtractor extractor = new SqlCollectionRootNameExtractor();
                extractor.Visit(collection);
                return extractor.rootName;
            }

            protected override void Visit(SqlInputPathCollection collection)
            {
                this.rootName = collection.Input.Value;
            }

            protected override void Visit(SqlLiteralArrayCollection collection)
            {
                this.rootName = string.Empty;
            }

            protected override void Visit(SqlSubqueryCollection collection)
            {
                if (collection.Query.FromClause != null)
                {
                    this.rootName = SqlRootNameExtractor.ExtractRootName(collection.Query.FromClause.Expression);
                }
                else
                {
                    this.rootName = string.Empty;
                }
            }
        }
    }
}
