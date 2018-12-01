//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

#define SUPPORT_SUBQUERIES

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Text;
    using System.Linq.Expressions;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Sql;

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
        public FromParameterBindings fromParameters
        {
            get;
            set;
        }

        private SqlSelectClause selectClause;
        private SqlWhereClause whereClause;
        private SqlOrderbyClause orderByClause;
        private SqlTopSpec topSpec;

        /// <summary>
        /// Input subquery.
        /// </summary>
        private QueryUnderConstruction inputQuery; 

        public QueryUnderConstruction()
        {
            this.fromParameters = new FromParameterBindings();
        }

        public void Bind(ParameterExpression parameter, SqlCollection collection)
        {
            this.fromParameters.Add(new FromParameterBindings.Binding(parameter, collection, true));
        }

        public ParameterExpression InputParameter()
        {
            return this.fromParameters.GetInputParameter();
        }
   
        /// <summary>
        /// Create a FROM clause from a set of FROM parameter bindings.
        /// </summary>
        /// <returns>The created FROM clause.</returns>
        private SqlFromClause CreateFrom()
        {
            SqlCollectionExpression input = null;
            foreach (var paramDef in this.fromParameters.GetBindings())
            {
                var parameter = paramDef.Parameter;
                var paramBinding = paramDef.ParameterDefinition;

                var ident = new SqlIdentifier(parameter.Name);
                SqlCollectionExpression collExpr;
                if (!paramDef.IsInCollection)
                {
                    var newInput = new SqlInputPathCollection(ident, null);
                    collExpr = new SqlAliasedCollectionExpression(newInput, null);
                }
                else
                {
                    collExpr = new SqlArrayIteratorCollectionExpression(ident, paramBinding);
                }

                if (input != null)
                {
                    input = new SqlJoinCollectionExpression(input, collExpr);
                }
                else
                {
                    input = collExpr;
                }
            }
            var fromClause = new SqlFromClause(input);
            return fromClause;
        }

        private SqlFromClause CreateSubqueryFromClause(SqlQuery subquery)
        {
            var collection = new SqlSubqueryCollection(subquery);
            var inputParam = this.InputParameter();
            SqlIdentifier identifier = new SqlIdentifier(inputParam.Name);
            var colExp = new SqlSubqueryCollectionExpression(identifier, collection);
            var fromClause = new SqlFromClause(colExp);
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
                SqlQuery input = this.inputQuery.GetSqlQuery();
                fromClause = this.CreateSubqueryFromClause(input);
#else
                throw new DocumentQueryException("SQL subqueries currently not supported");
#endif
            }
            else
            {
                fromClause = this.CreateFrom();
            }

            SqlQuery result = new SqlQuery(this.selectClause, fromClause, this.whereClause, this.orderByClause);
            return result;
        }

        private QueryUnderConstruction PackageQuery(HashSet<ParameterExpression> inScope)
        {
            QueryUnderConstruction result = new QueryUnderConstruction();
            var inputParam = result.fromParameters.SetInputParameter(typeof(object), ParameterSubstitution.InputParameterName, inScope);
            inScope.Add(inputParam);

            result.inputQuery = this;
            if (this.selectClause == null)
            {
                var star = new SqlSelectStarSpec();
                this.selectClause = new SqlSelectClause(star, null);
            }

            return result;
        }

        /// <summary>
        /// Flatten subqueries into a single query by substituting their expressions in the current query.
        /// </summary>
        /// <returns>A flattened query.</returns>
        public QueryUnderConstruction Flatten()
        {
            // SELECT fo(y) FROM y IN (SELECT fi(x) FROM x WHERE gi(x)) WHERE go(y)
            // is translated by substituting fi(x) for y in the outer query
            // producing
            // SELECT fo(fi(x)) FROM x WHERE gi(x) AND (go(fi(x))
            if (this.inputQuery == null)
            {
                // we are flat already
                if (this.selectClause == null)
                {
                    this.selectClause = new SqlSelectClause(new SqlSelectStarSpec(), this.topSpec);
                }
                else
                {
                    this.selectClause = new SqlSelectClause(this.selectClause.SelectSpec, this.topSpec, this.selectClause.HasDistinct);
                }

                return this;
            }

            if (this.inputQuery.orderByClause != null && this.orderByClause != null)
            {
                throw new DocumentQueryException("Multiple OrderBy is not supported.");
            }

            var flatInput = this.inputQuery.Flatten();
            var inputSelect = flatInput.selectClause;
            var inputwhere = flatInput.whereClause;

            SqlIdentifier replacement = new SqlIdentifier(this.InputParameter().Name);
            var composedSelect = Substitute(inputSelect, inputSelect.TopSpec ?? this.topSpec, replacement, this.selectClause);
            var composedWhere = Substitute(inputSelect.SelectSpec, replacement, this.whereClause);
            var composedOrderBy = Substitute(inputSelect.SelectSpec, replacement, this.orderByClause);
            var and = QueryUnderConstruction.CombineWithConjunction(inputwhere, composedWhere);
            QueryUnderConstruction result = new QueryUnderConstruction
            {
                selectClause = composedSelect,
                whereClause = and,
                inputQuery = null,
                fromParameters = flatInput.fromParameters,
                orderByClause = composedOrderBy ?? this.inputQuery.orderByClause,
            };
            return result;
        }

        private SqlSelectClause Substitute(SqlSelectClause inputSelectClause, SqlTopSpec topSpec, SqlIdentifier inputParam, SqlSelectClause selectClause)
        {
            var selectSpec = inputSelectClause.SelectSpec;

            if (selectClause == null)
            {
                return selectSpec != null ? new SqlSelectClause(selectSpec, topSpec, inputSelectClause.HasDistinct) : null;
            }

            if (selectSpec is SqlSelectStarSpec)
            {
                return new SqlSelectClause(selectSpec, topSpec, inputSelectClause.HasDistinct);
            }

            var selValue = selectSpec as SqlSelectValueSpec;
            if (selValue != null)
            {
                var intoSpec = selectClause.SelectSpec;
                if (intoSpec is SqlSelectStarSpec)
                {
                    return new SqlSelectClause(selectSpec, topSpec, selectClause.HasDistinct || inputSelectClause.HasDistinct);
                }

                var intoSelValue = intoSpec as SqlSelectValueSpec;
                if (intoSelValue != null)
                {
                    var replacement = SqlExpressionManipulation.Substitute(selValue.Expression, inputParam, intoSelValue.Expression);
                    SqlSelectValueSpec selValueReplacement = new SqlSelectValueSpec(replacement);
                    return new SqlSelectClause(selValueReplacement, this.topSpec, selectClause.HasDistinct || inputSelectClause.HasDistinct);
                }

                throw new DocumentQueryException("Unexpected SQL select clause type: " + intoSpec.Kind);
            }

            throw new DocumentQueryException("Unexpected SQL select clause type: " + selectSpec.Kind);
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
                var selValue = spec as SqlSelectValueSpec;
                if (selValue != null)
                {
                    SqlScalarExpression replaced = selValue.Expression;
                    SqlScalarExpression original = whereClause.FilterExpression;
                    SqlScalarExpression substituted = SqlExpressionManipulation.Substitute(replaced, inputParam, original);
                    SqlWhereClause result = new SqlWhereClause(substituted);
                    return result;
                }
            }
         
            throw new DocumentQueryException("Unexpected SQL select clause type: " + spec.Kind);
        }

        private SqlOrderbyClause Substitute(SqlSelectSpec spec, SqlIdentifier inputParam, SqlOrderbyClause orderByClause)
        {
            if (orderByClause == null)
            {
                return null;
            }

            if (spec is SqlSelectStarSpec)
            {
                return orderByClause;
            }

            var selValue = spec as SqlSelectValueSpec;
            if (selValue != null)
            {
                SqlScalarExpression replaced = selValue.Expression;
                var substitutedItems = new SqlOrderbyItem[orderByClause.OrderbyItems.Length];
                for (int i = 0; i < substitutedItems.Length; ++i)
                {
                    var substituted = SqlExpressionManipulation.Substitute(replaced, inputParam, orderByClause.OrderbyItems[i].Expression);
                    substitutedItems[i] = new SqlOrderbyItem(substituted, orderByClause.OrderbyItems[i].IsDescending);
                }
                var result = new SqlOrderbyClause(substitutedItems);
                return result;
            }

            throw new DocumentQueryException("Unexpected SQL select clause type: " + spec.Kind);
        }

        /// <summary>
        /// Add a Select clause to a query; may need to create a new subquery.
        /// </summary>
        /// <param name="select">Select clause to add.</param>
        /// <param name="inputElementType">Type of element in the input collection.</param>
        /// <param name="outputElementType">Type of element in output collection.</param>
        /// <param name="inScope">Set of parameter names in scope.</param>
        /// <returns>A new query containing a select clause.</returns>
        public QueryUnderConstruction AddSelectClause(SqlSelectClause select, Type inputElementType, Type outputElementType, HashSet<ParameterExpression> inScope)
        {
            if (select.HasDistinct)
            {
                QueryUnderConstruction.ValidateNonSubquerySupport(this);
            }

            QueryUnderConstruction result = this;
            if (this.selectClause != null)
            {
                result = this.PackageQuery(inScope);
            }

            if (result.selectClause != null)
            {
                throw new DocumentQueryException("Internal error: attempting to overwrite SELECT clause");
            }
            
            result.selectClause = select;
            return result;
        }

        public QueryUnderConstruction AddOrderByClause(SqlOrderbyClause orderBy, HashSet<ParameterExpression> inScope)
        {
            QueryUnderConstruction.ValidateNonSubquerySupport(this);

            QueryUnderConstruction result = this;
            if (this.selectClause != null)
            {
                result = this.PackageQuery(inScope);
            }

            if (result.orderByClause != null)
            {
                throw new DocumentQueryException("Multiple OrderBy is not supported.");
            }

            result.orderByClause = orderBy;
            return result;
        }

        public QueryUnderConstruction AddTopSpec(SqlTopSpec topSpec)
        {
            if(this.topSpec != null)
            {
                // Set the topSpec to the one with minimum Count value
                this.topSpec = (this.topSpec.Count < topSpec.Count) ? this.topSpec : topSpec;
            }
            else
            {
                this.topSpec = topSpec;
            }

            return this;
        }

        /// <summary>
        /// Verify that the current query is supported with non-subquery.
        /// As soon as the query has a TopSpec then after that, it cannot have a Distinct, OrderBy, or Where clause.
        /// That is because in those cases, the semantics of LINQ is Take() should be done first, whereas, due to the
        /// nature of SQL language, there is no equivalent in SQL as TOP clause in SQL is executed last.
        /// This limit will be lifted when we have the support sub query in LINQ translation.
        /// </summary>
        /// <param name="query"></param>
        private static void ValidateNonSubquerySupport(QueryUnderConstruction query)
        {
            while (query != null)
            {
                if (query.topSpec != null)
                {
                    throw new DocumentQueryException("LINQ operations after a Take() is not supported yet.");
                }
                query = query.inputQuery;
            }
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

            var previousFilter = first.FilterExpression;
            var currentFilter = second.FilterExpression;
            var and = new SqlBinaryScalarExpression(SqlBinaryScalarOperatorKind.And, previousFilter, currentFilter);
            var result = new SqlWhereClause(and);
            return result;
        }

        /// <summary>
        /// Add a Where clause to a query; may need to create a new query.
        /// </summary>
        /// <param name="whereClause">Clause to add.</param>
        /// <param name="elementType">Type of element in input collection.</param>
        /// <param name="inScope">Set of parameter names in scope.</param>
        /// <returns>A new query containing the specified Where clause.</returns>
        public QueryUnderConstruction AddWhereClause(SqlWhereClause whereClause, Type elementType, HashSet<ParameterExpression> inScope)
        {
            QueryUnderConstruction.ValidateNonSubquerySupport(this);

            QueryUnderConstruction result = this;
            if (this.selectClause != null)
            {
                result = this.PackageQuery(inScope);
            }

            whereClause = QueryUnderConstruction.CombineWithConjunction(result.whereClause, whereClause);
            result.whereClause = whereClause;
            return result;
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