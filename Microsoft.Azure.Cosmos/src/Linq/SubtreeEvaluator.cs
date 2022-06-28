//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary> 
    /// Evaluates and replaces sub-trees when first candidate is reached (top-down) 
    /// </summary> 
    internal sealed class SubtreeEvaluator : ExpressionVisitor
    {
        private HashSet<Expression> candidates;

        public SubtreeEvaluator(HashSet<Expression> candidates)
        {
            this.candidates = candidates;
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

        private Expression EvaluateMemberAccess(Expression expression)
        {
            while (expression?.CanReduce ?? false)
            {
                expression = expression.Reduce();
            }

            // This is an optimization which attempts to avoid the compilation of a delegate lambda for
            // conversion of an expression to a constant by handling member access on a constant through
            // reflection instead.
            //
            // This is done because the compilation of a delegate takes a global lock which causes highly
            // threaded clients to exhibit async-over-sync thread exhaustion behaviour on this call path
            // even when doing relatively straightforward queries.
            if (expression is not MemberExpression memberExpression)
            {
                return expression;
            }

            // We recursively attempt to evaluate member access expressions so that we can support
            // nested property access (x.y.z) without needing to fall back on delegate compilation.
            Expression targetExpression = this.EvaluateMemberAccess(memberExpression.Expression);

            // NOTE: When evaluating static field or property access, we may have a null targetExpression.
            //       In this situation, we should pass the null value to the GetValue(...) methods below to
            //       indicate that we are accessing a static member.
            ConstantExpression targetConstant = targetExpression as ConstantExpression;

            // If we have a target expression but it cannot be resolved to a constant, then we should skip
            // using reflectoin here and instead rely on the fallback delegate compilation approach.
            if (targetExpression is not null && targetConstant is null)
            {
                return expression;
            }

            if (memberExpression.Member is FieldInfo fieldInfo)
            {
                return Expression.Constant(fieldInfo.GetValue(targetConstant?.Value), memberExpression.Type);
            }

            if (memberExpression.Member is PropertyInfo propertyInfo)
            {
                return Expression.Constant(propertyInfo.GetValue(targetConstant?.Value), memberExpression.Type);
            }

            return expression;
        }

        private Expression EvaluateConstant(Expression expression)
        {
            expression = this.EvaluateMemberAccess(expression);

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
