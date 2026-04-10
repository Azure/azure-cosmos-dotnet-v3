//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Cql;

    internal class DistributionPlanWriter : ICqlVisitor
    {
        private readonly StringBuilder output = new StringBuilder();

        public string SerializedOutput => "{ \"clientDistributionPlan\": { \"clientQL\": { " + this.output.ToString() + " } } }";

        void ICqlVisitor.Visit(CqlAggregate cqlAggregate)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlAggregateEnumerableExpression cqlAggregateEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"Aggregate\", ");
            this.output.Append("\"Aggregate\": { ");
            cqlAggregateEnumerableExpression.Aggregate.Accept(this);
            this.output.Append(" }, ");
            this.output.Append("\"SourceExpression\": { ");
            cqlAggregateEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append("}");
        }

        void ICqlVisitor.Visit(CqlAggregateKind cqlAggregateKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlAggregateOperatorKind cqlAggregateOperatorKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlArrayCreateScalarExpression cqlArrayCreateScalarExpression)
        {
            this.output.Append("\"Kind\": \"ArrayCreate\", ");
            this.output.Append($"\"ArrayKind\": \"{cqlArrayCreateScalarExpression.ArrayKind}\", ");
            this.output.Append("\"Items\": [");
            int count = 0;
            foreach (CqlScalarExpression item in cqlArrayCreateScalarExpression.Items)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                item.Accept(this);
                this.output.Append("}");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlArrayIndexerScalarExpression cqlArrayIndexerScalarExpression)
        {
            this.output.Append("\"Kind\": \"ArrayIndexer\", ");
            this.output.Append("\"Expression\": { ");
            cqlArrayIndexerScalarExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append($"\"Index\": {cqlArrayIndexerScalarExpression.Index} ");
        }

        void ICqlVisitor.Visit(CqlArrayLiteral cqlArrayLiteral)
        {
            this.output.Append("\"Items\": [");
            int count = 0;

            foreach (CqlLiteral item in cqlArrayLiteral.Items)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                item.Accept(this);
                this.output.Append("}");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlBinaryScalarExpression cqlBinaryScalarExpression)
        {
            this.output.Append("\"Kind\": \"BinaryOperator\", ");
            this.output.Append($"\"OperatorKind\": \"{cqlBinaryScalarExpression.OperatorKind.ToString()}\", ");
            this.output.Append("\"LeftExpression\": { ");
            cqlBinaryScalarExpression.LeftExpression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("\"RightExpression\": { ");
            cqlBinaryScalarExpression.RightExpression.Accept(this);
            this.output.Append("} ");
        }

        void ICqlVisitor.Visit(CqlBinaryScalarOperatorKind cqlBinaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlBooleanLiteral cqlBooleanLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlBuiltinAggregate cqlBuiltinAggregate)
        {
            this.output.Append($"\"Kind\": \"{cqlBuiltinAggregate.Kind.ToString()}\", ");
            this.output.Append($"\"OperatorKind\": \"{cqlBuiltinAggregate.OperatorKind.ToString()}\"");
        }

        void ICqlVisitor.Visit(CqlBuiltinScalarFunctionKind cqlBuiltinScalarFunctionKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlDistinctEnumerableExpression cqlDistinctEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"Distinct\", ");
            string name = cqlDistinctEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlDistinctEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"\"DeclaredVariable\": {{ \"Name\": \"{name}\", \"UniqueId\": {uniqueId} }}, ");
            IReadOnlyList<CqlScalarExpression> scalarExpressions = cqlDistinctEnumerableExpression.Expression;
            this.output.Append("\"Expressions\": [ ");
            int count = 0;
            foreach (CqlScalarExpression scalarExpression in scalarExpressions)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                scalarExpression.Accept(this);
                this.output.Append("} ");
            }

            this.output.Append(" ], ");
            this.output.Append("\"SourceExpression\": { ");
            cqlDistinctEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }");
        }

        void ICqlVisitor.Visit(CqlEnumerableExpression cqlEnumerableExpression)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlEnumerableExpressionKind cqlEnumerableExpressionKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlEnumerationKind cqlEnumerationKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlFunctionIdentifier cqlFunctionIdentifier)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlGroupByEnumerableExpression cqlGroupByEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"GroupBy\", ");
            this.output.Append($"\"KeyCount\": {cqlGroupByEnumerableExpression.KeyCount}, \"Aggregates\": [ ");
            int count = 0;
            foreach (CqlAggregate aggregate in cqlGroupByEnumerableExpression.Aggregates)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                aggregate.Accept(this);
                this.output.Append("}");
            }

            this.output.Append("], ");
            this.output.Append("\"SourceExpression\": { ");
            cqlGroupByEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" } ");
        }

        void ICqlVisitor.Visit(CqlInputEnumerableExpression cqlInputEnumerableExpression)
        {
            string name = cqlInputEnumerableExpression.Name;
            this.output.Append($"\"Kind\": \"Input\", \"Name\": \"{name}\"");
        }

        void ICqlVisitor.Visit(CqlIsOperatorKind cqlIsOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlIsOperatorScalarExpression cqlIsOperatorScalarExpression)
        {
            this.output.Append("\"Kind\": \"IsOperator\", ");
            this.output.Append("\"Expression\": { ");
            cqlIsOperatorScalarExpression.Expression.Accept(this);
            this.output.Append(" }, ");
        }

        void ICqlVisitor.Visit(CqlLetScalarExpression cqlLetScalarExpression)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLiteral cqlLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLiteralKind cqlLiteralKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLiteralScalarExpression cqlLiteralScalarExpression)
        {
            CqlLiteralKind literalKind = cqlLiteralScalarExpression.Literal.Kind;
            this.output.Append("\"Kind\": \"Literal\", ");
            this.output.Append("\"Literal\": { ");
            this.output.Append($"\"Kind\": \"{literalKind}\"");
            if (literalKind != CqlLiteralKind.Undefined)
            {
                this.output.Append(", ");
                cqlLiteralScalarExpression.Literal.Accept(this);
            }

            this.output.Append("}");
        }

        void ICqlVisitor.Visit(CqlMuxScalarExpression cqlMuxScalarExpression)
        {
            this.output.Append("\"Kind\": \"Mux\", ");
            this.output.Append("\"ConditionExpression\": { ");
            cqlMuxScalarExpression.ConditionExpression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("\"LeftExpression\": { ");
            cqlMuxScalarExpression.LeftExpression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("\"RightExpression\": { ");
            cqlMuxScalarExpression.RightExpression.Accept(this);
            this.output.Append("} ");
        }

        void ICqlVisitor.Visit(CqlNullLiteral cqlNullLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlNumberLiteral cqlNumberLiteral)
        {
            this.output.Append($"\"Value\": {cqlNumberLiteral.Value} ");
        }

        void ICqlVisitor.Visit(CqlObjectCreateScalarExpression cqlObjectCreateScalarExpression)
        {
            this.output.Append("\"Kind\": \"ObjectCreate\", ");
            string objectKind = cqlObjectCreateScalarExpression.ObjectKind;
            this.output.Append($"\"ObjectKind\": \"{objectKind}\", ");
            this.output.Append("\"Properties\": [ ");
            int count = 0;
            foreach (CqlObjectProperty property in cqlObjectCreateScalarExpression.Properties)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append($"{{ \"Name\": \"{property.Name}\", ");
                this.output.Append("\"Expression\": { ");
                property.Expression.Accept(this);
                this.output.Append("} } ");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlObjectLiteral cqlObjectLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlObjectLiteralProperty cqlObjectLiteralProperty)
        {
        }

        void ICqlVisitor.Visit(CqlObjectProperty cqlObjectProperty)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlOrderByEnumerableExpression cqlOrderByEnumerableExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlOrderByItem cqlOrderByItem)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlPropertyRefScalarExpression cqlPropertyRefScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlScalarAsEnumerableExpression cqlScalarAsEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"ScalarAsEnumerable\", ");
            this.output.Append("\"Expression\": { ");
            cqlScalarAsEnumerableExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append($"\"EnumerationKind\": \"{cqlScalarAsEnumerableExpression.EnumerationKind}\"");
        }

        void ICqlVisitor.Visit(CqlScalarExpression cqlScalarExpression)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlScalarExpressionKind cqlScalarExpressionKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlSelectEnumerableExpression cqlSelectEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"Select\", ");
            string name = cqlSelectEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlSelectEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"\"DeclaredVariable\": {{ \"Name\": \"{name}\", \"UniqueId\": {uniqueId} }}, ");
            this.output.Append("\"Expression\": { ");
            cqlSelectEnumerableExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("\"SourceExpression\": { ");
            cqlSelectEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }");
        }

        void ICqlVisitor.Visit(CqlSelectManyEnumerableExpression cqlSelectManyEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"SelectMany\", ");
            string name = cqlSelectManyEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlSelectManyEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"\"DeclaredVariable\": {{ \"Name\": \"{name}\", \"UniqueId\": {uniqueId} }}, ");
            this.output.Append("\"SelectorExpression\": { ");
            cqlSelectManyEnumerableExpression.SelectorExpression.Accept(this);
            this.output.Append("},");
            this.output.Append("\"SourceExpression\": { ");
            cqlSelectManyEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append('}');
        }

        void ICqlVisitor.Visit(CqlSortOrder cqlSortOrder)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlStringLiteral cqlStringLiteral)
        {
            this.output.Append("\"Kind\": \"String\", ");
            this.output.Append($"\"Value\": \"{cqlStringLiteral.Value}\"");
        }

        void ICqlVisitor.Visit(CqlSystemFunctionCallScalarExpression cqlSystemFunctionCallScalarExpression)
        {
            this.output.Append("\"Kind\": \"SystemFunctionCall\", ");
            this.output.Append($"\"FunctionKind\": \"{cqlSystemFunctionCallScalarExpression.FunctionKind.ToString()}\", ");
            IReadOnlyList<CqlScalarExpression> scalarExpressions = cqlSystemFunctionCallScalarExpression.Arguments;
            this.output.Append("\"Arguments\": [ ");
            int count = 0;
            foreach (CqlScalarExpression scalarExpression in scalarExpressions)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                scalarExpression.Accept(this);
                this.output.Append("}");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlTakeEnumerableExpression cqlTakeEnumerableExpression)
        {
            this.output.Append($"\"Kind\": \"Take\", \"SkipValue\": {cqlTakeEnumerableExpression.SkipValue}, \"TakeValue\": {cqlTakeEnumerableExpression.TakeValue}, ");
            this.output.Append("\"SourceExpression\": { ");
            cqlTakeEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" } ");
        }

        void ICqlVisitor.Visit(CqlTupleAggregate cqlTupleAggregate)
        {
            this.output.Append("\"Kind\": \"Tuple\", ");
            this.output.Append("\"Items\": [");
            int count = 0;
            foreach (CqlAggregate item in cqlTupleAggregate.Items)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                item.Accept(this);
                this.output.Append("}");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlTupleCreateScalarExpression cqlTupleCreateScalarExpression)
        {
            this.output.Append(" \"Kind\": \"TupleCreate\", ");
            this.output.Append("\"Items\": [");
            int count = 0;
            foreach (CqlScalarExpression scalarExpression in cqlTupleCreateScalarExpression.Items)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                scalarExpression.Accept(this);
                this.output.Append("}");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlTupleItemRefScalarExpression cqlTupleItemRefScalarExpression)
        {
            this.output.Append("\"Kind\": \"TupleItemRef\", ");
            this.output.Append("\"Expression\": { ");
            cqlTupleItemRefScalarExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append($"\"Index\": {cqlTupleItemRefScalarExpression.Index}");
        }

        void ICqlVisitor.Visit(CqlUnaryScalarExpression cqlUnaryScalarExpression)
        {
            this.output.Append("\"Kind\": \"UnaryOperator\", ");
            this.output.Append($"\"OperatorKind\": \"{cqlUnaryScalarExpression.OperatorKind.ToString()}\", ");
            this.output.Append("\"Expression\": { ");
            cqlUnaryScalarExpression.Expression.Accept(this);
            this.output.Append("} ");
        }

        void ICqlVisitor.Visit(CqlUnaryScalarOperatorKind cqlUnaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlUndefinedLiteral cqlUndefinedLiteral)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlUserDefinedFunctionCallScalarExpression cqlUserDefinedFunctionCallScalarExpression)
        {
            this.output.Append("\"Kind\": \"UserDefinedFunctionCall\", ");
            this.output.Append($"\"Identifier\": {{ \"Name\": \"{cqlUserDefinedFunctionCallScalarExpression.Identifier.Name}\" }}, ");
            this.output.Append("\"Arguments\": [ ");
            int count = 0;
            foreach (CqlScalarExpression argument in cqlUserDefinedFunctionCallScalarExpression.Arguments)
            {
                if (count >= 1)
                {
                    this.output.Append(", ");
                }

                count++;
                this.output.Append("{ ");
                argument.Accept(this);
                this.output.Append("}");
            }

            this.output.Append(" ], ");
            this.output.Append($"\"Builtin\": {cqlUserDefinedFunctionCallScalarExpression.Builtin.ToString().ToLower()} ");
        }

        void ICqlVisitor.Visit(CqlVariable cqlVariable)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlVariableRefScalarExpression cqlVariableRefScalarExpression)
        {
            this.output.Append("\"Kind\": \"VariableRef\", ");
            this.output.Append($"\"Variable\": {{ \"Name\": \"{cqlVariableRefScalarExpression.Variable.Name}\", \"UniqueId\": {cqlVariableRefScalarExpression.Variable.UniqueId} }} ");
        }

        void ICqlVisitor.Visit(CqlWhereEnumerableExpression cqlWhereEnumerableExpression)
        {
            this.output.Append("\"Kind\": \"Where\", ");
            this.output.Append("\"DeclaredVariable\": {");
            this.output.Append($"\"Name\": \"{cqlWhereEnumerableExpression.DeclaredVariable.Name}\", ");
            this.output.Append($"\"UniqueId\": {cqlWhereEnumerableExpression.DeclaredVariable.UniqueId} }}, ");
            this.output.Append("\"Expression\": { ");
            cqlWhereEnumerableExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("\"SourceExpression\": { ");
            cqlWhereEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }");
        }
    }
}