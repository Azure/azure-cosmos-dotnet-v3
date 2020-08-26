//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Simplifies an Expression tree evaluating everything that can be evaluated 
    /// at the current time.  Could be more efficient by evaluating a complete constant subtree at once.
    /// </summary>
    internal static class ConstantFolding
    {
        public static bool IsConstant(Expression inputExpression)
        {
            return inputExpression == null || inputExpression.NodeType == ExpressionType.Constant;
        }

        public static Expression Fold(Expression inputExpression)
        {
            if (inputExpression == null)
            {
                return inputExpression;
            }

            switch (inputExpression.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                case ExpressionType.UnaryPlus:
                case ExpressionType.OnesComplement:
                case ExpressionType.Increment:
                case ExpressionType.Decrement:
                    return ConstantFolding.FoldUnary((UnaryExpression)inputExpression);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.ArrayIndex:
                case ExpressionType.RightShift:
                case ExpressionType.LeftShift:
                case ExpressionType.ExclusiveOr:
                    return ConstantFolding.FoldBinary((BinaryExpression)inputExpression);
                case ExpressionType.TypeIs:
                    return ConstantFolding.FoldTypeIs((TypeBinaryExpression)inputExpression);
                case ExpressionType.Conditional:
                    return ConstantFolding.FoldConditional((ConditionalExpression)inputExpression);
                case ExpressionType.Constant:
                    return inputExpression;
                case ExpressionType.Parameter:
                    return ConstantFolding.FoldParameter((ParameterExpression)inputExpression);
                case ExpressionType.MemberAccess:
                    return ConstantFolding.FoldMemberAccess((MemberExpression)inputExpression);
                case ExpressionType.Call:
                    return ConstantFolding.FoldMethodCall((MethodCallExpression)inputExpression);
                case ExpressionType.Lambda:
                    return ConstantFolding.FoldLambda((LambdaExpression)inputExpression);
                case ExpressionType.New:
                    return ConstantFolding.FoldNew((NewExpression)inputExpression);
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return ConstantFolding.FoldNewArray((NewArrayExpression)inputExpression);
                case ExpressionType.Invoke:
                    return ConstantFolding.FoldInvocation((InvocationExpression)inputExpression);
                case ExpressionType.MemberInit:
                    return ConstantFolding.FoldMemberInit((MemberInitExpression)inputExpression);
                case ExpressionType.ListInit:
                    return ConstantFolding.FoldListInit((ListInitExpression)inputExpression);
                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentUICulture,
                    "Unhandled expression type: '{0}'", inputExpression.NodeType));
            }
        }

        public static MemberBinding FoldBinding(MemberBinding inputExpression)
        {
            return inputExpression.BindingType switch
            {
                MemberBindingType.Assignment => ConstantFolding.FoldMemberAssignment((MemberAssignment)inputExpression),
                MemberBindingType.MemberBinding => ConstantFolding.FoldMemberMemberBinding((MemberMemberBinding)inputExpression),
                MemberBindingType.ListBinding => ConstantFolding.FoldMemberListBinding((MemberListBinding)inputExpression),
                _ => throw new DocumentQueryException(string.Format(CultureInfo.CurrentUICulture, "Unhandled binding type '{0}'", inputExpression.BindingType)),
            };
        }

        public static ElementInit FoldElementInitializer(ElementInit inputExpression)
        {
            ReadOnlyCollection<Expression> arguments = ConstantFolding.FoldExpressionList(inputExpression.Arguments);
            if (arguments != inputExpression.Arguments)
            {
                return Expression.ElementInit(inputExpression.AddMethod, arguments);
            }

            return inputExpression;
        }

        public static Expression FoldUnary(UnaryExpression inputExpression)
        {
            Expression operand = ConstantFolding.Fold(inputExpression.Operand);
            Expression resultExpression;
            if (operand != inputExpression.Operand)
            {
                resultExpression = Expression.MakeUnary(inputExpression.NodeType, operand, inputExpression.Type, inputExpression.Method);
            }
            else
            {
                resultExpression = inputExpression;
            }

            if (IsConstant(operand))
            {
                resultExpression = ExpressionSimplifier.EvaluateToExpression(resultExpression);
            }

            return resultExpression;
        }

        public static Expression FoldBinary(BinaryExpression inputExpression)
        {
            Expression left = ConstantFolding.Fold(inputExpression.Left);
            Expression right = ConstantFolding.Fold(inputExpression.Right);
            LambdaExpression conversion = ConstantFolding.FoldLambda(inputExpression.Conversion);
            Expression resultExpression;
            if (left != inputExpression.Left || right != inputExpression.Right || conversion != inputExpression.Conversion)
            {
                if (inputExpression.NodeType == ExpressionType.Coalesce)
                {
                    resultExpression = Expression.Coalesce(left, right, conversion);
                }
                else
                {
                    resultExpression = Expression.MakeBinary(inputExpression.NodeType, left, right, inputExpression.IsLiftedToNull, inputExpression.Method);
                }
            }
            else
            {
                resultExpression = inputExpression;
            }

            if (IsConstant(left) && inputExpression.NodeType == ExpressionType.Coalesce)
            {
                object leftValue = ExpressionSimplifier.Evaluate(left);
                if (leftValue == null)
                {
                    resultExpression = right;
                }
                else
                {
                    resultExpression = Expression.Constant(leftValue);
                }
            }
            else if (IsConstant(left) && IsConstant(right))
            {
                resultExpression = ExpressionSimplifier.EvaluateToExpression(resultExpression);
            }

            return resultExpression;
        }

        public static Expression FoldTypeIs(TypeBinaryExpression inputExpression)
        {
            Expression expr = ConstantFolding.Fold(inputExpression.Expression);
            Expression resultExpression;
            if (expr != inputExpression.Expression)
            {
                resultExpression = Expression.TypeIs(expr, inputExpression.TypeOperand);
            }
            else
            {
                resultExpression = inputExpression;
            }

            if (IsConstant(expr))
            {
                resultExpression = ExpressionSimplifier.EvaluateToExpression(resultExpression);
            }

            return resultExpression;
        }

        public static Expression FoldConstant(ConstantExpression inputExpression)
        {
            return inputExpression;
        }

        public static Expression FoldConditional(ConditionalExpression inputExpression)
        {
            Expression test = ConstantFolding.Fold(inputExpression.Test);
            Expression ifTrue = ConstantFolding.Fold(inputExpression.IfTrue);
            Expression ifFalse = ConstantFolding.Fold(inputExpression.IfFalse);
            Expression resultExpression;
            if (test != inputExpression.Test || ifTrue != inputExpression.IfTrue || ifFalse != inputExpression.IfFalse)
            {
                resultExpression = Expression.Condition(test, ifTrue, ifFalse);
            }
            else
            {
                resultExpression = inputExpression;
            }

            if (IsConstant(test))
            {
                object value = ExpressionSimplifier.Evaluate(test);
                bool bValue = (bool)value;

                if (bValue)
                {
                    // ifTrue is already folded
                    resultExpression = ifTrue;
                }
                else
                {
                    resultExpression = ifFalse;
                }
            }

            return resultExpression;
        }

        public static Expression FoldParameter(ParameterExpression inputExpression)
        {
            return inputExpression;
        }

        public static Expression FoldMemberAccess(MemberExpression inputExpression)
        {
            Expression expr = ConstantFolding.Fold(inputExpression.Expression);
            Expression resultExpression;
            if (expr != inputExpression.Expression)
            {
                resultExpression = Expression.MakeMemberAccess(expr, inputExpression.Member);
            }
            else
            {
                resultExpression = inputExpression;
            }

            if (IsConstant(expr))
            {
                resultExpression = ExpressionSimplifier.EvaluateToExpression(resultExpression);
            }

            return resultExpression;
        }

        public static Expression FoldMethodCall(MethodCallExpression inputExpression)
        {
            Expression obj = ConstantFolding.Fold(inputExpression.Object);
            ReadOnlyCollection<Expression> args = ConstantFolding.FoldExpressionList(inputExpression.Arguments);
            Expression resultExpression;
            if (obj != inputExpression.Object || args != inputExpression.Arguments)
            {
                resultExpression = Expression.Call(obj, inputExpression.Method, args);
            }
            else
            {
                resultExpression = inputExpression;
            }

            if (!IsConstant(obj))
            {
                return resultExpression;
            }

            foreach (Expression arg in args)
            {
                if (!IsConstant(arg))
                {
                    return resultExpression;
                }
            }

            // skip simplifying .Take(Constant)
            if (inputExpression.Method.IsStatic &&
                inputExpression.Method.DeclaringType.IsAssignableFrom(typeof(Queryable)) &&
                inputExpression.Method.Name.Equals("Take"))
            {
                return resultExpression;
            }

            resultExpression = ExpressionSimplifier.EvaluateToExpression(resultExpression);
            return resultExpression;
        }

        public static ReadOnlyCollection<Expression> FoldExpressionList(ReadOnlyCollection<Expression> inputExpressionList)
        {
            List<Expression> list = null;
            for (int i = 0; i < inputExpressionList.Count; i++)
            {
                Expression p = ConstantFolding.Fold(inputExpressionList[i]);
                if (list != null)
                {
                    list.Add(p);
                }
                else if (p != inputExpressionList[i])
                {
                    list = new List<Expression>(inputExpressionList.Count);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(inputExpressionList[j]);
                    }
                    list.Add(p);
                }
            }

            if (list != null)
            {
                return list.AsReadOnly();
            }

            return inputExpressionList;
        }

        public static MemberAssignment FoldMemberAssignment(MemberAssignment inputExpression)
        {
            Expression exp = ConstantFolding.Fold(inputExpression.Expression);
            if (exp != inputExpression.Expression)
            {
                return Expression.Bind(inputExpression.Member, exp);
            }

            return inputExpression;
        }

        public static MemberMemberBinding FoldMemberMemberBinding(MemberMemberBinding inputExpression)
        {
            IEnumerable<MemberBinding> bindings = ConstantFolding.FoldBindingList(inputExpression.Bindings);
            if (bindings != inputExpression.Bindings)
            {
                return Expression.MemberBind(inputExpression.Member, bindings);
            }

            return inputExpression;
        }

        public static MemberListBinding FoldMemberListBinding(MemberListBinding inputExpression)
        {
            IEnumerable<ElementInit> initializers = ConstantFolding.FoldElementInitializerList(inputExpression.Initializers);
            if (initializers != inputExpression.Initializers)
            {
                return Expression.ListBind(inputExpression.Member, initializers);
            }

            return inputExpression;
        }

        public static IList<MemberBinding> FoldBindingList(ReadOnlyCollection<MemberBinding> inputExpressionList)
        {
            List<MemberBinding> list = null;
            for (int i = 0; i < inputExpressionList.Count; i++)
            {
                MemberBinding b = ConstantFolding.FoldBinding(inputExpressionList[i]);
                if (list != null)
                {
                    list.Add(b);
                }
                else if (b != inputExpressionList[i])
                {
                    list = new List<MemberBinding>(inputExpressionList.Count);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(inputExpressionList[j]);
                    }
                    list.Add(b);
                }
            }

            if (list != null)
            {
                return list;
            }

            return inputExpressionList;
        }

        public static IList<ElementInit> FoldElementInitializerList(ReadOnlyCollection<ElementInit> inputExpressionList)
        {
            List<ElementInit> list = null;
            for (int i = 0; i < inputExpressionList.Count; i++)
            {
                ElementInit init = ConstantFolding.FoldElementInitializer(inputExpressionList[i]);
                if (list != null)
                {
                    list.Add(init);
                }
                else if (init != inputExpressionList[i])
                {
                    list = new List<ElementInit>(inputExpressionList.Count);
                    for (int j = 0; j < i; j++)
                    {
                        list.Add(inputExpressionList[j]);
                    }
                    list.Add(init);
                }
            }

            if (list != null)
            {
                return list;
            }

            return inputExpressionList;
        }

        public static LambdaExpression FoldLambda(LambdaExpression inputExpression)
        {
            if (inputExpression == null) return null;
            Expression body = ConstantFolding.Fold(inputExpression.Body);
            if (body != inputExpression.Body)
            {
                return Expression.Lambda(inputExpression.Type, body, inputExpression.Parameters);
            }

            return inputExpression;
        }

        public static NewExpression FoldNew(NewExpression inputExpression)
        {
            IEnumerable<Expression> args = ConstantFolding.FoldExpressionList(inputExpression.Arguments);
            if (args != inputExpression.Arguments)
            {
                if (inputExpression.Members != null)
                {
                    return Expression.New(inputExpression.Constructor, args, inputExpression.Members);
                }
                else
                {
                    return Expression.New(inputExpression.Constructor, args);
                }
            }

            return inputExpression;
        }

        public static Expression FoldMemberInit(MemberInitExpression inputExpression)
        {
            NewExpression foldedExp = ConstantFolding.FoldNew(inputExpression.NewExpression);
            IEnumerable<MemberBinding> bindings = ConstantFolding.FoldBindingList(inputExpression.Bindings);
            if (foldedExp != inputExpression.NewExpression || bindings != inputExpression.Bindings)
            {
                return Expression.MemberInit(foldedExp, bindings);
            }

            return inputExpression;
        }

        public static Expression FoldListInit(ListInitExpression inputExpression)
        {
            NewExpression n = ConstantFolding.FoldNew(inputExpression.NewExpression);
            IEnumerable<ElementInit> initializers = ConstantFolding.FoldElementInitializerList(inputExpression.Initializers);
            if (n != inputExpression.NewExpression || initializers != inputExpression.Initializers)
            {
                return Expression.ListInit(n, initializers);
            }

            return inputExpression;
        }

        public static Expression FoldNewArray(NewArrayExpression inputExpression)
        {
            IEnumerable<Expression> exprs = ConstantFolding.FoldExpressionList(inputExpression.Expressions);
            if (exprs != inputExpression.Expressions)
            {
                if (inputExpression.NodeType == ExpressionType.NewArrayInit)
                {
                    return Expression.NewArrayInit(inputExpression.Type.GetElementType(), exprs);
                }
                else
                {
                    return Expression.NewArrayBounds(inputExpression.Type.GetElementType(), exprs);
                }
            }

            return inputExpression;
        }

        public static Expression FoldInvocation(InvocationExpression inputExpression)
        {
            IEnumerable<Expression> args = ConstantFolding.FoldExpressionList(inputExpression.Arguments);
            Expression expr = ConstantFolding.Fold(inputExpression.Expression);
            Expression resultExpression;
            if (args != inputExpression.Arguments || expr != inputExpression.Expression)
            {
                resultExpression = Expression.Invoke(expr, args);
            }
            else
            {
                resultExpression = inputExpression;
            }

#if zero
            if (!IsConstant(expr))
            {
                return retval;
            }
            // lambdas are constant
#endif
            foreach (Expression arg in args)
            {
                if (!IsConstant(arg))
                {
                    return resultExpression;
                }
            }

            resultExpression = ExpressionSimplifier.EvaluateToExpression(resultExpression);
            return resultExpression;
        }
    }
}
