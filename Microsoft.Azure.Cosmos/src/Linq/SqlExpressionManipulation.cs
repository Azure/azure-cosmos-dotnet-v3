//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
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
                        if (!(into is SqlArrayCreateScalarExpression arrayExp))
                        {
                            throw new DocumentQueryException("Expected a SqlArrayCreateScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression[] items = new SqlScalarExpression[arrayExp.Items.Count];
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
                        if (!(into is SqlBinaryScalarExpression binaryExp))
                        {
                            throw new DocumentQueryException("Expected a BinaryScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression replleft = Substitute(replacement, toReplace, binaryExp.LeftExpression);
                        SqlScalarExpression replright = Substitute(replacement, toReplace, binaryExp.RightExpression);
                        return SqlBinaryScalarExpression.Create(binaryExp.OperatorKind, replleft, replright);
                    }
                case SqlObjectKind.UnaryScalarExpression:
                    {
                        if (!(into is SqlUnaryScalarExpression unaryExp))
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
                        if (!(into is SqlFunctionCallScalarExpression funcExp))
                        {
                            throw new DocumentQueryException("Expected a SqlFunctionCallScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression[] items = new SqlScalarExpression[funcExp.Arguments.Count];
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
                        if (!(into is SqlObjectCreateScalarExpression objExp))
                        {
                            throw new DocumentQueryException("Expected a SqlObjectCreateScalarExpression, got a " + into.GetType());
                        }

                        return SqlObjectCreateScalarExpression.Create(
                            objExp
                                .Properties
                                .Select(prop => SqlObjectProperty.Create(prop.Name, Substitute(replacement, toReplace, prop.Expression))));
                    }
                case SqlObjectKind.MemberIndexerScalarExpression:
                    {
                        if (!(into is SqlMemberIndexerScalarExpression memberExp))
                        {
                            throw new DocumentQueryException("Expected a SqlMemberIndexerScalarExpression, got a " + into.GetType());
                        }

                        SqlScalarExpression replMember = Substitute(replacement, toReplace, memberExp.MemberExpression);
                        SqlScalarExpression replIndex = Substitute(replacement, toReplace, memberExp.IndexExpression);
                        return SqlMemberIndexerScalarExpression.Create(replMember, replIndex);
                    }
                case SqlObjectKind.PropertyRefScalarExpression:
                    {
                        // This is the leaf of the recursion
                        if (!(into is SqlPropertyRefScalarExpression propExp))
                        {
                            throw new DocumentQueryException("Expected a SqlPropertyRefScalarExpression, got a " + into.GetType());
                        }

                        if (propExp.MemberExpression == null)
                        {
                            if (propExp.PropertyIdentifier.Value == toReplace.Value)
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
                            SqlScalarExpression replMember = Substitute(replacement, toReplace, propExp.MemberExpression);
                            return SqlPropertyRefScalarExpression.Create(replMember, propExp.PropertyIdentifier);
                        }
                    }
                case SqlObjectKind.ConditionalScalarExpression:
                    {
                        SqlConditionalScalarExpression conditionalExpression = (SqlConditionalScalarExpression)into;
                        if (conditionalExpression == null)
                        {
                            throw new ArgumentException();
                        }

                        SqlScalarExpression condition = Substitute(replacement, toReplace, conditionalExpression.ConditionExpression);
                        SqlScalarExpression first = Substitute(replacement, toReplace, conditionalExpression.FirstExpression);
                        SqlScalarExpression second = Substitute(replacement, toReplace, conditionalExpression.SecondExpression);

                        return SqlConditionalScalarExpression.Create(condition, first, second);
                    }
                case SqlObjectKind.InScalarExpression:
                    {
                        SqlInScalarExpression inExpression = (SqlInScalarExpression)into;
                        if (inExpression == null)
                        {
                            throw new ArgumentException();
                        }

                        SqlScalarExpression expression = Substitute(replacement, toReplace, inExpression.Expression);

                        SqlScalarExpression[] items = new SqlScalarExpression[inExpression.Items.Count];
                        for (int i = 0; i < items.Length; i++)
                        {
                            items[i] = Substitute(replacement, toReplace, inExpression.Items[i]);
                        }

                        return SqlInScalarExpression.Create(expression, inExpression.Not, items);
                    }
                default:
                    throw new ArgumentOutOfRangeException("Unexpected Sql Scalar expression kind " + into.Kind);
            }
        }
    }
}
