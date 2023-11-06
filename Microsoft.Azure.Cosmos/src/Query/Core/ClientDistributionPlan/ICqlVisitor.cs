//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan
{
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql;

    internal interface ICqlVisitor
    {
        void Visit(CqlAggregate cqlAggregate);
        void Visit(CqlAggregateEnumerableExpression cqlAggregateEnumerableExpression);
        void Visit(CqlAggregateKind cqlAggregateKind);
        void Visit(CqlAggregateOperatorKind cqlAggregateOperatorKind);
        void Visit(CqlArrayCreateScalarExpression cqlArrayCreateScalarExpression);
        void Visit(CqlArrayIndexerScalarExpression cqlArrayIndexerScalarExpression);
        void Visit(CqlArrayLiteral cqlArrayLiteral);
        void Visit(CqlBinaryScalarExpression cqlBinaryScalarExpression);
        void Visit(CqlBinaryScalarOperatorKind cqlBinaryScalarOperatorKind);
        void Visit(CqlBooleanLiteral cqlBooleanLiteral);
        void Visit(CqlBuiltinAggregate cqlBuiltinAggregate);
        void Visit(CqlBuiltinScalarFunctionKind cqlBuiltinScalarFunctionKind);
        void Visit(CqlDistinctEnumerableExpression cqlDistinctEnumerableExpression);
        void Visit(CqlEnumerableExpression cqlEnumerableExpression);
        void Visit(CqlEnumerableExpressionKind cqlEnumerableExpressionKind);
        void Visit(CqlEnumerationKind cqlEnumerationKind);
        void Visit(CqlFunctionIdentifier cqlFunctionIdentifier);
        void Visit(CqlGroupByEnumerableExpression cqlGroupByEnumerableExpression);
        void Visit(CqlInputEnumerableExpression cqlInputEnumerableExpression);
        void Visit(CqlIsOperatorKind cqlIsOperatorKind);
        void Visit(CqlIsOperatorScalarExpression cqlIsOperatorScalarExpression);
        void Visit(CqlLetScalarExpression cqlLetScalarExpression);
        void Visit(CqlLiteral cqlLiteral);
        void Visit(CqlLiteralKind cqlLiteralKind);
        void Visit(CqlLiteralScalarExpression cqlLiteralScalarExpression);
        void Visit(CqlMuxScalarExpression cqlMuxScalarExpression);
        void Visit(CqlNullLiteral cqlNullLiteral);
        void Visit(CqlNumberLiteral cqlNumberLiteral);
        void Visit(CqlObjectCreateScalarExpression cqlObjectCreateScalarExpression);
        void Visit(CqlObjectLiteral cqlObjectLiteral);
        void Visit(CqlObjectLiteralProperty cqlObjectLiteralProperty);
        void Visit(CqlObjectProperty cqlObjectProperty);
        void Visit(CqlOrderByEnumerableExpression cqlOrderByEnumerableExpression);
        void Visit(CqlOrderByItem cqlOrderByItem);
        void Visit(CqlPropertyRefScalarExpression cqlPropertyRefScalarExpression);
        void Visit(CqlScalarAsEnumerableExpression cqlScalarAsEnumerableExpression);
        void Visit(CqlScalarExpression cqlScalarExpression);
        void Visit(CqlScalarExpressionKind cqlScalarExpressionKind);
        void Visit(CqlSelectEnumerableExpression cqlSelectEnumerableExpression);
        void Visit(CqlSelectManyEnumerableExpression cqlSelectManyEnumerableExpression);
        void Visit(CqlSortOrder cqlSortOrder);
        void Visit(CqlStringLiteral cqlStringLiteral);
        void Visit(CqlSystemFunctionCallScalarExpression cqlSystemFunctionCallScalarExpression);
        void Visit(CqlTakeEnumerableExpression cqlTakeEnumerableExpression);
        void Visit(CqlTupleAggregate cqlTupleAggregate);
        void Visit(CqlTupleCreateScalarExpression cqlTupleCreateScalarExpression);
        void Visit(CqlTupleItemRefScalarExpression cqlTupleItemRefScalarExpression);
        void Visit(CqlUnaryScalarExpression cqlUnaryScalarExpression);
        void Visit(CqlUnaryScalarOperatorKind cqlUnaryScalarOperatorKind);
        void Visit(CqlUndefinedLiteral cqlUndefinedLiteral);
        void Visit(CqlUserDefinedFunctionCallScalarExpression cqlUserDefinedFunctionCallScalarExpression);
        void Visit(CqlVariable cqlVariable);
        void Visit(CqlVariableRefScalarExpression cqlVariableRefScalarExpression);
        void Visit(CqlWhereEnumerableExpression cqlWhereEnumerableExpression);
    }
}