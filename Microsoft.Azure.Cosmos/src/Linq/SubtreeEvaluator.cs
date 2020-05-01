//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Immutable;
    using System.Linq.Expressions;

    /// <summary> 
    /// Evaluates and replaces sub-trees when first candidate is reached (top-down) 
    /// </summary> 
    internal sealed class SubtreeEvaluator : ExpressionVisitor
    {
        private readonly ImmutableHashSet<Expression> candidates;

        public SubtreeEvaluator(ImmutableHashSet<Expression> candidates)
        {
            this.candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        }

        public Expression Evaluate(Expression expression)
        {
            return this.Visit(expression);
        }

        public override Expression Visit(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }
            if (this.candidates.Contains(expression))
            {
                return this.EvaluateConstant(expression);
            }
            return base.Visit(expression);
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            return node;
        }

        private Expression EvaluateConstant(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Constant)
            {
                return expression;
            }

            LambdaExpression lambda = Expression.Lambda(expression);
            Delegate function = lambda.Compile();
            return Expression.Constant(function.DynamicInvoke(null), expression.Type);
        }
    }
}
