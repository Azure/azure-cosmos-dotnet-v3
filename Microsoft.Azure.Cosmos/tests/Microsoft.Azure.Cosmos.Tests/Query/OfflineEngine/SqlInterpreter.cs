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
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;

    internal static class SqlInterpreter
    {
        private static readonly JToken Undefined = null;

        private static readonly JToken[] NoFromClauseDataSource = new JToken[]
        {
            // Single object with a dummy rid 
            new JObject
            {
                ["_rid"] = "AYIMAMmFOw8YAAAAAAAAAA=="
            }
        };

        public static IEnumerable<JToken> ExecuteQuery(
            IEnumerable<JToken> dataSource,
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

            if (ridToPartitionKeyRange == null)
            {
                ridToPartitionKeyRange = SinglePartitionRidToPartitionKeyRange.Value;
            }

            // From clause binds the data for the rest of the pipeline
            if (sqlQuery.FromClause != null)
            {
                dataSource = ExecuteFromClause(
                    dataSource,
                    sqlQuery.FromClause,
                    collectionConfigurations);
            }
            else
            {
                dataSource = NoFromClauseDataSource;
            }

            // We execute the filter here to reduce the data set as soon as possible.
            if (sqlQuery.WhereClause != null)
            {
                dataSource = ExecuteWhereClause(
                    dataSource,
                    sqlQuery.WhereClause,
                    collectionConfigurations);
            }

            // We sort before the projection,
            // since the projection might remove the order by items.
            if (sqlQuery.OrderbyClause != null)
            {
                dataSource = ExecuteOrderByClause(
                    dataSource,
                    sqlQuery.OrderbyClause,
                    ridToPartitionKeyRange,
                    collectionConfigurations);
            }
            else
            {
                // Even for non order by queries we need to order by partition key ranges and document ids
                dataSource = ExecuteCrossPartitionOrdering(
                    dataSource,
                    ridToPartitionKeyRange);
            }

            IEnumerable<IGrouping<GroupByKey, JToken>> groupings;

            // We need to create the groupings at this point for the rest of the pipeline
            if (sqlQuery.GroupByClause != null)
            {
                groupings = ExecuteGroupByClause(
                    dataSource,
                    sqlQuery.GroupByClause,
                    collectionConfigurations);
            }
            else
            {
                if (AggregateProjectionDector.HasAggregate(sqlQuery.SelectClause.SelectSpec))
                {
                    groupings = CreateOneGroupingForWholeCollection(dataSource);
                }
                else
                {
                    groupings = CreateOneGroupingForEachDocument(dataSource);
                }
            }

            // We finally project out the needed columns and remove all binding artifacts
            dataSource = ExecuteSelectClause(
                groupings,
                sqlQuery.SelectClause,
                collectionConfigurations);

            // Offset limit just performs skip take
            if (sqlQuery.OffsetLimitClause != null)
            {
                dataSource = ExecuteOffsetLimitClause(dataSource, sqlQuery.OffsetLimitClause);
            }

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

        private static IEnumerable<JToken> ExecuteSelectClause(
            IEnumerable<IGrouping<GroupByKey, JToken>> groupings,
            SqlSelectClause sqlSelectClause,
            CollectionConfigurations collectionConfigurations)
        {

            IEnumerable<JToken> dataSource = ProjectOnGroupings(
                groupings,
                sqlSelectClause,
                collectionConfigurations);

            if (sqlSelectClause.HasDistinct)
            {
                dataSource = dataSource
                    .Distinct(JsonTokenEqualityComparer.Value);
            }

            if (sqlSelectClause.TopSpec != null)
            {
                dataSource = dataSource
                    .Take((int)sqlSelectClause.TopSpec.Count);
            }

            return dataSource;
        }

        private static IEnumerable<JToken> ProjectOnGroupings(
            IEnumerable<IGrouping<GroupByKey, JToken>> groupings,
            SqlSelectClause sqlSelectClause,
            CollectionConfigurations collectionConfigurations)
        {
            foreach (IGrouping<GroupByKey, JToken> grouping in groupings)
            {
                IEnumerable<JToken> dataSource = grouping;
                if (AggregateProjectionDector.HasAggregate(sqlSelectClause.SelectSpec))
                {
                    // If there is an aggregate then we need to just project out the one document
                    // But we need to transform the query to first evaluate the aggregate on all the documents
                    AggregateProjectionTransformer aggregateProjectionTransformer = new AggregateProjectionTransformer(
                        dataSource,
                        collectionConfigurations);
                    SqlSelectSpec transformedSpec = aggregateProjectionTransformer.TransformAggregatesInProjection(sqlSelectClause.SelectSpec);
                    JToken aggregationResult = transformedSpec.Accept(
                        Projector.Create(collectionConfigurations),
                        dataSource.FirstOrDefault());
                    if (aggregationResult != null)
                    {
                        dataSource = new JToken[] { aggregationResult };
                    }
                    else
                    {
                        dataSource = Array.Empty<JToken>();
                    }
                }
                else
                {
                    dataSource = dataSource
                        .Select(element => sqlSelectClause.SelectSpec.Accept(
                            Projector.Create(collectionConfigurations),
                            element))
                        .Where(projection => projection != Undefined);
                }

                if (dataSource.Any())
                {
                    yield return dataSource.First();
                }
            }
        }

        private static IEnumerable<JToken> ExecuteFromClause(
            IEnumerable<JToken> dataSource,
            SqlFromClause sqlFromClause,
            CollectionConfigurations collectionConfigurations)
        {
            dataSource = sqlFromClause.Expression.Accept(
                DataSourceEvaluator.Create(collectionConfigurations),
                dataSource);
            return dataSource;
        }

        private static IEnumerable<JToken> ExecuteWhereClause(
            IEnumerable<JToken> dataSource,
            SqlWhereClause sqlWhereClause,
            CollectionConfigurations collectionConfigurations)
        {
            return dataSource
                .Where(element =>
                {
                    JToken evaluation = sqlWhereClause.FilterExpression.Accept(
                        ScalarExpressionEvaluator.Create(collectionConfigurations),
                        element);
                    return Utils.IsTrue(evaluation);
                });
        }

        private static IEnumerable<IGrouping<GroupByKey, JToken>> ExecuteGroupByClause(
            IEnumerable<JToken> dataSource,
            SqlGroupByClause sqlGroupByClause,
            CollectionConfigurations collectionConfigurations)
        {
            return dataSource.GroupBy(
                keySelector: (document) =>
                {
                    return GetGroupByKey(
                        document,
                        sqlGroupByClause.Expressions,
                        collectionConfigurations);
                },
                comparer: GroupByKeyEqualityComparer.Singleton);
        }

        private static IEnumerable<IGrouping<GroupByKey, JToken>> CreateOneGroupingForEachDocument(
            IEnumerable<JToken> dataSource)
        {
            return dataSource.Select(document => new SingleDocumentGrouping(document));
        }

        private static IEnumerable<IGrouping<GroupByKey, JToken>> CreateOneGroupingForWholeCollection(
            IEnumerable<JToken> dataSource)
        {
            yield return new WholeCollectionGrouping(dataSource);
        }

        private static GroupByKey GetGroupByKey(
            JToken element,
            IReadOnlyList<SqlScalarExpression> groupByExpressions,
            CollectionConfigurations collectionConfigurations)
        {
            List<JToken> groupByValues = new List<JToken>();
            foreach (SqlScalarExpression groupByExpression in groupByExpressions)
            {
                JToken groupByValue = groupByExpression.Accept(
                    ScalarExpressionEvaluator.Create(collectionConfigurations),
                    element);
                groupByValues.Add(groupByValue);
            }

            return new GroupByKey(groupByValues);
        }

        private static IEnumerable<JToken> ExecuteCrossPartitionOrdering(
            IEnumerable<JToken> dataSource,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange)
        {
            // Grab from the left most partition first
            IOrderedEnumerable<JToken> orderedDataSource = dataSource
            .OrderBy((element) =>
            {
                string rid = (string)element["_rid"];
                PartitionKeyRange partitionKeyRange = ridToPartitionKeyRange[rid];
                return partitionKeyRange.MinInclusive;
            },
            StringComparer.Ordinal);

            // Break all final ties within partition by document id
            orderedDataSource = orderedDataSource
                .ThenBy(element => ResourceId.Parse((string)element["_rid"]).Document);

            return orderedDataSource;
        }

        private static IEnumerable<JToken> ExecuteOrderByClause(
            IEnumerable<JToken> dataSource,
            SqlOrderbyClause sqlOrderByClause,
            IReadOnlyDictionary<string, PartitionKeyRange> ridToPartitionKeyRange,
            CollectionConfigurations collectionConfigurations)
        {
            // Sort by the columns left to right
            SqlOrderByItem firstItem = sqlOrderByClause.OrderbyItems[0];

            // Since we don't supply an explicit index on the policy undefined items don't show up in the sort order
            if (sqlOrderByClause.OrderbyItems.Count == 1)
            {
                dataSource = dataSource.Where(element => firstItem.Expression.Accept(
                    ScalarExpressionEvaluator.Create(collectionConfigurations),
                    element) != Undefined);
            }

            IOrderedEnumerable<JToken> orderedDataSource;
            if (firstItem.IsDescending)
            {
                orderedDataSource = dataSource.OrderByDescending(
                    element => firstItem.Expression.Accept(
                        ScalarExpressionEvaluator.Create(collectionConfigurations),
                        element),
                    JTokenComparer.Singleton);
            }
            else
            {
                orderedDataSource = dataSource.OrderBy(
                    element => firstItem.Expression.Accept(
                        ScalarExpressionEvaluator.Create(collectionConfigurations),
                        element),
                    JTokenComparer.Singleton);
            }

            foreach (SqlOrderByItem sqlOrderByItem in sqlOrderByClause.OrderbyItems.Skip(1))
            {
                if (sqlOrderByItem.IsDescending)
                {
                    orderedDataSource = orderedDataSource.ThenByDescending(
                        element => sqlOrderByItem.Expression.Accept(
                            ScalarExpressionEvaluator.Create(collectionConfigurations),
                            element),
                        JTokenComparer.Singleton);
                }
                else
                {
                    orderedDataSource = orderedDataSource.ThenBy(
                        element => sqlOrderByItem.Expression.Accept(
                            ScalarExpressionEvaluator.Create(collectionConfigurations),
                            element),
                        JTokenComparer.Singleton);
                }
            }

            // Grab from the left most partition first
            orderedDataSource = orderedDataSource
                .ThenBy((element) =>
                {
                    string rid = (string)element["_rid"];
                    PartitionKeyRange partitionKeyRange = ridToPartitionKeyRange[rid];
                    return partitionKeyRange.MinInclusive;
                },
                StringComparer.Ordinal);

            // Break all final ties within partition by document id
            if (firstItem.IsDescending)
            {
                orderedDataSource = orderedDataSource
                    .ThenByDescending(element => ResourceId.Parse((string)element["_rid"]).Document);
            }
            else
            {
                orderedDataSource = orderedDataSource
                    .ThenBy(element => ResourceId.Parse((string)element["_rid"]).Document);
            }

            return orderedDataSource;
        }

        private static IEnumerable<JToken> ExecuteOffsetLimitClause(
            IEnumerable<JToken> dataSource,
            SqlOffsetLimitClause sqlOffsetLimitClause)
        {
            SqlOffsetSpec sqlOffsetSpec = sqlOffsetLimitClause.OffsetSpec;
            if (sqlOffsetSpec != null)
            {
                dataSource = dataSource.Skip((int)sqlOffsetSpec.Offset);
            }

            SqlLimitSpec sqlLimitSpec = sqlOffsetLimitClause.LimitSpec;
            if (sqlLimitSpec != null)
            {
                dataSource = dataSource.Take((int)sqlLimitSpec.Limit);
            }

            return dataSource;
        }

        private sealed class GroupByKey
        {
            public GroupByKey(IReadOnlyList<JToken> groupByColums)
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

            public IReadOnlyList<JToken> GroupByColums
            {
                get;
            }
        }

        private sealed class SingleDocumentGrouping : IGrouping<GroupByKey, JToken>
        {
            private readonly JToken document;

            public SingleDocumentGrouping(JToken document)
            {
                this.document = document;
                this.Key = null;
            }

            public GroupByKey Key
            {
                get;
            }

            public IEnumerator<JToken> GetEnumerator()
            {
                yield return this.document;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private sealed class WholeCollectionGrouping : IGrouping<GroupByKey, JToken>
        {
            private readonly IEnumerable<JToken> collection;

            public WholeCollectionGrouping(IEnumerable<JToken> collection)
            {
                if (collection == null)
                {
                    throw new ArgumentNullException(nameof(collection));
                }

                this.collection = collection;
                this.Key = null;
            }

            public GroupByKey Key
            {
                get;
            }

            public IEnumerator<JToken> GetEnumerator()
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
                IEnumerable<Tuple<JToken, JToken>> pairwiseGroupByColumns = groupByKey1.GroupByColums
                    .Zip(
                        groupByKey2.GroupByColums,
                        (first, second) => new Tuple<JToken, JToken>(first, second));
                foreach (Tuple<JToken, JToken> pairwiseGroupByColumn in pairwiseGroupByColumns)
                {
                    JToken columnFromKey1 = pairwiseGroupByColumn.Item1;
                    JToken columnFromKey2 = pairwiseGroupByColumn.Item2;

                    equals &= JsonTokenEqualityComparer.Value.Equals(columnFromKey1, columnFromKey2);
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

                public override bool Visit(SqlBinaryScalarExpression scalarExpression)
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

                    if (!scalarExpression.FirstExpression.Accept(this))
                    {
                        return false;
                    }

                    if (!scalarExpression.SecondExpression.Accept(this))
                    {
                        return false;
                    }

                    return true;
                }

                public override bool Visit(SqlConversionScalarExpression scalarExpression)
                {
                    throw new NotImplementedException();
                }

                public override bool Visit(SqlExistsScalarExpression scalarExpression)
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

                public override bool Visit(SqlGeoNearCallScalarExpression scalarExpression)
                {
                    throw new NotImplementedException();
                }

                public override bool Visit(SqlInScalarExpression scalarExpression)
                {
                    if (this.MatchesGroupByExpression(scalarExpression))
                    {
                        return true;
                    }

                    if (!scalarExpression.Expression.Accept(this))
                    {
                        return false;
                    }

                    foreach (SqlScalarExpression item in scalarExpression.Items)
                    {
                        if (!item.Accept(this))
                        {
                            return false;
                        }
                    }

                    return true;
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
                        if (!sqlObjectProperty.Expression.Accept(this))
                        {
                            return false;
                        }
                    }

                    return true;
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
    }
}
