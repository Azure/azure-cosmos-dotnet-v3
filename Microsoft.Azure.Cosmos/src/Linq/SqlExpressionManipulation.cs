//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Sql;

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

            switch (into.Kind)
            {
                case SqlObjectKind.ArrayCreateScalarExpression:
                    {
                        SqlArrayCreateScalarExpression arrayExp = into as SqlArrayCreateScalarExpression;
                        if (arrayExp == null)
                        {
                            throw new DocumentQueryException("Expected a SqlArrayCreateScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression[] items = new SqlScalarExpression[arrayExp.Items.Length];
                        for (int i = 0; i < items.Length; i++)
                        {
                            SqlScalarExpression item = arrayExp.Items[i];
                            SqlScalarExpression replitem = Substitute(replacement, toReplace, item);
                            items[i] = replitem;
                        }

                        return SqlArrayCreateScalarExpression.Create(items);
                    }
                case SqlObjectKind.BinaryScalarExpression:
                    {
                        SqlBinaryScalarExpression binaryExp = into as SqlBinaryScalarExpression;
                        if (binaryExp == null)
                        {
                            throw new DocumentQueryException("Expected a BinaryScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression replleft = Substitute(replacement, toReplace, binaryExp.LeftExpression);
                        SqlScalarExpression replright = Substitute(replacement, toReplace, binaryExp.RightExpression);
                        return SqlBinaryScalarExpression.Create(replleft, binaryExp.OperatorKind, replright);
                    }
                case SqlObjectKind.UnaryScalarExpression:
                    {
                        SqlUnaryScalarExpression unaryExp = into as SqlUnaryScalarExpression;
                        if (unaryExp == null)
                        {
                            throw new DocumentQueryException("Expected a SqlUnaryScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression repl = Substitute(replacement, toReplace, unaryExp.Expression);
                        return SqlUnaryScalarExpression.Create(unaryExp.OperatorKind, repl);
                    }
                case SqlObjectKind.LiteralScalarExpression:
                    {
                        return into;
                    }
                case SqlObjectKind.FunctionCallScalarExpression:
                    {
                        SqlFunctionCallScalarExpression funcExp = into as SqlFunctionCallScalarExpression;
                        if (funcExp == null)
                        {
                            throw new DocumentQueryException("Expected a SqlFunctionCallScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression[] items = new SqlScalarExpression[funcExp.Arguments.Length];
                        for (int i = 0; i < items.Length; i++)
                        {
                            SqlScalarExpression item = funcExp.Arguments[i];
                            SqlScalarExpression replitem = Substitute(replacement, toReplace, item);
                            items[i] = replitem;
                        }

                        return SqlFunctionCallScalarExpression.Create(funcExp.Name, funcExp.IsUdf, items);
                    }
                case SqlObjectKind.ObjectCreateScalarExpression:
                    {
                        SqlObjectCreateScalarExpression objExp = into as SqlObjectCreateScalarExpression;
                        if (objExp == null)
                        {
                            throw new DocumentQueryException("Expected a SqlObjectCreateScalarExpression, got a " + into.GetType());
                        }

                        return SqlObjectCreateScalarExpression.Create(
                            objExp
                                .Properties
                                .Select(prop => SqlObjectProperty.Create(prop.Name, Substitute(replacement, toReplace, prop.Value)))
                                .ToImmutableArray());
                    }
                case SqlObjectKind.MemberIndexerScalarExpression:
                    {
                        SqlMemberIndexerScalarExpression memberExp = into as SqlMemberIndexerScalarExpression;
                        if (memberExp == null)
                        {
                            throw new DocumentQueryException("Expected a SqlMemberIndexerScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression replMember = Substitute(replacement, toReplace, memberExp.Member);
                        SqlScalarExpression replIndex = Substitute(replacement, toReplace, memberExp.Indexer);
                        return SqlMemberIndexerScalarExpression.Create(replMember, replIndex);
                    }
                case SqlObjectKind.PropertyRefScalarExpression:
                    {
                        // This is the leaf of the recursion
                        SqlPropertyRefScalarExpression propExp = into as SqlPropertyRefScalarExpression;
                        if (propExp == null)
                        {
                            throw new DocumentQueryException("Expected a SqlPropertyRefScalarExpression, got a " + into.GetType());
                        }

                        if (propExp.Member == null)
                        {
                            if (propExp.Identifer.Value == toReplace.Value)
                            {
                                return replacement;
                            }
                            else
                            {
                                return propExp;
                            }
                        }
                        else
                        {
                            SqlScalarExpression replMember = Substitute(replacement, toReplace, propExp.Member);
                            return SqlPropertyRefScalarExpression.Create(replMember, propExp.Identifer);
                        }
                    }
                case SqlObjectKind.ConditionalScalarExpression:
                    {
                        SqlConditionalScalarExpression conditionalExpression = (SqlConditionalScalarExpression)into;
                        if (conditionalExpression == null)
                        {
                            throw new ArgumentException();
                        }

                        SqlScalarExpression condition = Substitute(replacement, toReplace, conditionalExpression.Condition);
                        SqlScalarExpression first = Substitute(replacement, toReplace, conditionalExpression.Consequent);
                        SqlScalarExpression second = Substitute(replacement, toReplace, conditionalExpression.Alternative);

                        return SqlConditionalScalarExpression.Create(condition, first, second);
                    }
                case SqlObjectKind.InScalarExpression:
                    {
                        SqlInScalarExpression inExpression = (SqlInScalarExpression)into;
                        if (inExpression == null)
                        {
                            throw new ArgumentException();
                        }

                        SqlScalarExpression expression = Substitute(replacement, toReplace, inExpression.Needle);

                        SqlScalarExpression[] items = new SqlScalarExpression[inExpression.Haystack.Length];
                        for (int i = 0; i < items.Length; i++)
                        {
                            items[i] = Substitute(replacement, toReplace, inExpression.Haystack[i]);
                        }

                        return SqlInScalarExpression.Create(expression, inExpression.Not, items);
                    }
                default:
                    throw new ArgumentOutOfRangeException("Unexpected Sql Scalar expression kind " + into.Kind);
            }
        }
    }
}
