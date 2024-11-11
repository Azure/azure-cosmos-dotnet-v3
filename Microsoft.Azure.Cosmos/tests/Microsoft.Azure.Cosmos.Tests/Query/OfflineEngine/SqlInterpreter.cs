//-----------------------------------------------------------------------
// <copyright file="SqlInterpreter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    internal static class SqlInterpreter
    {
        private static readonly CosmosElement[] NoFromClauseDataSource = new CosmosElement[]
        {
            // Single object with a dummy rid 
            CosmosObject.Create(
                new Dictionary<string, CosmosElement>()
                {
                    { "_rid", CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==") }
                })
        };

        public static IEnumerable<CosmosElement> ExecuteQuery(
            IEnumerable<CosmosElement> dataSource,
            SqlQuery sqlQuery,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange = null)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException($"{nameof(dataSource)} must not be null.");
            }

            if (sqlQuery == null)
            {
                throw new ArgumentNullException($"{nameof(sqlQuery)} must not be null.");
            }

            PerformStaticAnalysis(sqlQuery);

            ridToPartitionKeyRange ??= SinglePartitionRidToPartitionKeyRange.Value;

            // From clause binds the data for the rest of the pipeline
            dataSource = sqlQuery.FromClause != null
                ? ExecuteFromClause(
                    dataSource,
                    sqlQuery.FromClause)
                : NoFromClauseDataSource;

            // We execute the filter here to reduce the data set as soon as possible.
            if (sqlQuery.WhereClause != null)
            {
                dataSource = ExecuteWhereClause(
                    dataSource,
                    sqlQuery.WhereClause);
            }

            // We sort before the projection,
            // since the projection might remove the order by items.
            if (sqlQuery.OrderByClause != null)
            {
                dataSource = ExecuteOrderByClause(
                    dataSource,
                    sqlQuery.OrderByClause,
                    ridToPartitionKeyRange);
            }
            else
            {
                // Even for non order by queries we need to order by partition key ranges and document ids
                dataSource = ExecuteCrossPartitionOrdering(
                    dataSource,
                    ridToPartitionKeyRange);
            }

            IEnumerable<IGrouping<GroupByKey, CosmosElement>> groupings;

            // We need to create the groupings at this point for the rest of the pipeline
            if (sqlQuery.GroupByClause != null)
            {
                groupings = ExecuteGroupByClause(
                    dataSource,
                    sqlQuery.GroupByClause);
            }
            else
            {
                groupings = AggregateProjectionDector.HasAggregate(sqlQuery.SelectClause.SelectSpec)
                    ? CreateOneGroupingForWholeCollection(dataSource)
                    : CreateOneGroupingForEachDocument(dataSource);
            }

            // We finally project out the needed columns and remove all binding artifacts
            dataSource = ExecuteSelectClause(
                groupings,
                sqlQuery.SelectClause);

            // Offset limit just performs skip take
            if (sqlQuery.OffsetLimitClause != null)
            {
                dataSource = ExecuteOffsetLimitClause(dataSource, sqlQuery.OffsetLimitClause);
            }

            dataSource = FilterUndefinedProperties(dataSource);

            return dataSource;
        }

        private static void PerformStaticAnalysis(SqlQuery sqlQuery)
        {
            if (sqlQuery.GroupByClause != null)
            {
                GroupByStaticAnalyzer groupByStaticAnalyzer = new GroupByStaticAnalyzer(sqlQuery.GroupByClause.Expressions);
                sqlQuery.SelectClause.SelectSpec.Accept(groupByStaticAnalyzer);
            }
        }

        private static IEnumerable<CosmosElement> FilterUndefinedProperties(IEnumerable<CosmosElement> dataSource)
        {
            return dataSource.Select(x => x.Accept(UndefinedPropertyRemover.Instance));
        }

        private static IEnumerable<CosmosElement> ExecuteSelectClause(
            IEnumerable<IGrouping<GroupByKey, CosmosElement>> groupings,
            SqlSelectClause sqlSelectClause)
        {

            IEnumerable<CosmosElement> dataSource = ProjectOnGroupings(
                groupings,
                sqlSelectClause);

            if (sqlSelectClause.HasDistinct)
            {
                dataSource = dataSource.Distinct();
            }

            if (sqlSelectClause.TopSpec != null)
            {
                CosmosNumber cosmosTopValue = (CosmosNumber)sqlSelectClause.TopSpec.TopExpresion.Accept(
                    ScalarExpressionEvaluator.Singleton,
                    input: null);
                long topCount = Number64.ToLong(cosmosTopValue.Value);
                dataSource = dataSource.Take((int)topCount);
            }

            return dataSource;
        }

        private static IEnumerable<CosmosElement> ProjectOnGroupings(
            IEnumerable<IGrouping<GroupByKey, CosmosElement>> groupings,
            SqlSelectClause sqlSelectClause)
        {
            foreach (IGrouping<GroupByKey, CosmosElement> grouping in groupings)
            {
                IEnumerable<CosmosElement> dataSource = grouping;
                if (AggregateProjectionDector.HasAggregate(sqlSelectClause.SelectSpec))
                {
                    // If there is an aggregate then we need to just project out the one document
                    // But we need to transform the query to first evaluate the aggregate on all the documents
                    AggregateProjectionTransformer aggregateProjectionTransformer = new AggregateProjectionTransformer(dataSource);
                    SqlSelectSpec transformedSpec = aggregateProjectionTransformer
                        .TransformAggregatesInProjection(sqlSelectClause.SelectSpec);
                    CosmosElement aggregationResult = transformedSpec.Accept(
                        Projector.Singleton,
                        dataSource.FirstOrDefault());
                    dataSource = aggregationResult is not CosmosUndefined ? (new CosmosElement[] { aggregationResult }) : (IEnumerable<CosmosElement>)Array.Empty<CosmosElement>();
                }
                else
                {
                    dataSource = dataSource
                        .Select(element => sqlSelectClause.SelectSpec.Accept(
                            Projector.Singleton,
                            element))
                        .Where(projection => projection is not CosmosUndefined);
                }

                if (dataSource.Any())
                {
                    yield return dataSource.First();
                }
            }
        }

        private static IEnumerable<CosmosElement> ExecuteFromClause(
            IEnumerable<CosmosElement> dataSource,
            SqlFromClause sqlFromClause)
        {
            dataSource = sqlFromClause.Expression.Accept(
                DataSourceEvaluator.Singleton,
                dataSource);
            return dataSource;
        }

        private static IEnumerable<CosmosElement> ExecuteWhereClause(
            IEnumerable<CosmosElement> dataSource,
            SqlWhereClause sqlWhereClause)
        {
            return dataSource
                .Where(element =>
                {
                    CosmosElement evaluation = sqlWhereClause.FilterExpression.Accept(
                        ScalarExpressionEvaluator.Singleton,
                        element);
                    return evaluation is CosmosBoolean cosmosBoolean && cosmosBoolean.Value;
                });
        }

        private static IEnumerable<IGrouping<GroupByKey, CosmosElement>> ExecuteGroupByClause(
            IEnumerable<CosmosElement> dataSource,
            SqlGroupByClause sqlGroupByClause)
        {
            return dataSource.GroupBy(
                keySelector: (document) => GetGroupByKey(
                        document,
                        sqlGroupByClause.Expressions),
                comparer: GroupByKeyEqualityComparer.Singleton);
        }

        private static IEnumerable<IGrouping<GroupByKey, CosmosElement>> CreateOneGroupingForEachDocument(
            IEnumerable<CosmosElement> dataSource)
        {
            return dataSource.Select(document => new SingleDocumentGrouping(document));
        }

        private static IEnumerable<IGrouping<GroupByKey, CosmosElement>> CreateOneGroupingForWholeCollection(
            IEnumerable<CosmosElement> dataSource)
        {
            yield return new WholeCollectionGrouping(dataSource);
        }

        private static GroupByKey GetGroupByKey(
            CosmosElement element,
            IReadOnlyList<SqlScalarExpression> groupByExpressions)
        {
            List<CosmosElement> groupByValues = new List<CosmosElement>();
            foreach (SqlScalarExpression groupByExpression in groupByExpressions)
            {
                CosmosElement groupByValue = groupByExpression.Accept(
                    ScalarExpressionEvaluator.Singleton,
                    element);
                groupByValues.Add(groupByValue);
            }

            return new GroupByKey(groupByValues);
        }

        private static IEnumerable<CosmosElement> ExecuteCrossPartitionOrdering(
            IEnumerable<CosmosElement> dataSource,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange)
        {
            // Grab from the left most partition first
            IOrderedEnumerable<CosmosElement> orderedDataSource = dataSource
            .OrderBy((element) =>
            {
                string rid = ((CosmosString)((CosmosObject)element)["_rid"]).Value;
                PartitionKeyRange partitionKeyRange = ridToPartitionKeyRange[rid];
                return partitionKeyRange.MinInclusive;
            },
            StringComparer.Ordinal);

            // Break all final ties within partition by document id
            orderedDataSource = orderedDataSource
                .ThenBy(element => ResourceId.Parse(((CosmosString)((CosmosObject)element)["_rid"]).Value).Database)
                .ThenBy(element => ResourceId.Parse(((CosmosString)((CosmosObject)element)["_rid"]).Value).Document);

            return orderedDataSource;
        }

        private static IEnumerable<CosmosElement> ExecuteOrderByClause(
            IEnumerable<CosmosElement> dataSource,
            SqlOrderByClause sqlOrderByClause,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange)
        {
            // Sort by the columns left to right
            SqlOrderByItem firstItem = sqlOrderByClause.OrderByItems[0];

            // Since we don't supply an explicit index on the policy undefined items don't show up in the sort order
            if (sqlOrderByClause.OrderByItems.Length == 1)
            {
                dataSource = dataSource.Where(element => firstItem.Expression.Accept(
                    ScalarExpressionEvaluator.Singleton,
                    element) is not CosmosUndefined);
            }

            IOrderedEnumerable<CosmosElement> orderedDataSource = firstItem.IsDescending
                ? dataSource.OrderByDescending(
                    element => firstItem.Expression.Accept(
                        ScalarExpressionEvaluator.Singleton,
                        element))
                : dataSource.OrderBy(
                    element => firstItem.Expression.Accept(
                        ScalarExpressionEvaluator.Singleton,
                        element));
            foreach (SqlOrderByItem sqlOrderByItem in sqlOrderByClause.OrderByItems.Skip(1))
            {
                orderedDataSource = sqlOrderByItem.IsDescending
                    ? orderedDataSource.ThenByDescending(
                        element => sqlOrderByItem.Expression.Accept(
                            ScalarExpressionEvaluator.Singleton,
                            element))
                    : orderedDataSource.ThenBy(
                        element => sqlOrderByItem.Expression.Accept(
                            ScalarExpressionEvaluator.Singleton,
                            element));
            }

            // Grab from the left most partition first
            orderedDataSource = orderedDataSource
                .ThenBy((element) =>
                {
                    string rid = ((CosmosString)((CosmosObject)element)["_rid"]).Value;
                    PartitionKeyRange partitionKeyRange = ridToPartitionKeyRange[rid];
                    return partitionKeyRange.MinInclusive;
                },
                StringComparer.Ordinal);

            // Break all final ties within partition by document id
            orderedDataSource = firstItem.IsDescending
                ? orderedDataSource
                    .ThenByDescending(element => ResourceId.Parse(((CosmosString)((CosmosObject)element)["_rid"]).Value).Document)
                : orderedDataSource
                    .ThenBy(element => ResourceId.Parse(((CosmosString)((CosmosObject)element)["_rid"]).Value).Document);

            return orderedDataSource;
        }

        private static IEnumerable<CosmosElement> ExecuteOffsetLimitClause(
            IEnumerable<CosmosElement> dataSource,
            SqlOffsetLimitClause sqlOffsetLimitClause)
        {
            SqlOffsetSpec sqlOffsetSpec = sqlOffsetLimitClause.OffsetSpec;
            if (sqlOffsetSpec != null)
            {
                CosmosNumber cosmosOffsetValue = (CosmosNumber)sqlOffsetSpec.OffsetExpression.Accept(
                    ScalarExpressionEvaluator.Singleton,
                    input: null);
                long offsetCount = Number64.ToLong(cosmosOffsetValue.Value);
                dataSource = dataSource.Skip((int)offsetCount);
            }

            SqlLimitSpec sqlLimitSpec = sqlOffsetLimitClause.LimitSpec;
            if (sqlLimitSpec != null)
            {
                CosmosNumber cosmosLimitValue = (CosmosNumber)sqlLimitSpec.LimitExpression.Accept(
                    ScalarExpressionEvaluator.Singleton,
                    input: null);
                long limitCount = Number64.ToLong(cosmosLimitValue.Value);
                dataSource = dataSource.Take((int)limitCount);
            }

            return dataSource;
        }

        private sealed class GroupByKey
        {
            public GroupByKey(IReadOnlyList<CosmosElement> groupByColums)
            {
                if (groupByColums == null)
                {
                    throw new ArgumentNullException(nameof(groupByColums));
                }

                if (groupByColums.Count == 0)
                {
                    throw new ArgumentException($"{nameof(groupByColums)} must not be empty.");
                }

                this.GroupByColums = groupByColums;
            }

            public IReadOnlyList<CosmosElement> GroupByColums
            {
                get;
            }
        }

        private sealed class SingleDocumentGrouping : IGrouping<GroupByKey, CosmosElement>
        {
            private readonly CosmosElement document;

            public SingleDocumentGrouping(CosmosElement document)
            {
                this.document = document;
                this.Key = null;
            }

            public GroupByKey Key
            {
                get;
            }

            public IEnumerator<CosmosElement> GetEnumerator()
            {
                yield return this.document;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private sealed class WholeCollectionGrouping : IGrouping<GroupByKey, CosmosElement>
        {
            private readonly IEnumerable<CosmosElement> collection;

            public WholeCollectionGrouping(IEnumerable<CosmosElement> collection)
            {
                this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
                this.Key = null;
            }

            public GroupByKey Key
            {
                get;
            }

            public IEnumerator<CosmosElement> GetEnumerator()
            {
                return this.collection.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private sealed class GroupByKeyEqualityComparer : IEqualityComparer<GroupByKey>
        {
            public static readonly GroupByKeyEqualityComparer Singleton = new GroupByKeyEqualityComparer();

            private GroupByKeyEqualityComparer()
            {
            }

            public bool Equals(GroupByKey groupByKey1, GroupByKey groupByKey2)
            {
                if (groupByKey1 == null && groupByKey2 == null)
                {
                    return true;
                }

                if (groupByKey1 == null || groupByKey2 == null)
                {
                    return false;
                }

                if (groupByKey1.GroupByColums.Count != groupByKey2.GroupByColums.Count)
                {
                    return false;
                }

                bool equals = true;
                IEnumerable<Tuple<CosmosElement, CosmosElement>> pairwiseGroupByColumns = groupByKey1.GroupByColums
                    .Zip(
                        groupByKey2.GroupByColums,
                        (first, second) => new Tuple<CosmosElement, CosmosElement>(first, second));
                foreach (Tuple<CosmosElement, CosmosElement> pairwiseGroupByColumn in pairwiseGroupByColumns)
                {
                    CosmosElement columnFromKey1 = pairwiseGroupByColumn.Item1;
                    CosmosElement columnFromKey2 = pairwiseGroupByColumn.Item2;

                    equals &= columnFromKey1 == columnFromKey2;
                }

                return equals;
            }

            public int GetHashCode(GroupByKey groupByKey)
            {
                return 0;
            }
        }

        private sealed class SinglePartitionRidToPartitionKeyRange : IReadOnlyDictionary<string, PartitionKeyRange>
        {
            private static readonly PartitionKeyRange FullRange = new PartitionKeyRange()
            {
                MinInclusive = "0",
                MaxExclusive = "FF",
                Id = "0",
            };

            private static readonly IEnumerable<PartitionKeyRange> MockValues = new PartitionKeyRange[] { FullRange };

            public static readonly SinglePartitionRidToPartitionKeyRange Value = new SinglePartitionRidToPartitionKeyRange();

            public PartitionKeyRange this[string key] => FullRange;

            public IEnumerable<string> Keys => throw new NotImplementedException();

            public IEnumerable<PartitionKeyRange> Values => MockValues;

            public int Count => int.MaxValue;

            public bool ContainsKey(string key)
            {
                return true;
            }

            public IEnumerator<KeyValuePair<string, PartitionKeyRange>> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public bool TryGetValue(string key, out PartitionKeyRange value)
            {
                value = FullRange;
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        private sealed class GroupByStaticAnalyzer : SqlSelectSpecVisitor
        {
            private readonly GroupByProjectionScalarExpressionVisitor groupByProjectionScalarExpressionVisitor;

            public GroupByStaticAnalyzer(IReadOnlyList<SqlScalarExpression> groupByScalarExpressions)
            {
                this.groupByProjectionScalarExpressionVisitor = new GroupByProjectionScalarExpressionVisitor(groupByScalarExpressions);
            }

            public override void Visit(SqlSelectListSpec selectSpec)
            {
                foreach (SqlSelectItem selectItem in selectSpec.Items)
                {
                    if (!selectItem.Expression.Accept(this.groupByProjectionScalarExpressionVisitor))
                    {
                        throw new ArgumentException($"{nameof(SqlSelectClause)} has an expression that is neither a group by expression or aggregate.");
                    }
                }
            }

            public override void Visit(SqlSelectStarSpec selectSpec)
            {
                // SELECT * is always okay.
                return;
            }

            public override void Visit(SqlSelectValueSpec selectSpec)
            {
                if (!selectSpec.Expression.Accept(this.groupByProjectionScalarExpressionVisitor))
                {
                    throw new ArgumentException($"{nameof(SqlSelectClause)} has an expression that is neither a group by expression or aggregate.");
                }
            }

            private sealed class GroupByProjectionScalarExpressionVisitor : SqlScalarExpressionVisitor<bool>
            {
                private static readonly HashSet<SqlIdentifier> AggregateIdentifiers = new HashSet<SqlIdentifier>()
                {
                    SqlFunctionCallScalarExpression.Identifiers.Avg,
                    SqlFunctionCallScalarExpression.Identifiers.Count,
                    SqlFunctionCallScalarExpression.Identifiers.Max,
                    SqlFunctionCallScalarExpression.Identifiers.Min,
                    SqlFunctionCallScalarExpression.Identifiers.Sum
                };

                private readonly HashSet<string> groupByScalarExpressionsStrings;

                public GroupByProjectionScalarExpressionVisitor(IReadOnlyList<SqlScalarExpression> groupByScalarExpressions)
                {
                    if (groupByScalarExpressions == null)
                    {
                        throw new ArgumentNullException(nameof(groupByScalarExpressions));
                    }

                    if (groupByScalarExpressions.Any((expression) => expression == null))
                    {
                        throw new ArgumentException(nameof(groupByScalarExpressions) + "can not have null elements");
                    }

                    this.groupByScalarExpressionsStrings = new HashSet<string>();
                    foreach (SqlScalarExpression sqlScalarExpression in groupByScalarExpressions)
                    {
                        this.groupByScalarExpressionsStrings.Add(sqlScalarExpression.ToString());
                    }
                }

                public override bool Visit(SqlAllScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlArrayCreateScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    foreach (SqlScalarExpression arrayItem in scalarExpression.Items)
                    {
                        if (!arrayItem.Accept(this))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                public override bool Visit(SqlArrayScalarExpression scalarExpression)
                {
                    // We don't allow subqueries in group by projections.
                    return false;
                }

                public override bool Visit(SqlBetweenScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.Expression.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.StartInclusive.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.EndInclusive.Accept(this))
                    {
                        return false;
                    }

                    return true;
                }

                public override bool Visit(SqlBinaryScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.LeftExpression.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.RightExpression.Accept(this))
                    {
                        return false;
                    }

                    return true;
                }

                public override bool Visit(SqlCoalesceScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.Left.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.Right.Accept(this))
                    {
                        return false;
                    }

                    return true;
                }

                public override bool Visit(SqlConditionalScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.Condition.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.Consequent.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.Alternative.Accept(this))
                    {
                        return false;
                    }

                    return true;
                }

                public override bool Visit(SqlExistsScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlFirstScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlFunctionCallScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.IsUdf)
                    {
                        if (AggregateIdentifiers.Contains(scalarExpression.Name))
                        {
                            // If the function call is an aggregate
                            return true;
                        }
                    }

                    foreach (SqlScalarExpression arguments in scalarExpression.Arguments)
                    {
                        if (!arguments.Accept(this))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                public override bool Visit(SqlInScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.Needle.Accept(this))
                    {
                        return false;
                    }

                    foreach (SqlScalarExpression item in scalarExpression.Haystack)
                    {
                        if (!item.Accept(this))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                public override bool Visit(SqlLastScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlLikeScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlLiteralScalarExpression scalarExpression)
                {
                    // Literals don't need to be checked, since they won't reference a non group by column.
                    return true;
                }

                public override bool Visit(SqlMemberIndexerScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    return false;
                }

                public override bool Visit(SqlObjectCreateScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    foreach (SqlObjectProperty sqlObjectProperty in scalarExpression.Properties)
                    {
                        if (!sqlObjectProperty.Value.Accept(this))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                public override bool Visit(SqlParameterRefScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlPropertyRefScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    return false;
                }

                public override bool Visit(SqlSubqueryScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlUnaryScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.Expression.Accept(this))
                    {
                        return false;
                    }

                    return true;
                }

                private bool MatchesGroupByExpression(SqlScalarExpression scalarExpression)
                {
                    // For now we are just doing string matching
                    string toStringOutput = scalarExpression.ToString();
                    foreach (string groupByExpressionToString in this.groupByScalarExpressionsStrings)
                    {
                        if (groupByExpressionToString == toStringOutput)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        private class UndefinedPropertyRemover : ICosmosElementVisitor<CosmosElement>
        {
            public static UndefinedPropertyRemover Instance = new UndefinedPropertyRemover();

            private UndefinedPropertyRemover()
            {
            }

            public CosmosElement Visit(CosmosArray cosmosArray)
            {
                List<CosmosElement> items = new List<CosmosElement>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    CosmosElement item = arrayItem.Accept(this);
                    if (item is not CosmosUndefined)
                    {
                        items.Add(item);
                    }
                }

                return CosmosArray.Create(items);
            }

            public CosmosElement Visit(CosmosBinary cosmosBinary)
            {
                return cosmosBinary;
            }

            public CosmosElement Visit(CosmosBoolean cosmosBoolean)
            {
                return cosmosBoolean;
            }

            public CosmosElement Visit(CosmosGuid cosmosGuid)
            {
                return cosmosGuid;
            }

            public CosmosElement Visit(CosmosNull cosmosNull)
            {
                return cosmosNull;
            }

            public CosmosElement Visit(CosmosNumber cosmosNumber)
            {
                return cosmosNumber;
            }

            public CosmosElement Visit(CosmosObject cosmosObject)
            {
                Dictionary<string, CosmosElement> properties = new Dictionary<string, CosmosElement>();
                foreach (KeyValuePair<string, CosmosElement> property in cosmosObject)
                {
                    CosmosElement value = property.Value.Accept(this);
                    if (value is not CosmosUndefined)
                    {
                        properties.Add(property.Key, value);
                    }
                }

                return CosmosObject.Create(properties);
            }

            public CosmosElement Visit(CosmosString cosmosString)
            {
                return cosmosString;
            }

            public CosmosElement Visit(CosmosUndefined cosmosUndefined)
            {
                return cosmosUndefined;
            }
        }
    }
}