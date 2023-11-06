//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan
{
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;

    internal class ClientDistributionPipelineFactory : ICqlVisitor
    { 
        private readonly ICqlVisitor visitor;
        // private MonadicCreatePipelineStage monadicCreatePipelineStage;

        public ClientDistributionPipelineFactory(ICqlVisitor visitor)
        {
            this.visitor = visitor;
            // initialize a parallel pipeline here which becomes the source pipeline to the first pipeline called here
            /*
              monadicCreatePipelineStage = (continuationToken, cancellationToken) => ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                  documentContainer: documentContainer,
                  sqlQuerySpec: sqlQuerySpec,
                  targetRanges: targetRanges,
                  queryPaginationOptions: queryPaginationOptions,
                  partitionKey: partitionKey,
                  prefetchPolicy: prefetchPolicy,
                  maxConcurrency: maxConcurrency,
                  continuationToken: continuationToken,
                  cancellationToken: cancellationToken);
             */
        }

        public void Visit(CqlAggregate cqlAggregate)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlAggregateEnumerableExpression cqlAggregateEnumerableExpression)
        {
            // One complication: To create any Enumerable pipeline, it needs the base of parallel pipeline or order by pipeline
            // call Aggregate pipeline
            /*MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
            monadicCreatePipelineStage = (continuationToken, cancellationToken) => AggregateQueryPipelineStage.MonadicCreate(
                executionEnvironment,
                queryInfo.Aggregates,
                queryInfo.GroupByAliasToAggregateType,
                queryInfo.GroupByAliases,
                queryInfo.HasSelectValue,
                continuationToken,
                cancellationToken,
                monadicCreateSourceStage);*/
        }

        public void Visit(CqlAggregateKind cqlAggregateKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlAggregateOperatorKind cqlAggregateOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlArrayCreateScalarExpression cqlArrayCreateScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlArrayIndexerScalarExpression cqlArrayIndexerScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlArrayLiteral cqlArrayLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlBinaryScalarExpression cqlBinaryScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlBinaryScalarOperatorKind cqlBinaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlBooleanLiteral cqlBooleanLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlBuiltinAggregate cqlBuiltinAggregate)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlBuiltinScalarFunctionKind cqlBuiltinScalarFunctionKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlDistinctEnumerableExpression cqlDistinctEnumerableExpression)
        {
            // Call distinct pipeline
        }

        public void Visit(CqlEnumerableExpression cqlEnumerableExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlEnumerableExpressionKind cqlEnumerableExpressionKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlEnumerationKind cqlEnumerationKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlFunctionIdentifier cqlFunctionIdentifier)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlGroupByEnumerableExpression cqlGroupByEnumerableExpression)
        {
            // Call Groupby pipeline
        }

        public void Visit(CqlInputEnumerableExpression cqlInputEnumerableExpression)
        {
            // Get data from backend
        }

        public void Visit(CqlIsOperatorKind cqlIsOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlIsOperatorScalarExpression cqlIsOperatorScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlLetScalarExpression cqlLetScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlLiteral cqlLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlLiteralKind cqlLiteralKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlLiteralScalarExpression cqlLiteralScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlMuxScalarExpression cqlMuxScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlNullLiteral cqlNullLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlNumberLiteral cqlNumberLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlObjectCreateScalarExpression cqlObjectCreateScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlObjectLiteral cqlObjectLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlObjectLiteralProperty cqlObjectLiteralProperty)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlObjectProperty cqlObjectProperty)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlOrderByEnumerableExpression cqlOrderByEnumerableExpression)
        {
            // call order by pipeline
        }

        public void Visit(CqlOrderByItem cqlOrderByItem)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlPropertyRefScalarExpression cqlPropertyRefScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlScalarAsEnumerableExpression cqlScalarAsEnumerableExpression)
        {
            // call scalar as enumerable pipeline
        }

        public void Visit(CqlScalarExpression cqlScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlScalarExpressionKind cqlScalarExpressionKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlSelectEnumerableExpression cqlSelectEnumerableExpression)
        {
            // call select pipeline
        }

        public void Visit(CqlSelectManyEnumerableExpression cqlSelectManyEnumerableExpression)
        {
            // call select many pipeline
        }

        public void Visit(CqlSortOrder cqlSortOrder)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlStringLiteral cqlStringLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlSystemFunctionCallScalarExpression cqlSystemFunctionCallScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlTakeEnumerableExpression cqlTakeEnumerableExpression)
        {
            // call take pipeline
        }

        public void Visit(CqlTupleAggregate cqlTupleAggregate)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlTupleCreateScalarExpression cqlTupleCreateScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlTupleItemRefScalarExpression cqlTupleItemRefScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlUnaryScalarExpression cqlUnaryScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlUnaryScalarOperatorKind cqlUnaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlUndefinedLiteral cqlUndefinedLiteral)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlUserDefinedFunctionCallScalarExpression cqlUserDefinedFunctionCallScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlVariable cqlVariable)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlVariableRefScalarExpression cqlVariableRefScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(CqlWhereEnumerableExpression cqlWhereEnumerableExpression)
        {
            // Call where pipeline
        }
    }
}