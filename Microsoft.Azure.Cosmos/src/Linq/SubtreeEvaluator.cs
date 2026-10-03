//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary> 
    /// Evaluates and replaces sub-trees when first candidate is reached (top-down) 
    /// </summary> 
    internal sealed class SubtreeEvaluator : ExpressionVisitor
    {
        private readonly HashSet<Expression> candidates;

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
            // Rebuild the MemberInit manually and intentionally do NOT visit node.NewExpression.
            // The Nominator nominates a parameterless `new T()` as a candidate (CanBeEvaluated
            // returns true for any non-Parameter / non-Lambda expression). Routing it through
            // our overridden Visit would fold it into a ConstantExpression of the constructed
            // CLR instance. Expression.MemberInit requires a NewExpression as its first argument,
            // not a ConstantExpression, so that path would throw InvalidOperationException at
            // runtime. We only need to recurse into the bindings to fold closure-captured
            // variables (and other independent sub-trees) in initializers — see issue #1664.
            ReadOnlyCollection<MemberBinding> newBindings = Visit(node.Bindings, this.VisitMemberBinding);
            if (newBindings == node.Bindings)
            {
                return node;
            }

            return Expression.MemberInit(node.NewExpression, newBindings);
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
            // using reflection here and instead rely on the fallback delegate compilation approach.
            if (targetExpression is not null && targetConstant is null)
            {
                return expression;
            }

            // We need special handling for Nullable<T>.HasValue. This is because most reflection
            // methods, including property and field accessors, pass instance arguments as object type.
            // Nullable<T> has special runtime behavior that boxes the value of the nullable instead of the
            // nullable struct itself. When Nullable<T>.HasValue is false, it is boxed as a null value when
            // passed to the PropertyInfo/FieldInfo.GetValue. This causes a TargetException to be thrown since
            // we are trying to evaluate an instance property with a null target.
            if (targetConstant != null &&
                targetConstant.Value == null &&
                Nullable.GetUnderlyingType(targetConstant.Type) != null &&
                memberExpression.Member.Name == "HasValue")
            {
                // So, if we're calling Nullable<T>.HasValue and targetConstant.Value is null, that means HasValue
                // would return false. Do that here to work around reflection quirks
                return Expression.Constant(false);
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
            Delegate function = lambda.Compile(preferInterpretation: true);

            return Expression.Constant(function.DynamicInvoke(null), expression.Type);
        }
    }
}
