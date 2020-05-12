//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Sql;

    internal class SqlBuiltinFunctionVisitor : BuiltinFunctionVisitor
    {
        public SqlBuiltinFunctionVisitor(
            string name,
            ImmutableArray<ImmutableArray<Type>>? argumentLists)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            // TODO: ArgumentLists could be auto generated using reflection.
            this.ArgumentLists = argumentLists;
        }

        private string Name { get; }

        private ImmutableArray<ImmutableArray<Type>>? ArgumentLists { get; }

        protected override SqlScalarExpression VisitExplicit(
            MethodCallExpression methodCallExpression,
            TranslationContext context)
        {
            // Try to match one if the argument lists
            if (this.ArgumentLists.HasValue)
            {
                if ((this.ArgumentLists.Value.Length == 0) && (methodCallExpression.Arguments.Count == 0))
                {
                    return this.VisitBuiltinFunction(methodCallExpression, context);
                }

                foreach (ImmutableArray<Type> arguments in this.ArgumentLists)
                {
                    if (this.MatchArgumentLists(methodCallExpression.Arguments, arguments))
                    {
                        return this.VisitBuiltinFunction(methodCallExpression, context);
                    }
                }
            }

            return null;
        }

        protected override SqlScalarExpression VisitImplicit(
            MethodCallExpression methodCallExpression,
            TranslationContext context)
        {
            return null;
        }

        private bool MatchArgumentLists(
            ReadOnlyCollection<Expression> methodCallArguments,
            ImmutableArray<Type> expectedArguments)
        {
            if (methodCallArguments.Count != expectedArguments.Length)
            {
                return false;
            }

            for (int i = 0; i < expectedArguments.Length; i++)
            {
                if ((methodCallArguments[i].Type != expectedArguments[i]) &&
                    !expectedArguments[i].IsAssignableFrom(methodCallArguments[i].Type))
                {
                    return false;
                }
            }

            return true;
        }

        private SqlScalarExpression VisitBuiltinFunction(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();

            if (methodCallExpression.Object != null)
            {
                arguments.Add(ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Object, context));
            }

            foreach (Expression argument in methodCallExpression.Arguments)
            {
                arguments.Add(ExpressionToSql.VisitNonSubqueryScalarExpression(argument, context));
            }

            return SqlFunctionCallScalarExpression.CreateBuiltin(this.Name, arguments);
        }
    }
}
