//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    internal static class SqlExpressionManipulation
    {
        public static SqlScalarExpression Substitute(SqlScalarExpression replacement, SqlIdentifier toReplace, SqlScalarExpression into)
        {
            if (into == null)
            {
                return null;
            }

            if (replacement == null)
            {
                throw new ArgumentNullException("replacement");
            }

            return into.Accept(SubstitionVisitor.Singleton, (replacement, toReplace));
        }

        private sealed class SubstitionVisitor : SqlScalarExpressionVisitor<(SqlScalarExpression replacement, SqlIdentifier toReplace), SqlScalarExpression>
        {
            public static readonly SubstitionVisitor Singleton = new SubstitionVisitor();

            private SubstitionVisitor()
            {
            }

            public override SqlScalarExpression Visit(
                SqlArrayCreateScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression[] items = new SqlScalarExpression[scalarExpression.Items.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    SqlScalarExpression replitem = scalarExpression.Accept(this, input);
                    items[i] = replitem;
                }

                return SqlArrayCreateScalarExpression.Create(items);
            }

            public override SqlScalarExpression Visit(
                SqlArrayScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                throw new NotImplementedException();
            }

            public override SqlScalarExpression Visit(
                SqlBetweenScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                throw new NotImplementedException();
            }

            public override SqlScalarExpression Visit(
                SqlBinaryScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression replleft = scalarExpression.Left.Accept(this, input);
                SqlScalarExpression replright = scalarExpression.Right.Accept(this, input);
                return SqlBinaryScalarExpression.Create(scalarExpression.OperatorKind, replleft, replright);
            }

            public override SqlScalarExpression Visit(
                SqlCoalesceScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                throw new NotImplementedException();
            }

            public override SqlScalarExpression Visit(
                SqlConditionalScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression condition = scalarExpression.Condition.Accept(this, input);
                SqlScalarExpression first = scalarExpression.Consequent.Accept(this, input);
                SqlScalarExpression second = scalarExpression.Alternative.Accept(this, input);

                return SqlConditionalScalarExpression.Create(condition, first, second);
            }

            public override SqlScalarExpression Visit(
                SqlExistsScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                throw new NotImplementedException();
            }

            public override SqlScalarExpression Visit(
                SqlFunctionCallScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression[] items = new SqlScalarExpression[scalarExpression.Arguments.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    SqlScalarExpression item = scalarExpression.Arguments[i];
                    SqlScalarExpression replitem = item.Accept(this, input);
                    items[i] = replitem;
                }

                return SqlFunctionCallScalarExpression.Create(scalarExpression.Name, scalarExpression.IsUdf, items);
            }

            public override SqlScalarExpression Visit(SqlInScalarExpression scalarExpression, (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression expression = scalarExpression.Needle.Accept(this, input);

                SqlScalarExpression[] items = new SqlScalarExpression[scalarExpression.Haystack.Count];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = scalarExpression.Haystack[i].Accept(this, input);
                }

                return SqlInScalarExpression.Create(expression, scalarExpression.Not, items);
            }

            public override SqlScalarExpression Visit(
                SqlLiteralScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                return scalarExpression;
            }

            public override SqlScalarExpression Visit(SqlMemberIndexerScalarExpression scalarExpression, (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression replMember = scalarExpression.Member.Accept(this, input);
                SqlScalarExpression replIndex = scalarExpression.Indexer.Accept(this, input);
                return SqlMemberIndexerScalarExpression.Create(replMember, replIndex);
            }

            public override SqlScalarExpression Visit(
                SqlObjectCreateScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                return SqlObjectCreateScalarExpression.Create(
                    scalarExpression
                        .Properties
                        .Select(prop => SqlObjectProperty.Create(prop.Name, prop.Value.Accept(this, input))));
            }

            public override SqlScalarExpression Visit(
                SqlParameterRefScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                throw new NotImplementedException();
            }

            public override SqlScalarExpression Visit(
                SqlPropertyRefScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                // This is the leaf of the recursion
                if (scalarExpression.Member != null)
                {
                    SqlScalarExpression replMember = scalarExpression.Member.Accept(this, input);
                    return SqlPropertyRefScalarExpression.Create(replMember, scalarExpression.Identifier);
                }

                if (scalarExpression.Identifier.Value != input.toReplace.Value)
                {
                    return scalarExpression;
                }

                return input.replacement;
            }

            public override SqlScalarExpression Visit(
                SqlSubqueryScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                throw new NotImplementedException();
            }

            public override SqlScalarExpression Visit(
                SqlUnaryScalarExpression scalarExpression,
                (SqlScalarExpression replacement, SqlIdentifier toReplace) input)
            {
                SqlScalarExpression repl = scalarExpression.Expression.Accept(this, input);
                return SqlUnaryScalarExpression.Create(scalarExpression.OperatorKind, repl);
            }
        }
    }
}
