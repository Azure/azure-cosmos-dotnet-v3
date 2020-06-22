//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;

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

            switch (into)
            {
                case SqlArrayCreateScalarExpression arrayExp:
                    {
                        SqlScalarExpression[] items = new SqlScalarExpression[arrayExp.Items.Count];
                        for (int i = 0; i < items.Length; i++)
                        {
                            SqlScalarExpression item = arrayExp.Items[i];
                            SqlScalarExpression replitem = Substitute(replacement, toReplace, item);
                            items[i] = replitem;
                        }

                        return SqlArrayCreateScalarExpression.Create(items);
                    }
                case SqlBinaryScalarExpression binaryExp:
                    {
                        SqlScalarExpression replleft = Substitute(replacement, toReplace, binaryExp.LeftExpression);
                        SqlScalarExpression replright = Substitute(replacement, toReplace, binaryExp.RightExpression);
                        return SqlBinaryScalarExpression.Create(binaryExp.OperatorKind, replleft, replright);
                    }
                case SqlUnaryScalarExpression unaryExp:
                    {
                        SqlScalarExpression repl = Substitute(replacement, toReplace, unaryExp.Expression);
                        return SqlUnaryScalarExpression.Create(unaryExp.OperatorKind, repl);
                    }
                case SqlLiteralScalarExpression literalScalarExpression:
                    {
                        return into;
                    }
                case SqlFunctionCallScalarExpression funcExp:
                    {
                        SqlScalarExpression[] items = new SqlScalarExpression[funcExp.Arguments.Count];
                        for (int i = 0; i < items.Length; i++)
                        {
                            SqlScalarExpression item = funcExp.Arguments[i];
                            SqlScalarExpression replitem = Substitute(replacement, toReplace, item);
                            items[i] = replitem;
                        }

                        return SqlFunctionCallScalarExpression.Create(funcExp.Name, funcExp.IsUdf, items);
                    }
                case SqlObjectCreateScalarExpression objExp:
                    {
                        return SqlObjectCreateScalarExpression.Create(
                            objExp
                                .Properties
                                .Select(prop => SqlObjectProperty.Create(prop.Name, Substitute(replacement, toReplace, prop.Value))));
                    }
                case SqlMemberIndexerScalarExpression memberExp:
                    {
                        SqlScalarExpression replMember = Substitute(replacement, toReplace, memberExp.Member);
                        SqlScalarExpression replIndex = Substitute(replacement, toReplace, memberExp.Indexer);
                        return SqlMemberIndexerScalarExpression.Create(replMember, replIndex);
                    }
                case SqlPropertyRefScalarExpression propExp:
                    {
                        // This is the leaf of the recursion
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
                case SqlConditionalScalarExpression conditionalExpression:
                    {
                        SqlScalarExpression condition = Substitute(replacement, toReplace, conditionalExpression.Condition);
                        SqlScalarExpression first = Substitute(replacement, toReplace, conditionalExpression.Consequent);
                        SqlScalarExpression second = Substitute(replacement, toReplace, conditionalExpression.Alternative);

                        return SqlConditionalScalarExpression.Create(condition, first, second);
                    }
                case SqlInScalarExpression inExpression:
                    {
                        SqlScalarExpression expression = Substitute(replacement, toReplace, inExpression.Needle);

                        SqlScalarExpression[] items = new SqlScalarExpression[inExpression.Haystack.Count];
                        for (int i = 0; i < items.Length; i++)
                        {
                            items[i] = Substitute(replacement, toReplace, inExpression.Haystack[i]);
                        }

                        return SqlInScalarExpression.Create(expression, inExpression.Not, items);
                    }
                default:
                    throw new ArgumentOutOfRangeException("Unexpected Sql Scalar expression kind " + into.GetType());
            }
        }
    }
}
