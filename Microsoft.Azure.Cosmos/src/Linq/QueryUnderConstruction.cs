//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#define SUPPORT_SUBQUERIES

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using static FromParameterBindings;
    using static Microsoft.Azure.Cosmos.Linq.ExpressionToSql;

    /// <summary>
    /// Query that is being constructed.
    /// </summary>
    internal sealed class QueryUnderConstruction
    {
        // The SQLQuery class does not maintain enough information for optimizations.
        // so this class is a replacement which in the end should produce a SQLQuery.

        /// <summary>
        /// Binding for the FROM parameters.
        /// </summary>
        public FromParameterBindings FromParameters
        {
            get;
            set;
        }

        /// <summary>
        /// Binding for the Group By clause.
        /// </summary>
        public FromParameterBindings GroupByParameter
        {
            get;
            set;
        }

        /// <summary>
        /// The parameter expression to be used as this query's alias.
        /// </summary>
        public ParameterExpression Alias
        {
            get
            {
                return this.alias.Value;
            }
        }

        private readonly Func<string, ParameterExpression> aliasCreatorFunc;

        public const string DefaultSubqueryRoot = "r";

        private SqlSelectClause selectClause;
        private SqlWhereClause whereClause;
        private SqlOrderByClause orderByClause;
        private SqlGroupByClause groupByClause;

        // The specs could be in clauses to reflect the SqlQuery.
        // However, they are separated to avoid update recreation of the readonly DOMs and lengthy code.
        private SqlTopSpec topSpec;
        private SqlOffsetSpec offsetSpec;
        private SqlLimitSpec limitSpec;

        private Lazy<ParameterExpression> alias;

        /// <summary>
        /// Input subquery / query to the left of the current query. 
        /// </summary>
        private QueryUnderConstruction inputQuery;

        public QueryUnderConstruction(Func<string, ParameterExpression> aliasCreatorFunc)
            : this(aliasCreatorFunc, inputQuery: null)
        {
        }

        public QueryUnderConstruction(Func<string, ParameterExpression> aliasCreatorFunc, QueryUnderConstruction inputQuery)
        {
            this.FromParameters = new FromParameterBindings();
            this.aliasCreatorFunc = aliasCreatorFunc;
            this.inputQuery = inputQuery;
            this.alias = new Lazy<ParameterExpression>(() => aliasCreatorFunc(QueryUnderConstruction.DefaultSubqueryRoot));
        }

        public void Bind(ParameterExpression parameter, SqlCollection collection)
        {
            this.AddBinding(new FromParameterBindings.Binding(parameter, collection, isInCollection: true));
        }

        public void AddBinding(Binding binding)
        {
            this.FromParameters.Add(binding);
        }

        public ParameterExpression GetInputParameterInContext(bool isInNewQuery)
        {
            return isInNewQuery ? this.Alias : this.FromParameters.GetInputParameter();
        }

        /// <summary>
        /// Create a FROM clause from a set of FROM parameter bindings.
        /// </summary>
        /// <returns>The created FROM clause.</returns>
        private SqlFromClause CreateFromClause(SqlCollectionExpression inputCollectionExpression)
        {
            bool first = true;
            foreach (Binding paramDef in this.FromParameters.GetBindings())
            {
                // If input collection expression is provided, the first binding,
                // which is the input paramter name, should be omitted.
                if (first)
                {
                    first = false;
                    if (inputCollectionExpression != null) continue;
                }

                ParameterExpression parameter = paramDef.Parameter;
                SqlCollection paramBinding = paramDef.ParameterDefinition;

                SqlIdentifier identifier = SqlIdentifier.Create(parameter.Name);
                SqlCollectionExpression collExpr;
                if (!paramDef.IsInCollection)
                {
                    SqlCollection collection = paramBinding ?? SqlInputPathCollection.Create(identifier, null);
                    SqlIdentifier alias = paramBinding == null ? null : identifier;
                    collExpr = SqlAliasedCollectionExpression.Create(collection, alias);
                }
                else
                {
                    collExpr = SqlArrayIteratorCollectionExpression.Create(identifier, paramBinding);
                }

                if (inputCollectionExpression != null)
                {
                    inputCollectionExpression = SqlJoinCollectionExpression.Create(inputCollectionExpression, collExpr);
                }
                else
                {
                    inputCollectionExpression = collExpr;
                }
            }

            SqlFromClause fromClause = SqlFromClause.Create(inputCollectionExpression);
            return fromClause;
        }

        private SqlFromClause CreateSubqueryFromClause()
        {
            SqlQuery subquery = this.inputQuery.GetSqlQuery();
            SqlSubqueryCollection collection = SqlSubqueryCollection.Create(subquery);
            ParameterExpression inputParam = this.inputQuery.Alias;
            SqlIdentifier identifier = SqlIdentifier.Create(inputParam.Name);
            SqlAliasedCollectionExpression colExp = SqlAliasedCollectionExpression.Create(collection, identifier);
            SqlFromClause fromClause = this.CreateFromClause(colExp);
            return fromClause;
        }

        /// <summary>
        /// Convert the entire query to a SQL Query.
        /// </summary>
        /// <returns>The corresponding SQL Query.</returns>
        public SqlQuery GetSqlQuery()
        {
            SqlFromClause fromClause;
            if (this.inputQuery != null)
            {
#if SUPPORT_SUBQUERIES

                fromClause = this.CreateSubqueryFromClause();
#else
                throw new DocumentQueryException("SQL subqueries currently not supported");
#endif
            }
            else
            {
                fromClause = this.CreateFromClause(inputCollectionExpression: null);
            }

            // Create a SqlSelectClause with the topSpec.
            // If the query is flatten then selectClause may have a topSpec. It should be taken in that case.
            // If the query doesn't have a selectClause, use SELECT v0 where v0 is the input param.
            SqlSelectClause selectClause = this.selectClause;
            if (selectClause == null)
            {
                string parameterName = this.FromParameters.GetInputParameter().Name;
                SqlScalarExpression parameterExpression = SqlPropertyRefScalarExpression.Create(null, SqlIdentifier.Create(parameterName));
                selectClause = this.selectClause = SqlSelectClause.Create(SqlSelectValueSpec.Create(parameterExpression));
            }
            selectClause = SqlSelectClause.Create(selectClause.SelectSpec, selectClause.TopSpec ?? this.topSpec, selectClause.HasDistinct);
            SqlOffsetLimitClause offsetLimitClause = (this.offsetSpec != null) ?
                SqlOffsetLimitClause.Create(this.offsetSpec, this.limitSpec ?? SqlLimitSpec.Create(SqlNumberLiteral.Create(int.MaxValue))) :
                offsetLimitClause = default(SqlOffsetLimitClause);
            SqlQuery result = SqlQuery.Create(selectClause, fromClause, this.whereClause, this.groupByClause, this.orderByClause, offsetLimitClause);
            return result;
        }

        /// <summary>
        /// Create a new QueryUnderConstruction node that take the current query as its input
        /// </summary>
        /// <param name="inScope">The current context's parameters scope</param>
        /// <returns>The new query node</returns>
        public QueryUnderConstruction PackageQuery(HashSet<ParameterExpression> inScope)
        {
            QueryUnderConstruction result = new QueryUnderConstruction(this.aliasCreatorFunc);
            result.FromParameters.SetInputParameter(typeof(object), this.Alias.Name, inScope);
            result.inputQuery = this;
            return result;
        }

        /// <summary>
        /// Find and flatten the prefix set of queries into a single query by substituting their expressions.
        /// </summary>
        /// <returns>The query that has been flatten</returns>
        public QueryUnderConstruction FlattenAsPossible()
        {
            // Flatten should be done when the current query can be translated without the need of using sub query
            // The cases that need to use sub query are: 
            //     1. Select clause appears after Distinct
            //     2. There are any operations after Take that is not a pure Select.
            //     3. There are nested Select, Where or OrderBy
            //     4. Group by clause appears after Select
            QueryUnderConstruction parentQuery = null;
            QueryUnderConstruction flattenQuery = null;
            bool seenSelect = false;
            bool seenAnyNonSelectOp = false;
            for (QueryUnderConstruction query = this; query != null; query = query.inputQuery)
            {
                foreach (Binding binding in query.FromParameters.GetBindings())
                {
                    if ((binding.ParameterDefinition != null) && (binding.ParameterDefinition is SqlSubqueryCollection))
                    {
                        flattenQuery = this;
                        break;
                    }
                }

                // In Select -> SelectMany cases, fromParameter substitution is not yet supported .
                // Therefore these are un-flattenable.
                if (query.inputQuery != null &&
                    (query.FromParameters.GetBindings().First().Parameter.Name == query.inputQuery.Alias.Name) &&
                    query.FromParameters.GetBindings().Any(b => b.ParameterDefinition != null))
                {
                    flattenQuery = this;
                    break;
                }

                // In case of Select/Order By -> Group by cases, the Select/Order By query should not be flattened and kept as a subquery
                if (((query.inputQuery?.selectClause != null) || (query.inputQuery?.orderByClause != null)) && (query.groupByClause != null))
                {
                    flattenQuery = this;
                    break;
                }

                if (flattenQuery != null) break;

                if (((query.topSpec != null || query.offsetSpec != null || query.limitSpec != null) && seenAnyNonSelectOp) ||
                    (query.selectClause != null && query.selectClause.HasDistinct && seenSelect))
                {
                    parentQuery.inputQuery = query.FlattenAsPossible();
                    flattenQuery = this;
                    break;
                }

                seenSelect = seenSelect || ((query.selectClause != null) && !query.selectClause.HasDistinct);
                seenAnyNonSelectOp |=
                    (query.whereClause != null) ||
                    (query.orderByClause != null) ||
                    (query.groupByClause != null) ||    
                    (query.topSpec != null) ||
                    (query.offsetSpec != null) ||
                    query.FromParameters.GetBindings().Any(b => b.ParameterDefinition != null) ||
                    ((query.selectClause != null) && (query.selectClause.HasDistinct || 
                    this.HasSelectAggregate()));
                parentQuery = query;
            }

            if (flattenQuery == null) flattenQuery = this.Flatten();

            return flattenQuery;
        }

        /// <summary>
        /// Flatten subqueries into a single query by substituting their expressions in the current query.
        /// </summary>
        /// <returns>A flattened query.</returns>
        private QueryUnderConstruction Flatten()
        {
            // SELECT fo(y) FROM y IN (SELECT fi(x) FROM x WHERE gi(x)) WHERE go(y)
            // is translated by substituting y for fi(x) in the outer query
            // producing
            // SELECT fo(fi(x)) FROM x WHERE gi(x) AND (go(fi(x))
            if (this.inputQuery == null)
            {
                // we are flat already
                if (this.selectClause == null)
                {
                    // If selectClause doesn't exists, use SELECT v0 where v0 is the input parameter, instead of SELECT *.
                    // If there is a groupby clause, the input parameter comes from the groupBy binding instead of the from clause binding
                    string parameterName = (this.GroupByParameter ?? this.FromParameters).GetInputParameter().Name;
                    SqlScalarExpression parameterExpression = SqlPropertyRefScalarExpression.Create(null, SqlIdentifier.Create(parameterName));
                    this.selectClause = SqlSelectClause.Create(SqlSelectValueSpec.Create(parameterExpression));
                }
                else
                {
                    this.selectClause = SqlSelectClause.Create(this.selectClause.SelectSpec, this.topSpec, this.selectClause.HasDistinct);
                }

                return this;
            }

            QueryUnderConstruction flatInput = this.inputQuery.Flatten();
            SqlSelectClause inputSelect = flatInput.selectClause;
            SqlWhereClause inputwhere = flatInput.whereClause;

            // Determine the paramName to be replaced in the current query
            // It should be the top input parameter name which is not binded in this collection.
            // That is because if it has been binded before, it has global scope and should not be replaced.
            string paramName = null;
            HashSet<string> inputQueryParams = new HashSet<string>();
            foreach (Binding binding in this.inputQuery.FromParameters.GetBindings())
            {
                inputQueryParams.Add(binding.Parameter.Name);
            }

            foreach (Binding binding in this.FromParameters.GetBindings())
            {
                if (binding.ParameterDefinition == null || inputQueryParams.Contains(binding.Parameter.Name))
                {
                    paramName = binding.Parameter.Name;
                }
            }

            SqlIdentifier replacement = SqlIdentifier.Create(paramName);
            SqlSelectClause composedSelect;
            
            composedSelect = this.Substitute(inputSelect, inputSelect.TopSpec ?? this.topSpec, replacement, this.selectClause);
            SqlWhereClause composedWhere = this.Substitute(inputSelect.SelectSpec, replacement, this.whereClause);
            SqlOrderByClause composedOrderBy = this.Substitute(inputSelect.SelectSpec, replacement, this.orderByClause);
            SqlGroupByClause composedGroupBy = this.Substitute(inputSelect.SelectSpec, replacement, this.groupByClause);
            SqlWhereClause and = QueryUnderConstruction.CombineWithConjunction(inputwhere, composedWhere);
            FromParameterBindings fromParams = QueryUnderConstruction.CombineInputParameters(flatInput.FromParameters, this.FromParameters);
            SqlOffsetSpec offsetSpec;
            SqlLimitSpec limitSpec;
            if (flatInput.offsetSpec != null)
            {
                offsetSpec = flatInput.offsetSpec;
                limitSpec = flatInput.limitSpec;
            }
            else
            {
                offsetSpec = this.offsetSpec;
                limitSpec = this.limitSpec;
            }
            QueryUnderConstruction result = new QueryUnderConstruction(this.aliasCreatorFunc)
            {
                selectClause = composedSelect,
                whereClause = and,
                inputQuery = null,
                FromParameters = flatInput.FromParameters,
                orderByClause = composedOrderBy ?? this.inputQuery.orderByClause,
                groupByClause = composedGroupBy ?? this.inputQuery.groupByClause,
                offsetSpec = offsetSpec,
                limitSpec = limitSpec,
                alias = new Lazy<ParameterExpression>(() => this.Alias)
            };
            return result;
        }

        private SqlSelectClause Substitute(SqlSelectClause inputSelectClause, SqlTopSpec topSpec, SqlIdentifier inputParam, SqlSelectClause selectClause)
        {
            SqlSelectSpec inputSelectSpec = inputSelectClause.SelectSpec;

            if (selectClause == null)
            {
                return inputSelectSpec != null ? SqlSelectClause.Create(inputSelectSpec, topSpec, inputSelectClause.HasDistinct) : null;
            }

            if (inputSelectSpec is SqlSelectStarSpec)
            {
                return SqlSelectClause.Create(inputSelectSpec, topSpec, inputSelectClause.HasDistinct);
            }

            SqlSelectValueSpec selValue = inputSelectSpec as SqlSelectValueSpec;
            if (selValue != null)
            {
                SqlSelectSpec intoSpec = selectClause.SelectSpec;
                if (intoSpec is SqlSelectStarSpec)
                {
                    return SqlSelectClause.Create(inputSelectSpec, topSpec, selectClause.HasDistinct || inputSelectClause.HasDistinct);
                }

                SqlSelectValueSpec intoSelValue = intoSpec as SqlSelectValueSpec;
                if (intoSelValue != null)
                {
                    SqlScalarExpression replacement = SqlExpressionManipulation.Substitute(selValue.Expression, inputParam, intoSelValue.Expression);
                    SqlSelectValueSpec selValueReplacement = SqlSelectValueSpec.Create(replacement);
                    return SqlSelectClause.Create(selValueReplacement, topSpec, selectClause.HasDistinct || inputSelectClause.HasDistinct);
                }

                throw new DocumentQueryException("Unexpected SQL select clause type: " + intoSpec.GetType());
            }

            throw new DocumentQueryException("Unexpected SQL select clause type: " + inputSelectSpec.GetType());
        }

        private SqlWhereClause Substitute(SqlSelectSpec spec, SqlIdentifier inputParam, SqlWhereClause whereClause)
        {
            if (whereClause == null)
            {
                return null;
            }

            if (spec is SqlSelectStarSpec)
            {
                return whereClause;
            }
            else
            {
                SqlSelectValueSpec selValue = spec as SqlSelectValueSpec;
                if (selValue != null)
                {
                    SqlScalarExpression replaced = selValue.Expression;
                    SqlScalarExpression original = whereClause.FilterExpression;
                    SqlScalarExpression substituted = SqlExpressionManipulation.Substitute(replaced, inputParam, original);
                    SqlWhereClause result = SqlWhereClause.Create(substituted);
                    return result;
                }
            }

            throw new DocumentQueryException("Unexpected SQL select clause type: " + spec.GetType());
        }

        private SqlOrderByClause Substitute(SqlSelectSpec spec, SqlIdentifier inputParam, SqlOrderByClause orderByClause)
        {
            if (orderByClause == null)
            {
                return null;
            }

            if (spec is SqlSelectStarSpec)
            {
                return orderByClause;
            }

            SqlSelectValueSpec selValue = spec as SqlSelectValueSpec;
            if (selValue != null)
            {
                SqlScalarExpression replaced = selValue.Expression;
                SqlOrderByItem[] substitutedItems = new SqlOrderByItem[orderByClause.OrderByItems.Length];
                for (int i = 0; i < substitutedItems.Length; ++i)
                {
                    SqlScalarExpression substituted = SqlExpressionManipulation.Substitute(replaced, inputParam, orderByClause.OrderByItems[i].Expression);
                    substitutedItems[i] = SqlOrderByItem.Create(substituted, orderByClause.OrderByItems[i].IsDescending);
                }
                SqlOrderByClause result = SqlOrderByClause.Create(substitutedItems);
                return result;
            }

            throw new DocumentQueryException("Unexpected SQL select clause type: " + spec.GetType());
        }

        private SqlGroupByClause Substitute(SqlSelectSpec spec, SqlIdentifier inputParam, SqlGroupByClause groupByClause)
        {
            if (groupByClause == null)
            {
                return null;
            }

            SqlSelectValueSpec selectValueSpec = spec as SqlSelectValueSpec;
            if (selectValueSpec != null)
            {
                SqlScalarExpression replaced = selectValueSpec.Expression;
                SqlScalarExpression[] substitutedItems = new SqlScalarExpression[groupByClause.Expressions.Length];
                for (int i = 0; i < substitutedItems.Length; ++i)
                {
                    SqlScalarExpression substituted = SqlExpressionManipulation.Substitute(replaced, inputParam, groupByClause.Expressions[i]);
                    substitutedItems[i] = substituted;
                }
                SqlGroupByClause result = SqlGroupByClause.Create(substitutedItems);
                return result;
            }

            throw new DocumentQueryException("Unexpected SQL select clause type: " + spec.GetType());
        }

        /// <summary>
        /// Determine if the current method call should create a new QueryUnderConstruction node or not.
        /// </summary>
        /// <param name="methodName">The current method name</param>
        /// <param name="argumentCount">The method's parameter count</param>
        /// <returns>True if the current method should be in a new query node</returns>
        public bool ShouldBeOnNewQuery(string methodName, int argumentCount)
        {
            // In the LINQ provider perspective, a SQL query (without subquery) the order of the execution of the operations is:
            //      Join -> Where -> Order By  -> Aggregates/Distinct/Select -> Top/Offset Limit
            //                    |             |
            //                    |-> Group By->|
            //
            // The order for the corresponding LINQ operations is:
            //      SelectMany -> Where -> OrderBy    -> Aggregates/Distinct/Select -> Skip/Take
            //                          |             |
            //                          |-> Group By->|
            //
            // In general, if an operation Op1 is being visited and the current query already has Op0 which
            // appear not before Op1 in the execution order, then this Op1 needs to be in a new query. This ensures
            // the semantics because if both of them are in the same query then the order would be Op0 -> Op1 
            // which is not true to the order they appear. In this case, Op1 will be consider to be in a parent-query 
            // in the flattening step.
            // 
            // In some cases, two operations has commutativity property, e.g. Select and Skip/Take/OrderBy/Where.
            // So visiting Select after Skip/Take has the same affect as visiting Skip/Take and then Select.
            //
            // Some operations are represented together with another operations in QueryUnderConstruction for simplicity purpose.
            // Therefore, the carrying operation needs to be considered instead. E.g. Aggregation functions are represented as a
            // SELECT VALUE <aggregate method> by themselves, Distinct is represented as SELECT VALUE DISTINCT.
            //
            // The rules in this function are simplified based on the above observations.

            bool shouldPackage = false;

            switch (methodName)
            {
                case LinqMethods.Select:
                    // New query is needed when adding a Select to an existing Select
                    shouldPackage = this.selectClause != null;
                    break;

                case LinqMethods.Min:
                case LinqMethods.Max:
                case LinqMethods.Sum:
                case LinqMethods.Average:
                    shouldPackage = (this.selectClause != null) ||
                        (this.offsetSpec != null) ||
                        (this.topSpec != null);
                    break;

                case LinqMethods.Count:
                    // When Count has 2 arguments, it calls into AddWhereClause so it should be considered as a Where in that case.
                    // Otherwise, treat it as other aggregate functions (using Sum here for simplicity).
                    shouldPackage = (argumentCount == 2 && this.ShouldBeOnNewQuery(LinqMethods.Where, 2)) ||
                        this.ShouldBeOnNewQuery(LinqMethods.Sum, 1);
                    break;

                case LinqMethods.Where:
                // Where expression parameter needs to be substituted if necessary so
                // It is not needed in Select distinct because the Select distinct would have the necessary parameter name adjustment.
                case LinqMethods.Any:
                case LinqMethods.OrderBy:
                case LinqMethods.OrderByDescending:
                case LinqMethods.ThenBy:
                case LinqMethods.ThenByDescending:
                case LinqMethods.Distinct:
                    // New query is needed when there is already a Take or a non-distinct Select
                    shouldPackage = (this.topSpec != null) ||
                        (this.offsetSpec != null) ||
                        (this.selectClause != null && !this.selectClause.HasDistinct) || 
                        (this.groupByClause != null);
                    break;

                case LinqMethods.GroupBy:
                    // New query is needed when there is already a Take or a Select or a Group by clause or an Order By clause
                    shouldPackage = (this.topSpec != null) ||
                        (this.offsetSpec != null) ||
                        (this.selectClause != null) || 
                        (this.groupByClause != null) || 
                        (this.orderByClause != null);
                    break;

                case LinqMethods.Skip:
                    shouldPackage = (this.topSpec != null) ||
                        (this.limitSpec != null);
                    break;

                case LinqMethods.SelectMany:
                    shouldPackage = (this.topSpec != null) ||
                        (this.offsetSpec != null) ||
                        (this.selectClause != null);
                    break;

                default:
                    break;
            }

            return shouldPackage;
        }

        /// <summary>
        /// Add a Select clause to a query, without creating a new subquery
        /// </summary>
        /// <param name="select">The Select clause to add</param>
        /// <returns>A new query containing a select clause.</returns>
        public QueryUnderConstruction AddSelectClause(SqlSelectClause select)
        {
            // If result SelectClause is not null, or both result selectClause and select has Distinct
            // then it is unexpected since the SelectClause will be overwritten.
            if (!((this.selectClause != null && this.selectClause.HasDistinct && this.selectClause.HasDistinct) ||
                this.selectClause == null))
            {
                throw new DocumentQueryException("Internal error: attempting to overwrite SELECT clause");
            }

            this.selectClause = select;

            return this;
        }

        /// <summary>
        /// Add a Select clause to a query; may need to create a new subquery.
        /// </summary>
        /// <param name="select">Select clause to add.</param>
        /// <param name="context">The translation context.</param>
        /// <returns>A new query containing a select clause.</returns>
        public QueryUnderConstruction AddSelectClause(SqlSelectClause select, TranslationContext context)
        {
            QueryUnderConstruction result = context.PackageCurrentQueryIfNeccessary();

            // If result SelectClause is not null, or both result selectClause and select has Distinct
            // then it is unexpected since the SelectClause will be overwritten.
            if (!((result.selectClause != null && result.selectClause.HasDistinct && this.selectClause.HasDistinct) ||
                result.selectClause == null))
            {
                throw new DocumentQueryException("Internal error: attempting to overwrite SELECT clause");
            }

            result.selectClause = select;
            foreach (Binding binding in context.CurrentSubqueryBinding.TakeBindings()) result.AddBinding(binding);

            return result;
        }

        public QueryUnderConstruction AddOrderByClause(SqlOrderByClause orderBy, TranslationContext context)
        {
            QueryUnderConstruction result = context.PackageCurrentQueryIfNeccessary();

            result.orderByClause = orderBy;
            foreach (Binding binding in context.CurrentSubqueryBinding.TakeBindings()) result.AddBinding(binding);

            return result;
        }

        public QueryUnderConstruction UpdateOrderByClause(SqlOrderByClause thenBy, TranslationContext context)
        {
            List<SqlOrderByItem> items = new List<SqlOrderByItem>(context.CurrentQuery.orderByClause.OrderByItems);
            items.AddRange(thenBy.OrderByItems);
            context.CurrentQuery.orderByClause = SqlOrderByClause.Create(items.ToImmutableArray());

            foreach (Binding binding in context.CurrentSubqueryBinding.TakeBindings()) context.CurrentQuery.AddBinding(binding);

            return context.CurrentQuery;
        }

        public QueryUnderConstruction AddGroupByClause(SqlGroupByClause groupBy, TranslationContext context)
        {
            QueryUnderConstruction result = context.PackageCurrentQueryIfNeccessary();

            result.groupByClause = groupBy;
            foreach (Binding binding in context.CurrentSubqueryBinding.TakeBindings()) result.AddBinding(binding);

            return result;
        }

        public QueryUnderConstruction AddOffsetSpec(SqlOffsetSpec offsetSpec, TranslationContext context)
        {
            QueryUnderConstruction result = context.PackageCurrentQueryIfNeccessary();
            if (result.offsetSpec != null)
            {
                // Skip(A).Skip(B) => Skip(A + B)
                long offsetA = QueryUnderConstruction.GetOffsetCount(result.offsetSpec);
                long offsetB = QueryUnderConstruction.GetOffsetCount(offsetSpec);
                result.offsetSpec = SqlOffsetSpec.Create(SqlNumberLiteral.Create(offsetA + offsetB));
            }
            else
            {
                result.offsetSpec = offsetSpec;
            }

            return result;
        }

        private static long GetOffsetCount(SqlOffsetSpec offsetSpec)
        {
            if (offsetSpec == null)
            {
                throw new ArgumentNullException(nameof(offsetSpec));
            }

            SqlScalarExpression offsetExpression = offsetSpec.OffsetExpression;
            if (!(offsetExpression is SqlLiteralScalarExpression offsetLiteralExpression))
            {
                throw new ArgumentException($"Expected number literal scalar expression.");
            }

            SqlLiteral sqlLiteral = offsetLiteralExpression.Literal;
            if (!(sqlLiteral is SqlNumberLiteral sqlNumberLiteral))
            {
                throw new ArgumentException($"Expected number literal.");
            }

            if (!sqlNumberLiteral.Value.IsInteger)
            {
                throw new ArgumentException($"Expected integer literal.");
            }

            long value = Number64.ToLong(sqlNumberLiteral.Value);
            return value;
        }

        public QueryUnderConstruction AddLimitSpec(SqlLimitSpec limitSpec, TranslationContext context)
        {
            QueryUnderConstruction result = this;
            if (result.limitSpec != null)
            {
                long accumulatedLimitCount = QueryUnderConstruction.GetLimitCount(result.limitSpec);
                long currentLimitCount = QueryUnderConstruction.GetLimitCount(limitSpec);
                if (currentLimitCount < accumulatedLimitCount)
                {
                    result.limitSpec = limitSpec;
                }
            }
            else
            {
                result.limitSpec = limitSpec;
            }

            return result;
        }

        private static long GetLimitCount(SqlLimitSpec limitSpec)
        {
            if (limitSpec == null)
            {
                throw new ArgumentNullException(nameof(limitSpec));
            }

            SqlScalarExpression limitExpression = limitSpec.LimitExpression;
            if (!(limitExpression is SqlLiteralScalarExpression limitLiteralExpression))
            {
                throw new ArgumentException($"Expected number literal scalar expression.");
            }

            SqlLiteral sqlLiteral = limitLiteralExpression.Literal;
            if (!(sqlLiteral is SqlNumberLiteral sqlNumberLiteral))
            {
                throw new ArgumentException($"Expected number literal.");
            }

            if (!sqlNumberLiteral.Value.IsInteger)
            {
                throw new ArgumentException($"Expected integer literal.");
            }

            long value = Number64.ToLong(sqlNumberLiteral.Value);
            return value;
        }

        public QueryUnderConstruction AddTopSpec(SqlTopSpec topSpec)
        {
            QueryUnderConstruction result = this;

            if (result.topSpec != null)
            {
                long accumulatedTopCount = QueryUnderConstruction.GetTopCount(result.topSpec);
                long currentTopCount = QueryUnderConstruction.GetTopCount(topSpec);
                if (currentTopCount < accumulatedTopCount)
                {
                    result.topSpec = topSpec;
                }
            }
            else
            {
                result.topSpec = topSpec;
            }

            return result;
        }

        private static long GetTopCount(SqlTopSpec sqlTopSpec)
        {
            if (sqlTopSpec == null)
            {
                throw new ArgumentNullException(nameof(sqlTopSpec));
            }

            SqlScalarExpression topExpression = sqlTopSpec.TopExpresion;
            if (!(topExpression is SqlLiteralScalarExpression topLiteralExpression))
            {
                throw new ArgumentException($"Expected number literal scalar expression.");
            }

            SqlLiteral sqlLiteral = topLiteralExpression.Literal;
            if (!(sqlLiteral is SqlNumberLiteral sqlNumberLiteral))
            {
                throw new ArgumentException($"Expected number literal.");
            }

            if (!sqlNumberLiteral.Value.IsInteger)
            {
                throw new ArgumentException($"Expected integer literal.");
            }

            long value = Number64.ToLong(sqlNumberLiteral.Value);
            return value;
        }

        private static SqlWhereClause CombineWithConjunction(SqlWhereClause first, SqlWhereClause second)
        {
            if (first == null)
            {
                return second;
            }

            if (second == null)
            {
                return first;
            }

            SqlScalarExpression previousFilter = first.FilterExpression;
            SqlScalarExpression currentFilter = second.FilterExpression;
            SqlBinaryScalarExpression and = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, previousFilter, currentFilter);
            SqlWhereClause result = SqlWhereClause.Create(and);
            return result;
        }

        private static FromParameterBindings CombineInputParameters(FromParameterBindings inputQueryParams, FromParameterBindings currentQueryParams)
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (Binding binding in inputQueryParams.GetBindings())
            {
                seen.Add(binding.Parameter.Name);
            }

            FromParameterBindings fromParams = inputQueryParams;
            foreach (FromParameterBindings.Binding binding in currentQueryParams.GetBindings())
            {
                if (binding.ParameterDefinition != null && !seen.Contains(binding.Parameter.Name))
                {
                    fromParams.Add(binding);
                    seen.Add(binding.Parameter.Name);
                }
            }

            return fromParams;
        }

        /// <summary>
        /// Add a Where clause to a query; may need to create a new query.
        /// </summary>
        /// <param name="whereClause">Clause to add.</param>
        /// <param name="context">The translation context.</param>
        /// <returns>A new query containing the specified Where clause.</returns>
        public QueryUnderConstruction AddWhereClause(SqlWhereClause whereClause, TranslationContext context)
        {
            QueryUnderConstruction result = context.PackageCurrentQueryIfNeccessary();

            whereClause = QueryUnderConstruction.CombineWithConjunction(result.whereClause, whereClause);
            result.whereClause = whereClause;
            foreach (Binding binding in context.CurrentSubqueryBinding.TakeBindings()) result.AddBinding(binding);

            return result;
        }

        /// <summary>
        /// Separate out the query branch, which makes up a subquery and is built on top of the parent query chain.
        /// E.g. Let the query chain at some point in time be q0 - q1 - q2. When a subquery is recognized, its expression is visited.
        /// Assume that adds 2 queries to the chain to q0 - q1 - q2 - q3 - q4. Invoking q4.GetSubquery(q2) would return q3 - q4
        /// after it's isolated from the rest of the chain.
        /// </summary>
        /// <param name="queryBeforeVisit">The last query in the chain before the collection expression is visited</param>
        /// <returns>The subquery that has been decoupled from the parent query chain</returns>
        public QueryUnderConstruction GetSubquery(QueryUnderConstruction queryBeforeVisit)
        {
            QueryUnderConstruction parentQuery = null;
            for (QueryUnderConstruction query = this;
                query != queryBeforeVisit;
                query = query.inputQuery)
            {
                parentQuery = query;
            }

            parentQuery.inputQuery = null;
            return this;
        }

        public bool HasOffsetSpec()
        {
            return this.offsetSpec != null;
        }

        /// <summary>
        /// Check whether the current SELECT clause has an aggregate function
        /// </summary>
        /// <returns>true if the selectClause has an aggregate function call</returns>
        private bool HasSelectAggregate()
        {
            string functionCallName = ((this.selectClause?.SelectSpec as SqlSelectValueSpec)?.Expression as SqlFunctionCallScalarExpression)?.Name.Value;

            return (functionCallName != null) &&
                ((functionCallName == SqlFunctionCallScalarExpression.Names.Max) ||
                (functionCallName == SqlFunctionCallScalarExpression.Names.Min) ||
                (functionCallName == SqlFunctionCallScalarExpression.Names.Avg) ||
                (functionCallName == SqlFunctionCallScalarExpression.Names.Count) ||
                (functionCallName == SqlFunctionCallScalarExpression.Names.Sum));
        }

        /// <summary>
        /// Debugging string.
        /// </summary>
        /// <returns>Query representation as a string (not legal SQL).</returns>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            if (this.inputQuery != null)
            {
                builder.Append(this.inputQuery);
            }

            if (this.whereClause != null)
            {
                builder.Append("->");
                builder.Append(this.whereClause);
            }

            if (this.selectClause != null)
            {
                builder.Append("->");
                builder.Append(this.selectClause);
            }

            return builder.ToString();
        }
    }
}
