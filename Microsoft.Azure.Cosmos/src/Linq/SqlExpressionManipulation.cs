//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Sql;
    using System;

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
                    var arrayExp = into as SqlArrayCreateScalarExpression;
                    if (arrayExp == null)
                    {
                        throw new DocumentQueryException("Expected a SqlArrayCreateScalarExpression, got a " + into.GetType());
                    }

                    SqlScalarExpression[] items = new SqlScalarExpression[arrayExp.Items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = arrayExp.Items[i];
                        var replitem = Substitute(replacement, toReplace, item);
                        items[i] = replitem;
                    }

                    return new SqlArrayCreateScalarExpression(items);
                }
                case SqlObjectKind.BinaryScalarExpression:
                {
                    var binaryExp = into as SqlBinaryScalarExpression;
                    if (binaryExp == null)
                    {
                        throw new DocumentQueryException("Expected a BinaryScalarExpression, got a " + into.GetType());
                    }

                    var replleft = Substitute(replacement, toReplace, binaryExp.LeftExpression);
                    var replright = Substitute(replacement, toReplace, binaryExp.RightExpression);
                    return new SqlBinaryScalarExpression(binaryExp.OperatorKind, replleft, replright);
                }
                case SqlObjectKind.UnaryScalarExpression:
                {
                    var unaryExp = into as SqlUnaryScalarExpression;
                    if (unaryExp == null)
                    {
                        throw new DocumentQueryException("Expected a SqlUnaryScalarExpression, got a " + into.GetType());
                    }

                    var repl = Substitute(replacement, toReplace, unaryExp.Expression);
                    return new SqlUnaryScalarExpression(unaryExp.OperatorKind, repl);
                }
                case SqlObjectKind.LiteralScalarExpression:
                {
                    return into;
                }
                case SqlObjectKind.FunctionCallScalarExpression:
                {
                    var funcExp = into as SqlFunctionCallScalarExpression;
                    if (funcExp == null)
                    {
                        throw new DocumentQueryException("Expected a SqlFunctionCallScalarExpression, got a " + into.GetType());
                    }

                    SqlScalarExpression[] items = new SqlScalarExpression[funcExp.Arguments.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = funcExp.Arguments[i];
                        var replitem = Substitute(replacement, toReplace, item);
                        items[i] = replitem;
                    }

                    return new SqlFunctionCallScalarExpression(funcExp.Name, items);
                }
                case SqlObjectKind.ObjectCreateScalarExpression:
                {
                    var objExp = into as SqlObjectCreateScalarExpression;
                    if (objExp == null)
                    {
                        throw new DocumentQueryException("Expected a SqlObjectCreateScalarExpression, got a " + into.GetType());
                    }
                    
                    SqlObjectProperty[] items = new SqlObjectProperty[objExp.Properties.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var prop = objExp.Properties[i];
                        var value = prop.Expression;
                        var replitem = Substitute(replacement, toReplace, value);
                        SqlObjectProperty replProp = new SqlObjectProperty(prop.Name, replitem);
                        items[i] = replProp;
                    }
                    
                    return new SqlObjectCreateScalarExpression(items);
                }
                case SqlObjectKind.MemberIndexerScalarExpression:
                {
                    var memberExp = into as SqlMemberIndexerScalarExpression;
                    if (memberExp == null)
                    {
                        throw new DocumentQueryException("Expected a SqlMemberIndexerScalarExpression, got a " + into.GetType());
                    }

                    var replMember = Substitute(replacement, toReplace, memberExp.MemberExpression);
                    var replIndex = Substitute(replacement, toReplace, memberExp.IndexExpression);
                    return new SqlMemberIndexerScalarExpression(replMember, replIndex);
                }
                case SqlObjectKind.PropertyRefScalarExpression:
                {
                    // This is the leaf of the recursion
                    var propExp = into as SqlPropertyRefScalarExpression;
                    if (propExp == null)
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
                        var replMember = Substitute(replacement, toReplace, propExp.MemberExpression);
                        return new SqlPropertyRefScalarExpression(replMember, propExp.PropertyIdentifier);
                    }
                }
                case SqlObjectKind.ConditionalScalarExpression:
                {
                    SqlConditionalScalarExpression conditionalExpression = (SqlConditionalScalarExpression)into;
                    if(conditionalExpression == null)
                    {
                        throw new ArgumentException();
                    }

                    SqlScalarExpression condition = Substitute(replacement, toReplace, conditionalExpression.ConditionExpression);
                    SqlScalarExpression first = Substitute(replacement, toReplace, conditionalExpression.FirstExpression);
                    SqlScalarExpression second = Substitute(replacement, toReplace, conditionalExpression.SecondExpression);

                    return new SqlConditionalScalarExpression(condition, first, second);
                }
                case SqlObjectKind.InScalarExpression:
                {
                    SqlInScalarExpression inExpression = (SqlInScalarExpression)into;
                    if(inExpression == null)
                    {
                        throw new ArgumentException();
                    }

                    SqlScalarExpression expression = Substitute(replacement, toReplace, inExpression.Expression);

                    SqlScalarExpression[] items = new SqlScalarExpression[inExpression.Items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        items[i] = Substitute(replacement, toReplace, inExpression.Items[i]);
                    }

                    return new SqlInScalarExpression(expression, items, inExpression.Not);
                }
                default:
                    throw new ArgumentOutOfRangeException("Unexpected Sql Scalar expression kind " + into.Kind);
            }
        }
    }
}