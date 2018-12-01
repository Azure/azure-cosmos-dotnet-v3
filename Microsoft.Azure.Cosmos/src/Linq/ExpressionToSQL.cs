//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Sql;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    // ReSharper disable UnusedParameter.Local

    //////////////////////////////////////////////////////////////////////
    //
    // data.SelectMany(x => x.fields.SelectMany(y => y.fields.Select(z => f(x,y,z)))
    //  expression tree:
    // SelectMany - lambda - Selectmany - lambda - Select - lambda - f(x,y,z)
    //   |            |          |           |       |          |
    //  data          x          .- fields   y       .- fields  z
    //                           |                   |
    //                           x                   y
    // parameter bound_to
    //    x        data
    //    y        x.fields
    //    z        y.fields
    //
    // data.Where(x => f(x)).Select(y => g(y))
    //  expression tree:
    // Select - lambda - g(y)
    //    |        |
    //    |        y
    //  Where - lambda - f(x)
    //    |        |
    //  data       x
    //
    // parameter bound_to
    //    x       data
    //    y       Where

    /// <summary>
    /// Core Linq to DocDBSQL translator.
    /// </summary>
    internal static class ExpressionToSql
    {
        private static class LinqMethods
        {
            public const string Average = "Average";
            public const string SqlAvg = "Avg";
            public const string Count = "Count";
            public const string Max = "Max";
            public const string Min = "Min";
            public const string OrderBy = "OrderBy";
            public const string OrderByDescending = "OrderByDescending";
            public const string Select = "Select";
            public const string SelectMany = "SelectMany";
            public const string Sum = "Sum";
            public const string Take = "Take";
            public const string Distinct = "Distinct";
            public const string Where = "Where";
        }

        private static string SqlRoot = "root";
        private static bool usePropertyRef = false;
        private static SqlIdentifier RootIdentifier = new SqlIdentifier(SqlRoot);

        static ExpressionToSql()
        {
        }

        /// <summary>
        /// Toplevel entry point.
        /// </summary>
        /// <param name="inputExpression">An Expression representing a Query on a IDocumentQuery object.</param>
        /// <returns>The corresponding SQL query.</returns>
        public static SqlQuery TranslateQuery(Expression inputExpression)
        {
            TranslationContext context = new TranslationContext();
            ExpressionToSql.Translate(inputExpression, context); // ignore result here

            QueryUnderConstruction query = context.currentQuery;
            query = query.Flatten();
            SqlQuery result = query.GetSqlQuery();
            ExpressionToSql.Normalize(result);
            return result;
        }

        private static void Normalize(SqlQuery query)
        {
            // ExpressionToSql only works for flattened queries
            if (query.SelectClause == null)
            {
                query.SelectClause = new SqlSelectClause(new SqlSelectStarSpec(), null);
            }
        }

        /// <summary>
        /// Translate an expression into a query.
        /// Query is constructed as a side-effect in context.currentQuery.
        /// </summary>
        /// <param name="inputExpression">Expression to translate.</param>
        /// <param name="context">Context for translation.</param>
        public static Collection Translate(Expression inputExpression, TranslationContext context)
        {
            if (inputExpression == null)
            {
                throw new ArgumentNullException("inputExpression");
            }

            Collection collection;
            switch (inputExpression.NodeType)
            {
                case ExpressionType.Call:
                    collection = ExpressionToSql.VisitMethodCall((MethodCallExpression)inputExpression, context);
                    break;
                case ExpressionType.Constant:
                    collection = ExpressionToSql.TranslateInput((ConstantExpression)inputExpression, context);
                    break;
                default:
                    {
                        SqlScalarExpression scalar = ExpressionToSql.VisitScalarExpression(inputExpression, context);
                        collection = ExpressionToSql.ConvertToCollection(scalar);
                        break;
                    }
            }
            return collection;
        }

        #region VISITOR
        /// <summary>
        /// Visitor which produces a SqlScalarExpression.
        /// </summary>
        /// <param name="inputExpression">Expression to visit.</param>
        /// <param name="context">Context information.</param>
        /// <returns>The translation as a ScalarExpression.</returns>
        internal static SqlScalarExpression VisitScalarExpression(Expression inputExpression, TranslationContext context)
        {
            if (inputExpression == null)
            {
                return null;
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
                    return ExpressionToSql.VisitUnary((UnaryExpression)inputExpression, context);
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
                    return ExpressionToSql.VisitBinary((BinaryExpression)inputExpression, context);
                case ExpressionType.TypeIs:
                    return ExpressionToSql.VisitTypeIs((TypeBinaryExpression)inputExpression, context);
                case ExpressionType.Conditional:
                    return ExpressionToSql.VisitConditional((ConditionalExpression)inputExpression, context);
                case ExpressionType.Constant:
                    return ExpressionToSql.VisitConstant((ConstantExpression)inputExpression);
                case ExpressionType.Parameter:
                    return ExpressionToSql.VisitParameter((ParameterExpression)inputExpression, context);
                case ExpressionType.MemberAccess:
                    return ExpressionToSql.VisitMemberAccess((MemberExpression)inputExpression, context);
                case ExpressionType.New:
                    return ExpressionToSql.VisitNew((NewExpression)inputExpression, context);
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return ExpressionToSql.VisitNewArray((NewArrayExpression)inputExpression, context);
                case ExpressionType.Invoke:
                    return ExpressionToSql.VisitInvocation((InvocationExpression)inputExpression, context);
                case ExpressionType.MemberInit:
                    return ExpressionToSql.VisitMemberInit((MemberInitExpression)inputExpression, context);
                case ExpressionType.ListInit:
                    return ExpressionToSql.VisitListInit((ListInitExpression)inputExpression, context);
                case ExpressionType.Call:
                    return ExpressionToSql.VisitMethodCallScalar((MethodCallExpression)inputExpression, context);
                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
            }
        }

        private static SqlScalarExpression VisitMethodCallScalar(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            // Check if it is a UDF method call
            if (methodCallExpression.Method.Equals(typeof(UserDefinedFunctionProvider).GetMethod("Invoke")))
            {
                string udfName = ((ConstantExpression)methodCallExpression.Arguments[0]).Value as string;
                if (string.IsNullOrEmpty(udfName))
                {
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.UdfNameIsNullOrEmpty));
                }

                SqlIdentifier methodName = new SqlIdentifier(udfName);
                List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();

                if (methodCallExpression.Arguments.Count == 2)
                {
                    // We have two cases here, if the udf was expecting only one parameter and this parameter is an array
                    // then the second argument will be an expression of this array.
                    // else we will have a NewArrayExpression of the udf arguments
                    if (methodCallExpression.Arguments[1] is NewArrayExpression)
                    {
                        ReadOnlyCollection<Expression> argumentsExpressions = ((NewArrayExpression)methodCallExpression.Arguments[1]).Expressions;
                        foreach (Expression argument in argumentsExpressions)
                        {
                            arguments.Add(ExpressionToSql.VisitScalarExpression(argument, context));
                        }
                    }
                    else if (methodCallExpression.Arguments[1].NodeType == ExpressionType.Constant &&
                        methodCallExpression.Arguments[1].Type == typeof(object[]))
                    {
                        object[] argumentsExpressions = (object[])((ConstantExpression)methodCallExpression.Arguments[1]).Value;
                        foreach (object argument in argumentsExpressions)
                        {
                            arguments.Add(ExpressionToSql.VisitConstant(Expression.Constant(argument)));
                        }
                    }
                    else
                    {
                        arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context));
                    }
                }

                return new SqlFunctionCallScalarExpression(methodName, arguments.ToArray(), true);
            }
            else
            {
                return BuiltinFunctionVisitor.VisitBuiltinFunctionCall(methodCallExpression, context);
            }
        }

        private static SqlObjectProperty VisitBinding(MemberBinding binding, TranslationContext context)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return ExpressionToSql.VisitMemberAssignment((MemberAssignment)binding, context);
                case MemberBindingType.MemberBinding:
                    return ExpressionToSql.VisitMemberMemberBinding((MemberMemberBinding)binding, context);
                case MemberBindingType.ListBinding:
                default:
                    return ExpressionToSql.VisitMemberListBinding((MemberListBinding)binding, context);
            }
        }

        private static SqlUnaryScalarOperatorKind GetUnaryOperatorKind(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.UnaryPlus:
                    return SqlUnaryScalarOperatorKind.Plus;
                case ExpressionType.Negate:
                    return SqlUnaryScalarOperatorKind.Minus;
                case ExpressionType.OnesComplement:
                    return SqlUnaryScalarOperatorKind.BitwiseNot;
                case ExpressionType.Not:
                    return SqlUnaryScalarOperatorKind.Not;
                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.UnaryOperatorNotSupported, type));
            }
        }

        private static SqlScalarExpression VisitUnary(UnaryExpression inputExpression, TranslationContext context)
        {
            SqlScalarExpression operand = ExpressionToSql.VisitScalarExpression(inputExpression.Operand, context);

            // handle NOT IN
            if (operand is SqlInScalarExpression && inputExpression.NodeType == ExpressionType.Not)
            {
                SqlInScalarExpression inExpression = (SqlInScalarExpression)operand;
                return new SqlInScalarExpression(inExpression.Expression, inExpression.Items, true);
            }

            if (inputExpression.NodeType == ExpressionType.Quote)
            {
                return operand;
            }

            if (inputExpression.NodeType == ExpressionType.Convert)
            {
                return operand;
            }

            SqlUnaryScalarOperatorKind op = GetUnaryOperatorKind(inputExpression.NodeType);
            return new SqlUnaryScalarExpression(op, operand);
        }

        private static SqlBinaryScalarOperatorKind GetBinaryOperatorKind(ExpressionType expressionType, Type resultType)
        {
            switch (expressionType)
            {
                case ExpressionType.Add:
                    {
                        if (resultType == typeof(string))
                        {
                            return SqlBinaryScalarOperatorKind.StringConcat;
                        }
                        return SqlBinaryScalarOperatorKind.Add;
                    }
                case ExpressionType.AndAlso:
                    return SqlBinaryScalarOperatorKind.And;
                case ExpressionType.And:
                    return SqlBinaryScalarOperatorKind.BitwiseAnd;
                case ExpressionType.Or:
                    return SqlBinaryScalarOperatorKind.BitwiseOr;
                case ExpressionType.ExclusiveOr:
                    return SqlBinaryScalarOperatorKind.BitwiseXor;
                case ExpressionType.Divide:
                    return SqlBinaryScalarOperatorKind.Divide;
                case ExpressionType.Equal:
                    return SqlBinaryScalarOperatorKind.Equal;
                case ExpressionType.GreaterThan:
                    return SqlBinaryScalarOperatorKind.GreaterThan;
                case ExpressionType.GreaterThanOrEqual:
                    return SqlBinaryScalarOperatorKind.GreaterThanOrEqual;
                case ExpressionType.LessThan:
                    return SqlBinaryScalarOperatorKind.LessThan;
                case ExpressionType.LessThanOrEqual:
                    return SqlBinaryScalarOperatorKind.LessThanOrEqual;
                case ExpressionType.Modulo:
                    return SqlBinaryScalarOperatorKind.Modulo;
                case ExpressionType.Multiply:
                    return SqlBinaryScalarOperatorKind.Multiply;
                case ExpressionType.NotEqual:
                    return SqlBinaryScalarOperatorKind.NotEqual;
                case ExpressionType.OrElse:
                    return SqlBinaryScalarOperatorKind.Or;
                case ExpressionType.Subtract:
                    return SqlBinaryScalarOperatorKind.Subtract;
                case ExpressionType.Coalesce:
                    return SqlBinaryScalarOperatorKind.Coalesce;
                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.BinaryOperatorNotSupported, expressionType));
            }
        }

        private static SqlScalarExpression VisitBinary(BinaryExpression inputExpression, TranslationContext context)
        {
            // Speical case for string.CompareTo
            // if any of the left or right expression is MethodCallExpression
            // the other expression should only be constant (integer)
            MethodCallExpression methodCallExpression = null;
            ConstantExpression constantExpression = null;

            bool reverseNodeType = false;
            if (inputExpression.Left.NodeType == ExpressionType.Call && inputExpression.Right.NodeType == ExpressionType.Constant)
            {
                methodCallExpression = (MethodCallExpression)inputExpression.Left;
                constantExpression = (ConstantExpression)inputExpression.Right;
            }
            else if (inputExpression.Right.NodeType == ExpressionType.Call && inputExpression.Left.NodeType == ExpressionType.Constant)
            {
                methodCallExpression = (MethodCallExpression)inputExpression.Right;
                constantExpression = (ConstantExpression)inputExpression.Left;
                reverseNodeType = true;
            }

            if (methodCallExpression != null && constantExpression != null)
            {
                if (TryMatchStringCompareTo(methodCallExpression, constantExpression, inputExpression.NodeType))
                {
                    return ExpressionToSql.VisitStringCompareTo(methodCallExpression, constantExpression, inputExpression.NodeType, reverseNodeType, context);
                }
            }

            SqlScalarExpression left = ExpressionToSql.VisitScalarExpression(inputExpression.Left, context);
            SqlScalarExpression right = ExpressionToSql.VisitScalarExpression(inputExpression.Right, context);

            if (inputExpression.NodeType == ExpressionType.ArrayIndex)
            {
                SqlMemberIndexerScalarExpression result = new SqlMemberIndexerScalarExpression(left, right);
                return result;
            }

            SqlBinaryScalarOperatorKind op = GetBinaryOperatorKind(inputExpression.NodeType, inputExpression.Type);

            if (left.Kind == SqlObjectKind.MemberIndexerScalarExpression && right.Kind == SqlObjectKind.LiteralScalarExpression)
            {
                right = ExpressionToSql.ApplyCustomConverters(inputExpression.Left, right as SqlLiteralScalarExpression);
            }
            else if (right.Kind == SqlObjectKind.MemberIndexerScalarExpression && left.Kind == SqlObjectKind.LiteralScalarExpression)
            {
                left = ExpressionToSql.ApplyCustomConverters(inputExpression.Right, left as SqlLiteralScalarExpression);
            }

            return new SqlBinaryScalarExpression(op, left, right);
        }

        private static SqlScalarExpression ApplyCustomConverters(Expression left, SqlLiteralScalarExpression right)
        {
            MemberExpression memberExpression;
            if (left is UnaryExpression)
            {
                memberExpression = ((UnaryExpression)left).Operand as MemberExpression;
            }
            else
            {
                memberExpression = left as MemberExpression;
            }

            if (memberExpression != null)
            {
                var memberType = memberExpression.Type;
                if (memberType.IsNullable())
                {
                    memberType = memberType.NullableUnderlyingType();
                }

                // There are two ways to specify a custom attribute
                // 1- by specifying the JsonConverterAttribute on a Class/Enum
                //      [JsonConverter(typeof(StringEnumConverter))]
                //      Enum MyEnum
                //      {
                //           ...
                //      }
                //
                // 2- by specifying the JsonConverterAttribute on a property
                //      class MyClass
                //      {
                //           [JsonConverter(typeof(StringEnumConverter))]
                //           public MyEnum MyEnum;
                //      }
                //
                // Newtonsoft gives high precedence to the attribute specified
                // on a property over on a type (class/enum)
                // so we check both attributes and apply the same precedence rules
                // JsonConverterAttribute doesn't allow duplicates so it's safe to
                // use FirstOrDefault()
                var memberAttribute = memberExpression.Member.CustomAttributes.Where(ca => ca.AttributeType == typeof(JsonConverterAttribute)).FirstOrDefault();
                var typeAttribute = memberType.GetsCustomAttributes().Where(ca => ca.AttributeType == typeof(JsonConverterAttribute)).FirstOrDefault();

                CustomAttributeData converterAttribute = memberAttribute ?? typeAttribute;
                if (converterAttribute != null)
                {
                    Debug.Assert(converterAttribute.ConstructorArguments.Count > 0);

                    Type converterType = (Type)converterAttribute.ConstructorArguments[0].Value;

                    object value = default(object);
                    // Enum
                    if (memberType.IsEnum())
                    {
                        value = Enum.ToObject(memberType, ((SqlNumberLiteral)right.Literal).Value);
                    }
                    // DateTime
                    else if (memberType == typeof(DateTime))
                    {
                        value = ((SqlObjectLiteral)right.Literal).Value;
                    }

                    if (value != default(object))
                    {
                        string serializedValue;

                        if (converterType.GetConstructor(Type.EmptyTypes) != null)
                        {
                            serializedValue = JsonConvert.SerializeObject(value, (JsonConverter)Activator.CreateInstance(converterType));
                        }
                        else
                        {
                            serializedValue = JsonConvert.SerializeObject(value);
                        }

                        return new SqlLiteralScalarExpression(new SqlObjectLiteral(serializedValue, true));
                    }
                }
            }

            return right;
        }

        private static bool TryMatchStringCompareTo(MethodCallExpression left, ConstantExpression right, ExpressionType compareOperator)
        {
            if (left.Method.Equals(typeof(string).GetMethod("CompareTo", new Type[] { typeof(string) })) && left.Arguments.Count == 1)
            {
                // operator can only be =, >, >=, <, <=
                switch (compareOperator)
                {
                    case ExpressionType.Equal:
                    case ExpressionType.GreaterThan:
                    case ExpressionType.GreaterThanOrEqual:
                    case ExpressionType.LessThan:
                    case ExpressionType.LessThanOrEqual:
                        break;
                    default:
                        throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.StringCompareToInvalidOperator));
                }

                // the constant value should be zero, otherwise we can't determine how to translate the expression
                // it could be either integer or nullable integer
                if (!(right.Type == typeof(int) && (int)(right.Value) == 0) &&
                    !(right.Type == typeof(int?) && ((int?)right.Value).HasValue && ((int?)right.Value).Value == 0))
                {
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.StringCompareToInvalidConstant));
                }

                return true;
            }

            return false;
        }

        private static SqlScalarExpression VisitStringCompareTo(
            MethodCallExpression left,
            ConstantExpression right,
            ExpressionType compareOperator,
            bool reverseNodeType,
            TranslationContext context)
        {
            if (reverseNodeType)
            {
                switch (compareOperator)
                {
                    case ExpressionType.Equal:
                        // do nothing
                        break;
                    case ExpressionType.GreaterThan:
                        compareOperator = ExpressionType.LessThan;
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        compareOperator = ExpressionType.LessThanOrEqual;
                        break;
                    case ExpressionType.LessThan:
                        compareOperator = ExpressionType.GreaterThan;
                        break;
                    case ExpressionType.LessThanOrEqual:
                        compareOperator = ExpressionType.GreaterThanOrEqual;
                        break;
                    default:
                        throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.StringCompareToInvalidOperator));
                }
            }

            SqlBinaryScalarOperatorKind op = GetBinaryOperatorKind(compareOperator, null);

            SqlScalarExpression leftExpression = ExpressionToSql.VisitScalarExpression(left.Object, context);
            SqlScalarExpression rightExpression = ExpressionToSql.VisitScalarExpression(left.Arguments[0], context);

            return new SqlBinaryScalarExpression(op, leftExpression, rightExpression);
        }

        private static SqlScalarExpression VisitTypeIs(TypeBinaryExpression inputExpression, TranslationContext context)
        {
            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
        }

        public static SqlScalarExpression VisitConstant(ConstantExpression inputExpression)
        {
            if (inputExpression.Value == null)
            {
                SqlNullLiteral literal = new SqlNullLiteral();
                return new SqlLiteralScalarExpression(literal);
            }

            if (inputExpression.Type.IsNullable())
            {
                return ExpressionToSql.VisitConstant(Expression.Constant(inputExpression.Value, Nullable.GetUnderlyingType(inputExpression.Type)));
            }

            Type constantType = inputExpression.Value.GetType();
            if (constantType.IsValueType())
            {
                if (constantType == typeof(bool))
                {
                    SqlBooleanLiteral literal = new SqlBooleanLiteral((bool)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(byte))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((byte)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(sbyte))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((sbyte)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(char))
                {
                    SqlStringLiteral literal = new SqlStringLiteral(inputExpression.Value.ToString());
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(decimal))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral(((decimal)inputExpression.Value));
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(double))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((double)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(float))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((float)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(int))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((int)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(uint))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((uint)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(long))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((long)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(ulong))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((ulong)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(short))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((short)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(ushort))
                {
                    SqlNumberLiteral literal = new SqlNumberLiteral((ushort)inputExpression.Value);
                    return new SqlLiteralScalarExpression(literal);
                }

                if (constantType == typeof(Guid))
                {
                    SqlStringLiteral literal = new SqlStringLiteral(inputExpression.Value.ToString());
                    return new SqlLiteralScalarExpression(literal);
                }
            }

            if (constantType == typeof(string))
            {
                SqlStringLiteral literal = new SqlStringLiteral((string)inputExpression.Value);
                return new SqlLiteralScalarExpression(literal);
            }

            if (typeof(Geometry).IsAssignableFrom(constantType))
            {
                return GeometrySqlExpressionFactory.Construct(inputExpression);
            }

            if (inputExpression.Value is IEnumerable)
            {
                List<SqlScalarExpression> arrayItems = new List<SqlScalarExpression>();

                foreach (var item in ((IEnumerable)(inputExpression.Value)))
                {
                    arrayItems.Add(ExpressionToSql.VisitConstant(Expression.Constant(item)));
                }

                return new SqlArrayCreateScalarExpression(arrayItems.ToArray());
            }

            return new SqlLiteralScalarExpression(new SqlObjectLiteral(inputExpression.Value, false));
        }

        private static SqlScalarExpression VisitConditional(ConditionalExpression inputExpression, TranslationContext context)
        {
            SqlScalarExpression conditionExpression = ExpressionToSql.VisitScalarExpression(inputExpression.Test, context);
            SqlScalarExpression firstExpression = ExpressionToSql.VisitScalarExpression(inputExpression.IfTrue, context);
            SqlScalarExpression secondExpression = ExpressionToSql.VisitScalarExpression(inputExpression.IfFalse, context);

            return new SqlConditionalScalarExpression(conditionExpression, firstExpression, secondExpression);
        }

        private static SqlScalarExpression VisitParameter(ParameterExpression inputExpression, TranslationContext context)
        {
            Expression subst = context.LookupSubstitution(inputExpression);
            if (subst != null)
            {
                return ExpressionToSql.VisitScalarExpression(subst, context);
            }

            string name = inputExpression.Name;
            SqlIdentifier id = new SqlIdentifier(name);
            return new SqlPropertyRefScalarExpression(null, id);
        }

        private static SqlScalarExpression VisitMemberAccess(MemberExpression inputExpression, TranslationContext context)
        {
            SqlScalarExpression memberExpression = ExpressionToSql.VisitScalarExpression(inputExpression.Expression, context);
            string memberName = inputExpression.Member.GetMemberName();

            // if expression is nullable
            if (inputExpression.Expression.Type.IsNullable())
            {
                // ignore .Value 
                if (memberName == "Value")
                {
                    return memberExpression;
                }

                // convert .HasValue to IS_DEFINED expression
                if (memberName == "HasValue")
                {
                    return new SqlFunctionCallScalarExpression(new SqlIdentifier("IS_DEFINED"), new SqlScalarExpression[] { memberExpression });
                }
            }

            if (usePropertyRef)
            {
                SqlIdentifier propertyIdnetifier = new SqlIdentifier(memberName);
                SqlPropertyRefScalarExpression propertyRefExpression = new SqlPropertyRefScalarExpression(memberExpression, propertyIdnetifier);
                return propertyRefExpression;
            }
            else
            {
                SqlScalarExpression indexExpression = new SqlLiteralScalarExpression(new SqlStringLiteral(memberName));
                SqlMemberIndexerScalarExpression memberIndexerExpression = new SqlMemberIndexerScalarExpression(memberExpression, indexExpression);
                return memberIndexerExpression;
            }
        }

        private static SqlScalarExpression[] VisitExpressionList(ReadOnlyCollection<Expression> inputExpressionList, TranslationContext context)
        {
            SqlScalarExpression[] result = new SqlScalarExpression[inputExpressionList.Count];
            for (int i = 0; i < inputExpressionList.Count; i++)
            {
                SqlScalarExpression p = ExpressionToSql.VisitScalarExpression(inputExpressionList[i], context);
                result[i] = p;
            }

            return result;
        }

        private static SqlObjectProperty VisitMemberAssignment(MemberAssignment inputExpression, TranslationContext context)
        {
            SqlScalarExpression assign = ExpressionToSql.VisitScalarExpression(inputExpression.Expression, context);
            string memberName = inputExpression.Member.GetMemberName();
            SqlPropertyName propName = new SqlPropertyName(memberName);
            SqlObjectProperty prop = new SqlObjectProperty(propName, assign);
            return prop;
        }

        private static SqlObjectProperty VisitMemberMemberBinding(MemberMemberBinding inputExpression, TranslationContext context)
        {
            throw new DocumentQueryException(ClientResources.MemberBindingNotSupported);
        }

        private static SqlObjectProperty VisitMemberListBinding(MemberListBinding inputExpression, TranslationContext context)
        {
            throw new DocumentQueryException(ClientResources.MemberBindingNotSupported);
        }

        private static SqlObjectProperty[] VisitBindingList(ReadOnlyCollection<MemberBinding> inputExpressionList, TranslationContext context)
        {
            SqlObjectProperty[] list = new SqlObjectProperty[inputExpressionList.Count];
            for (int i = 0; i < inputExpressionList.Count; i++)
            {
                SqlObjectProperty b = ExpressionToSql.VisitBinding(inputExpressionList[i], context);
                list[i] = b;
            }

            return list;
        }

        private static SqlObjectProperty[] CreateInitializers(ReadOnlyCollection<Expression> arguments, ReadOnlyCollection<MemberInfo> members, TranslationContext context)
        {
            if (arguments.Count != members.Count)
            {
                throw new InvalidOperationException("Expected same number of arguments as members");
            }

            SqlObjectProperty[] result = new SqlObjectProperty[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                Expression arg = arguments[i];
                MemberInfo member = members[i];
                SqlScalarExpression value = ExpressionToSql.VisitScalarExpression(arg, context);

                string memberName = member.GetMemberName();
                SqlPropertyName propName = new SqlPropertyName(memberName);
                SqlObjectProperty prop = new SqlObjectProperty(propName, value);
                result[i] = prop;
            }

            return result;
        }

        private static SqlScalarExpression VisitNew(NewExpression inputExpression, TranslationContext context)
        {
            if (typeof(Geometry).IsAssignableFrom(inputExpression.Type))
            {
                return GeometrySqlExpressionFactory.Construct(inputExpression);
            }

            if (inputExpression.Arguments.Count > 0)
            {
                if (inputExpression.Members == null)
                {
                    throw new DocumentQueryException(ClientResources.ConstructorInvocationNotSupported);
                }

                SqlObjectProperty[] bindings = CreateInitializers(inputExpression.Arguments, inputExpression.Members, context);
                SqlObjectCreateScalarExpression create = new SqlObjectCreateScalarExpression(bindings);
                return create;
            }
            else
            {
                // no need to return anything; the initializer will generate the complete code
                return null;
            }
        }

        private static SqlScalarExpression VisitMemberInit(MemberInitExpression inputExpression, TranslationContext context)
        {
            ExpressionToSql.VisitNew(inputExpression.NewExpression, context); // Return value is ignored
            SqlObjectProperty[] bindings = ExpressionToSql.VisitBindingList(inputExpression.Bindings, context);
            SqlObjectCreateScalarExpression create = new SqlObjectCreateScalarExpression(bindings);
            return create;
        }

        private static SqlScalarExpression VisitListInit(ListInitExpression inputExpression, TranslationContext context)
        {
            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
        }

        private static SqlScalarExpression VisitNewArray(NewArrayExpression inputExpression, TranslationContext context)
        {
            SqlScalarExpression[] exprs = ExpressionToSql.VisitExpressionList(inputExpression.Expressions, context);
            if (inputExpression.NodeType == ExpressionType.NewArrayInit)
            {
                SqlArrayCreateScalarExpression array = new SqlArrayCreateScalarExpression(exprs);
                return array;
            }
            else
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
            }
        }

        private static SqlScalarExpression VisitInvocation(InvocationExpression inputExpression, TranslationContext context)
        {
            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
        }

        #endregion VISITOR

        private static Collection ConvertToCollection(SqlScalarExpression scalar)
        {
            if (usePropertyRef)
            {
                SqlPropertyRefScalarExpression propertyRefExpression = scalar as SqlPropertyRefScalarExpression;
                if (propertyRefExpression == null)
                {
                    throw new DocumentQueryException(ClientResources.PathExpressionsOnly);
                }

                SqlInputPathCollection path = ConvertPropertyRefToPath(propertyRefExpression);
                Collection result = new Collection(path);
                return result;
            }
            else
            {
                SqlMemberIndexerScalarExpression memberIndexerExpression = scalar as SqlMemberIndexerScalarExpression;
                if (memberIndexerExpression == null)
                {
                    SqlPropertyRefScalarExpression propertyRefExpression = scalar as SqlPropertyRefScalarExpression;
                    if (propertyRefExpression == null)
                    {
                        throw new DocumentQueryException(ClientResources.PathExpressionsOnly);
                    }

                    SqlInputPathCollection path = ConvertPropertyRefToPath(propertyRefExpression);
                    Collection result = new Collection(path);
                    return result;
                }
                else
                {
                    SqlInputPathCollection path = ConvertMemberIndexerToPath(memberIndexerExpression);
                    Collection result = new Collection(path);
                    return result;
                }
            }
        }

        private static SqlScalarExpression VisitScalarLambda(Expression inputExpression, TranslationContext context)
        {
            LambdaExpression lambda = Utilities.GetLambda(inputExpression);
            ReadOnlyCollection<ParameterExpression> parms = lambda.Parameters;
            if (parms.Count != 1)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, lambda.Body, 1, parms.Count));
            }

            foreach (ParameterExpression par in parms)
            {
                context.PushParameter(par);
            }

            SqlScalarExpression body = ExpressionToSql.VisitScalarExpression(lambda.Body, context);
            foreach (ParameterExpression par in parms)
            {
                context.PopParameter();
            }

            return body;
        }

        /// <summary>
        /// Visit a lambda which is supposed to return a collection.
        /// </summary>
        /// <param name="inputExpression">LambdaExpression with a result which is a collection.</param>
        /// <param name="context">Translation context.</param>
        /// <returns>The collection computed by the lambda.</returns>
        private static Collection VisitCollectionLambda(Expression inputExpression, TranslationContext context)
        {
            LambdaExpression lambda = Utilities.GetLambda(inputExpression);
            ReadOnlyCollection<ParameterExpression> parms = lambda.Parameters;
            if (parms.Count != 1)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, lambda.Body, 1, parms.Count));
            }

            foreach (ParameterExpression par in parms)
            {
                context.PushParameter(par);
            }

            Collection result;
            switch (lambda.Body.NodeType)
            {
                case ExpressionType.Call:
                    {
                        result = ExpressionToSql.Translate(lambda.Body, context);
                        break;
                    }
                default:
                    {
                        // Generate the equivalent of inserting a Select(tmp => tmp) 
                        SqlScalarExpression body = ExpressionToSql.VisitScalarExpression(lambda.Body, context);
                        Type type = lambda.Body.Type;
                        Type elementType = TypeSystem.GetElementType(type);
                        ParameterExpression tmp = context.GenFreshParameter(type, "tmp");
                        Collection collection = ExpressionToSql.ConvertToCollection(body);
                        context.PushCollection(collection);
                        context.PushParameter(tmp);

                        SqlScalarExpression id = ExpressionToSql.VisitScalarExpression(tmp, context);
                        SqlSelectSpec sqlSpec = new SqlSelectValueSpec(id);
                        SqlSelectClause select = new SqlSelectClause(sqlSpec, null);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, elementType, elementType, context.InScope);

                        context.PopParameter();
                        context.PopCollection();
                        result = new Collection("SelectMany");
                        break;
                    }
            }

            foreach (ParameterExpression par in parms)
            {
                context.PopParameter();
            }

            return result;
        }

        private static Collection TranslateInput(ConstantExpression inputExpression, TranslationContext context)
        {
            if (!typeof(IDocumentQuery).IsAssignableFrom(inputExpression.Type))
            {
                throw new DocumentQueryException(ClientResources.InputIsNotIDocumentQuery);
            }

            // ExpressionToSql is the query input value: a IDocumentQuery
            IDocumentQuery input = inputExpression.Value as IDocumentQuery;
            if (input == null)
            {
                throw new DocumentQueryException(ClientResources.InputIsNotIDocumentQuery);
            }

            context.currentQuery = new QueryUnderConstruction();
            Type elemType = TypeSystem.GetElementType(inputExpression.Type);
            context.SetInputParameter(elemType, ParameterSubstitution.InputParameterName); // ignore result

            // First outer collection
            Collection result = new Collection("INPUT");
            return result;
        }

        /// <summary>
        /// Visit a method call, construct the corresponding query in context.currentQuery.
        /// At ExpressionToSql point only LINQ method calls are allowed.
        /// These methods are static extension methods of IQueryable or IEnumerable.
        /// </summary>
        /// <param name="inputExpression">Method to translate.</param>
        /// <param name="context">Query translation context.</param>
        private static Collection VisitMethodCall(MethodCallExpression inputExpression, TranslationContext context)
        {
            context.PushMethod(inputExpression);

            Type declaringType = inputExpression.Method.DeclaringType;
            if ((declaringType != typeof(Queryable) && declaringType != typeof(Enumerable))
                || !inputExpression.Method.IsStatic)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.OnlyLINQMethodsAreSupported, inputExpression.Method.Name));
            }

            Type returnType = inputExpression.Method.ReturnType;
            Type returnElementType = TypeSystem.GetElementType(returnType);

            if (inputExpression.Object != null)
            {
                throw new DocumentQueryException(ClientResources.ExpectedMethodCallsMethods);
            }

            Expression inputCollection = inputExpression.Arguments[0]; // all these methods are static extension methods, so argument[0] is the collection

            Type inputElementType = TypeSystem.GetElementType(inputCollection.Type);
            Collection collection = ExpressionToSql.Translate(inputCollection, context);
            context.PushCollection(collection);

            Collection result;
            switch (inputExpression.Method.Name)
            {
                case LinqMethods.Select:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitSelect(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.Where:
                    {
                        result = new Collection("");
                        SqlWhereClause where = ExpressionToSql.VisitWhere(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddWhereClause(where, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.SelectMany:
                    {
                        result = ExpressionToSql.VisitSelectMany(inputExpression.Arguments, context);
                        break;
                    }
                case LinqMethods.OrderBy:
                    {
                        result = new Collection("");
                        SqlOrderbyClause orderBy = ExpressionToSql.VisitOrderBy(inputExpression.Arguments, false, context);
                        context.currentQuery = context.currentQuery.AddOrderByClause(orderBy, context.InScope);
                        break;
                    }
                case LinqMethods.OrderByDescending:
                    {
                        result = new Collection("");
                        SqlOrderbyClause orderBy = ExpressionToSql.VisitOrderBy(inputExpression.Arguments, true, context);
                        context.currentQuery = context.currentQuery.AddOrderByClause(orderBy, context.InScope);
                        break;
                    }
                case LinqMethods.Take:
                    {
                        result = new Collection("");
                        SqlTopSpec topSpec = ExpressionToSql.VisitTake(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddTopSpec(topSpec);
                        break;
                    }
                case LinqMethods.Distinct:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitDistinct(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.Max:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, LinqMethods.Max);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.Min:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, LinqMethods.Min);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.Average:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, LinqMethods.SqlAvg);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.Count:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitCount(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                case LinqMethods.Sum:
                    {
                        result = new Collection("");
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, LinqMethods.Sum);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, inputElementType, returnElementType, context.InScope);
                        break;
                    }
                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, inputExpression.Method.Name));
            }

            context.PopCollection();
            context.PopMethod();
            return result;
        }

        #region LINQ_SPECIFIC
        private static SqlWhereClause VisitWhere(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, "Where", 2, arguments.Count));
            }

            Expression function = arguments[1];
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarLambda(function, context);
            SqlWhereClause where = new SqlWhereClause(sqlfunc);
            return where;
        }

        private static SqlSelectClause VisitSelect(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, "Select", 2, arguments.Count));
            }

            Expression function = arguments[1];
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarLambda(function, context);
            SqlSelectSpec sqlSpec = new SqlSelectValueSpec(sqlfunc);
            SqlSelectClause select = new SqlSelectClause(sqlSpec, null);
            return select;
        }

        private static Collection VisitSelectMany(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, "SelectMany", 2, arguments.Count));
            }

            Expression function = arguments[1];
            Collection collection = ExpressionToSql.VisitCollectionLambda(function, context);
            return collection;
        }

        private static SqlOrderbyClause VisitOrderBy(ReadOnlyCollection<Expression> arguments, bool isDescending, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, "OrderBy", 2, arguments.Count));
            }

            Expression function = arguments[1];
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarLambda(function, context);
            SqlOrderbyItem orderByItem = new SqlOrderbyItem(sqlfunc, isDescending);
            SqlOrderbyClause orderby = new SqlOrderbyClause(new SqlOrderbyItem[] { orderByItem });
            return orderby;
        }

        private static SqlTopSpec VisitTake(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, "Take", 2, arguments.Count));
            }

            SqlScalarExpression takeExpression = ExpressionToSql.VisitScalarExpression(arguments[1], context);

            // takeExpression must be number literal
            if (takeExpression is SqlLiteralScalarExpression)
            {
                SqlLiteralScalarExpression takeLiteralExpression = (SqlLiteralScalarExpression)takeExpression;
                if (takeLiteralExpression.Literal != null &&
                    takeLiteralExpression.Literal.Kind == SqlObjectKind.NumberLiteral)
                {
                    SqlNumberLiteral takeNumberLiteral = (SqlNumberLiteral)takeLiteralExpression.Literal;
                    SqlTopSpec topSpec = new SqlTopSpec((int)takeNumberLiteral.Value);
                    return topSpec;
                }
            }

            // .Take() has only one overload that takes int
            // so we really should always get a number (integer) literal here
            // the below throw serves as assert
            throw new ArgumentException(ClientResources.InvalidTakeValue);
        }

        private static SqlSelectClause VisitAggregateFunction(
            ReadOnlyCollection<Expression> arguments,
            TranslationContext context,
            string functionName)
        {
            SqlScalarExpression aggregateExpression;
            if (arguments.Count == 1)
            {
                aggregateExpression = new SqlPropertyRefScalarExpression(null, RootIdentifier);
            }
            else if (arguments.Count == 2)
            {
                Expression function = arguments[1];
                aggregateExpression = ExpressionToSql.VisitScalarLambda(function, context);
            }
            else
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, functionName, 2, arguments.Count));
            }

            SqlSelectSpec selectSpec = new SqlSelectValueSpec(
                new SqlFunctionCallScalarExpression(
                    new SqlIdentifier(functionName),
                    new SqlScalarExpression[] { aggregateExpression }));

            SqlSelectClause selectClause = new SqlSelectClause(selectSpec, null);

            return selectClause;
        }

        private static SqlSelectClause VisitDistinct(
            ReadOnlyCollection<Expression> arguments,
            TranslationContext context)
        {
            string functionName = LinqMethods.Distinct;
            if (arguments.Count != 1)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, functionName, 1, arguments.Count));
            }

            // We consider Distinct as Distinct(var => var)
            // It's necessary to visit this identity method to replace the parameters names
            ParameterExpression tmp = context.GenFreshParameter(typeof(object), "tmp");
            Expression identityFunc = Expression.Lambda(tmp, tmp);
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarLambda(identityFunc, context);
            SqlSelectSpec sqlSpec = new SqlSelectValueSpec(sqlfunc);
            SqlSelectClause select = new SqlSelectClause(sqlSpec, topSpec: null, hasDistinct: true);
            return select;
        }

        private static SqlSelectClause VisitCount(
            ReadOnlyCollection<Expression> arguments,
            TranslationContext context)
        {
            SqlScalarExpression countExpression;
            if (arguments.Count == 1)
            {
                countExpression = new SqlLiteralScalarExpression(new SqlNumberLiteral(1));
            }
            else if (arguments.Count == 2)
            {
                // TODO: replace root with the projected property if any
                countExpression = new SqlPropertyRefScalarExpression(null, RootIdentifier);
                SqlWhereClause whereClause = VisitWhere(arguments, context);
                context.currentQuery = context.currentQuery.AddWhereClause(whereClause, typeof(bool), context.InScope);
            }
            else
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Count, 2, arguments.Count));
            }

            SqlSelectSpec selectSpec = new SqlSelectValueSpec(
                new SqlFunctionCallScalarExpression(
                    new SqlIdentifier(LinqMethods.Count),
                    new SqlScalarExpression[] { countExpression }));

            SqlSelectClause selectClause = new SqlSelectClause(selectSpec, null);

            return selectClause;
        }

        /// <summary>
        /// Property references that refer to array-valued properties are converted to collection references.
        /// </summary>
        /// <param name="propRef">Property reference object.</param>
        /// <returns>An inputPathCollection which contains the same property path as the propRef.</returns>
        private static SqlInputPathCollection ConvertPropertyRefToPath(SqlPropertyRefScalarExpression propRef)
        {
            List<SqlIdentifier> identifiers = new List<SqlIdentifier>();
            while (true)
            {
                identifiers.Add(propRef.PropertyIdentifier);
                SqlScalarExpression parent = propRef.MemberExpression;
                if (parent == null)
                {
                    break;
                }

                if (parent is SqlPropertyRefScalarExpression)
                {
                    propRef = parent as SqlPropertyRefScalarExpression;
                }
                else
                {
                    throw new DocumentQueryException(ClientResources.NotSupported);
                }
            }

            if (identifiers.Count == 0)
            {
                throw new DocumentQueryException(ClientResources.NotSupported);
            }

            SqlPathExpression path = null;
            for (int i = identifiers.Count - 2; i >= 0; i--)
            {
                SqlIdentifier identifer = identifiers[i];
                path = new SqlIdentifierPathExpression(path, identifer);
            }

            SqlIdentifier last = identifiers[identifiers.Count - 1];
            SqlInputPathCollection result = new SqlInputPathCollection(last, path);
            return result;
        }

        private static SqlInputPathCollection ConvertMemberIndexerToPath(SqlMemberIndexerScalarExpression memberIndexerExpression)
        {
            // root.Children.Age ==> root["Children"]["Age"]
            List<SqlStringLiteral> literals = new List<SqlStringLiteral>();
            while (true)
            {
                literals.Add((SqlStringLiteral)((SqlLiteralScalarExpression)memberIndexerExpression.IndexExpression).Literal);
                SqlScalarExpression parent = memberIndexerExpression.MemberExpression;
                if (parent == null)
                {
                    break;
                }

                if (parent is SqlPropertyRefScalarExpression)
                {
                    literals.Add(new SqlStringLiteral(((SqlPropertyRefScalarExpression)parent).PropertyIdentifier.Value));
                    break;
                }

                if (parent is SqlMemberIndexerScalarExpression)
                {
                    memberIndexerExpression = parent as SqlMemberIndexerScalarExpression;
                }
                else
                {
                    throw new DocumentQueryException(ClientResources.NotSupported);
                }
            }

            if (literals.Count == 0)
            {
                throw new ArgumentException("memberIndexerExpression");
            }

            SqlPathExpression path = null;
            for (int i = literals.Count - 2; i >= 0; i--)
            {
                path = new SqlStringPathExpression(path, literals[i]);
            }

            SqlInputPathCollection result = new SqlInputPathCollection(new SqlIdentifier(literals[literals.Count - 1].Value), path);
            return result;
        }

        #endregion LINQ_SPECIFIC
    }
}
