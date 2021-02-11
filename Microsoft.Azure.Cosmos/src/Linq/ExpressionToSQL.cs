//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Linq.FromParameterBindings;

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
        public static class LinqMethods
        {
            public const string Any = "Any";
            public const string Average = "Average";
            public const string Count = "Count";
            public const string Max = "Max";
            public const string Min = "Min";
            public const string OrderBy = "OrderBy";
            public const string ThenBy = "ThenBy";
            public const string OrderByDescending = "OrderByDescending";
            public const string ThenByDescending = "ThenByDescending";
            public const string Select = "Select";
            public const string SelectMany = "SelectMany";
            public const string Sum = "Sum";
            public const string Skip = "Skip";
            public const string Take = "Take";
            public const string Distinct = "Distinct";
            public const string Where = "Where";
        }

        private static string SqlRoot = "root";
        private static string DefaultParameterName = "v";
        private static bool usePropertyRef = false;
        private static SqlIdentifier RootIdentifier = SqlIdentifier.Create(SqlRoot);

        /// <summary>
        /// Toplevel entry point.
        /// </summary>
        /// <param name="inputExpression">An Expression representing a Query on a IDocumentQuery object.</param>
        /// <param name="parameters">Optional dictionary for parameter name and value</param>
        /// <param name="serializationOptions">Optional serializer options.</param>
        /// <returns>The corresponding SQL query.</returns>
        public static SqlQuery TranslateQuery(
            Expression inputExpression,
            IDictionary<object, string> parameters,
            CosmosSerializationOptions serializationOptions)
        {
            TranslationContext context = new TranslationContext(serializationOptions, parameters);
            ExpressionToSql.Translate(inputExpression, context); // ignore result here

            QueryUnderConstruction query = context.currentQuery;
            query = query.FlattenAsPossible();
            SqlQuery result = query.GetSqlQuery();

            return result;
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
                    MethodCallExpression methodCallExpression = (MethodCallExpression)inputExpression;
                    bool shouldConvertToScalarAnyCollection = (context.PeekMethod() == null) && methodCallExpression.Method.Name.Equals(LinqMethods.Any);
                    collection = ExpressionToSql.VisitMethodCall(methodCallExpression, context);
                    if (shouldConvertToScalarAnyCollection) collection = ExpressionToSql.ConvertToScalarAnyCollection(context);

                    break;

                case ExpressionType.Constant:
                    collection = ExpressionToSql.TranslateInput((ConstantExpression)inputExpression, context);
                    break;

                case ExpressionType.MemberAccess:
                    collection = ExpressionToSql.VisitMemberAccessCollectionExpression(inputExpression, context, ExpressionToSql.GetBindingParameterName(context));
                    break;

                case ExpressionType.Parameter:
                    SqlScalarExpression scalar = ExpressionToSql.VisitNonSubqueryScalarExpression(inputExpression, context);
                    collection = ExpressionToSql.ConvertToCollection(scalar);
                    break;

                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
            }
            return collection;
        }

        private static Collection TranslateInput(ConstantExpression inputExpression, TranslationContext context)
        {
            if (!typeof(IDocumentQuery).IsAssignableFrom(inputExpression.Type))
            {
                throw new DocumentQueryException(ClientResources.InputIsNotIDocumentQuery);
            }

            // ExpressionToSql is the query input value: a IDocumentQuery
            if (!(inputExpression.Value is IDocumentQuery input))
            {
                throw new DocumentQueryException(ClientResources.InputIsNotIDocumentQuery);
            }

            context.currentQuery = new QueryUnderConstruction(context.GetGenFreshParameterFunc());
            Type elemType = TypeSystem.GetElementType(inputExpression.Type);
            context.SetInputParameter(elemType, ParameterSubstitution.InputParameterName); // ignore result

            // First outer collection
            Collection result = new Collection(ExpressionToSql.SqlRoot);
            return result;
        }

        /// <summary>
        /// Get a paramter name to be binded to the a collection from the next lambda.
        /// It's merely for readability purpose. If that is not possible, use a default 
        /// parameter name.
        /// </summary>
        /// <param name="context">The translation context</param>
        /// <returns>A parameter name</returns>
        private static string GetBindingParameterName(TranslationContext context)
        {
            MethodCallExpression peekMethod = context.PeekMethod();

            // The parameter name is the top method's parameter if applicable
            string parameterName = null;
            if (peekMethod.Arguments.Count > 1)
            {
                if (peekMethod.Arguments[1] is LambdaExpression lambda && lambda.Parameters.Count > 0)
                {
                    parameterName = lambda.Parameters[0].Name;
                }
            }

            if (parameterName == null) parameterName = ExpressionToSql.DefaultParameterName;

            return parameterName;
        }

        #region VISITOR

        /// <summary>
        /// Visitor which produces a SqlScalarExpression.
        /// </summary>
        /// <param name="inputExpression">Expression to visit.</param>
        /// <param name="context">Context information.</param>
        /// <returns>The translation as a ScalarExpression.</returns>
        internal static SqlScalarExpression VisitNonSubqueryScalarExpression(Expression inputExpression, TranslationContext context)
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
                    return ExpressionToSql.VisitConstant((ConstantExpression)inputExpression, context);
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
            if (methodCallExpression.Method.Equals(typeof(CosmosLinq).GetMethod("InvokeUserDefinedFunction")))
            {
                string udfName = ((ConstantExpression)methodCallExpression.Arguments[0]).Value as string;
                if (string.IsNullOrEmpty(udfName))
                {
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.UdfNameIsNullOrEmpty));
                }

                SqlIdentifier methodName = SqlIdentifier.Create(udfName);
                List<SqlScalarExpression> arguments = new List<SqlScalarExpression>();

                if (methodCallExpression.Arguments.Count == 2)
                {
                    // We have two cases here, if the udf was expecting only one parameter and this parameter is an array
                    // then the second argument will be an expression of this array.
                    // else we will have a NewArrayExpression of the udf arguments
                    if (methodCallExpression.Arguments[1] is NewArrayExpression newArrayExpression)
                    {
                        ReadOnlyCollection<Expression> argumentsExpressions = newArrayExpression.Expressions;
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
                            arguments.Add(ExpressionToSql.VisitConstant(Expression.Constant(argument), context));
                        }
                    }
                    else
                    {
                        arguments.Add(ExpressionToSql.VisitScalarExpression(methodCallExpression.Arguments[1], context));
                    }
                }

                return SqlFunctionCallScalarExpression.Create(methodName, true, arguments.ToImmutableArray());
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
            if (operand is SqlInScalarExpression sqlInScalarExpression && inputExpression.NodeType == ExpressionType.Not)
            {
                SqlInScalarExpression inExpression = sqlInScalarExpression;
                return SqlInScalarExpression.Create(inExpression.Needle, true, inExpression.Haystack);
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
            return SqlUnaryScalarExpression.Create(op, operand);
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
                SqlMemberIndexerScalarExpression result = SqlMemberIndexerScalarExpression.Create(left, right);
                return result;
            }

            SqlBinaryScalarOperatorKind op = GetBinaryOperatorKind(inputExpression.NodeType, inputExpression.Type);

            if (left is SqlMemberIndexerScalarExpression && right is SqlLiteralScalarExpression literalScalarExpression)
            {
                right = ExpressionToSql.ApplyCustomConverters(inputExpression.Left, literalScalarExpression);
            }
            else if (right is SqlMemberIndexerScalarExpression && left is SqlLiteralScalarExpression sqlLiteralScalarExpression)
            {
                left = ExpressionToSql.ApplyCustomConverters(inputExpression.Right, sqlLiteralScalarExpression);
            }

            return SqlBinaryScalarExpression.Create(op, left, right);
        }

        private static SqlScalarExpression ApplyCustomConverters(Expression left, SqlLiteralScalarExpression right)
        {
            MemberExpression memberExpression;
            if (left is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
            else
            {
                memberExpression = left as MemberExpression;
            }

            if (memberExpression != null)
            {
                Type memberType = memberExpression.Type;
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
                CustomAttributeData memberAttribute = memberExpression.Member.CustomAttributes.Where(ca => ca.AttributeType == typeof(JsonConverterAttribute)).FirstOrDefault();
                CustomAttributeData typeAttribute = memberType.GetsCustomAttributes().Where(ca => ca.AttributeType == typeof(JsonConverterAttribute)).FirstOrDefault();

                CustomAttributeData converterAttribute = memberAttribute ?? typeAttribute;
                if (converterAttribute != null)
                {
                    Debug.Assert(converterAttribute.ConstructorArguments.Count > 0);

                    Type converterType = (Type)converterAttribute.ConstructorArguments[0].Value;

                    object value = default(object);
                    // Enum
                    if (memberType.IsEnum())
                    {
                        Number64 number64 = ((SqlNumberLiteral)right.Literal).Value;
                        if (number64.IsDouble)
                        {
                            value = Enum.ToObject(memberType, Number64.ToDouble(number64));
                        }
                        else
                        {
                            value = Enum.ToObject(memberType, Number64.ToLong(number64));
                        }

                    }
                    // DateTime
                    else if (memberType == typeof(DateTime))
                    {
                        SqlStringLiteral serializedDateTime = (SqlStringLiteral)right.Literal;
                        value = DateTime.Parse(serializedDateTime.Value);
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

                        return CosmosElement.Parse(serializedValue).Accept(CosmosElementToSqlScalarExpressionVisitor.Singleton);
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
                if (!(right.Type == typeof(int) && (int)right.Value == 0) &&
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

            SqlScalarExpression leftExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(left.Object, context);
            SqlScalarExpression rightExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(left.Arguments[0], context);

            return SqlBinaryScalarExpression.Create(op, leftExpression, rightExpression);
        }

        private static SqlScalarExpression VisitTypeIs(TypeBinaryExpression inputExpression, TranslationContext context)
        {
            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, inputExpression.NodeType));
        }

        public static SqlScalarExpression VisitConstant(ConstantExpression inputExpression, TranslationContext context)
        {
            if (inputExpression.Value == null)
            {
                return SqlLiteralScalarExpression.SqlNullLiteralScalarExpression;
            }

            if (inputExpression.Type.IsNullable())
            {
                return ExpressionToSql.VisitConstant(Expression.Constant(inputExpression.Value, Nullable.GetUnderlyingType(inputExpression.Type)), context);
            }

            if (context.parameters != null && context.parameters.TryGetValue(inputExpression.Value, out string paramName))
            {
                SqlParameter sqlParameter = SqlParameter.Create(paramName);
                return SqlParameterRefScalarExpression.Create(sqlParameter);
            }

            Type constantType = inputExpression.Value.GetType();
            if (constantType.IsValueType())
            {
                if (inputExpression.Value is bool boolValue)
                {
                    SqlBooleanLiteral literal = SqlBooleanLiteral.Create(boolValue);
                    return SqlLiteralScalarExpression.Create(literal);
                }

                if (ExpressionToSql.TryGetSqlNumberLiteral(inputExpression.Value, out SqlNumberLiteral numberLiteral))
                {
                    return SqlLiteralScalarExpression.Create(numberLiteral);
                }

                if (inputExpression.Value is Guid guidValue)
                {
                    SqlStringLiteral literal = SqlStringLiteral.Create(guidValue.ToString());
                    return SqlLiteralScalarExpression.Create(literal);
                }
            }

            if (inputExpression.Value is string stringValue)
            {
                SqlStringLiteral literal = SqlStringLiteral.Create(stringValue);
                return SqlLiteralScalarExpression.Create(literal);
            }

            if (typeof(Geometry).IsAssignableFrom(constantType))
            {
                return GeometrySqlExpressionFactory.Construct(inputExpression);
            }

            if (inputExpression.Value is IEnumerable enumerable)
            {
                List<SqlScalarExpression> arrayItems = new List<SqlScalarExpression>();

                foreach (object item in enumerable)
                {
                    arrayItems.Add(ExpressionToSql.VisitConstant(Expression.Constant(item), context));
                }

                return SqlArrayCreateScalarExpression.Create(arrayItems.ToImmutableArray());
            }

            return CosmosElement.Parse(JsonConvert.SerializeObject(inputExpression.Value)).Accept(CosmosElementToSqlScalarExpressionVisitor.Singleton);
        }

        private static SqlScalarExpression VisitConditional(ConditionalExpression inputExpression, TranslationContext context)
        {
            SqlScalarExpression conditionExpression = ExpressionToSql.VisitScalarExpression(inputExpression.Test, context);
            SqlScalarExpression firstExpression = ExpressionToSql.VisitScalarExpression(inputExpression.IfTrue, context);
            SqlScalarExpression secondExpression = ExpressionToSql.VisitScalarExpression(inputExpression.IfFalse, context);

            return SqlConditionalScalarExpression.Create(conditionExpression, firstExpression, secondExpression);
        }

        private static SqlScalarExpression VisitParameter(ParameterExpression inputExpression, TranslationContext context)
        {
            Expression subst = context.LookupSubstitution(inputExpression);
            if (subst != null)
            {
                return ExpressionToSql.VisitNonSubqueryScalarExpression(subst, context);
            }

            string name = inputExpression.Name;
            SqlIdentifier id = SqlIdentifier.Create(name);
            return SqlPropertyRefScalarExpression.Create(null, id);
        }

        private static SqlScalarExpression VisitMemberAccess(MemberExpression inputExpression, TranslationContext context)
        {
            SqlScalarExpression memberExpression = ExpressionToSql.VisitScalarExpression(inputExpression.Expression, context);
            string memberName = inputExpression.Member.GetMemberName(context?.serializationOptions);

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
                    return SqlFunctionCallScalarExpression.CreateBuiltin("IS_DEFINED", memberExpression);
                }
            }

            if (usePropertyRef)
            {
                SqlIdentifier propertyIdnetifier = SqlIdentifier.Create(memberName);
                SqlPropertyRefScalarExpression propertyRefExpression = SqlPropertyRefScalarExpression.Create(memberExpression, propertyIdnetifier);
                return propertyRefExpression;
            }
            else
            {
                SqlScalarExpression indexExpression = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create(memberName));
                SqlMemberIndexerScalarExpression memberIndexerExpression = SqlMemberIndexerScalarExpression.Create(memberExpression, indexExpression);
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
            string memberName = inputExpression.Member.GetMemberName(context?.serializationOptions);
            SqlPropertyName propName = SqlPropertyName.Create(memberName);
            SqlObjectProperty prop = SqlObjectProperty.Create(propName, assign);
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

                string memberName = member.GetMemberName(context?.serializationOptions);
                SqlPropertyName propName = SqlPropertyName.Create(memberName);
                SqlObjectProperty prop = SqlObjectProperty.Create(propName, value);
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

                SqlObjectProperty[] propertyBindings = ExpressionToSql.CreateInitializers(inputExpression.Arguments, inputExpression.Members, context);
                SqlObjectCreateScalarExpression create = SqlObjectCreateScalarExpression.Create(propertyBindings);
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
            SqlObjectProperty[] propertyBindings = ExpressionToSql.VisitBindingList(inputExpression.Bindings, context);
            SqlObjectCreateScalarExpression create = SqlObjectCreateScalarExpression.Create(propertyBindings);
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
                SqlArrayCreateScalarExpression array = SqlArrayCreateScalarExpression.Create(exprs);
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

        #region Scalar and CollectionScalar Visitors

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

        /// <summary>
        /// Convert the context's current query to a scalar Any collection
        /// by wrapping it as following: SELECT VALUE COUNT(v0) > 0 FROM (current query) AS v0.
        /// This is used in cases where LINQ expression ends with Any() which is a boolean scalar.
        /// Normally Any would translate to SELECT VALUE EXISTS() subquery. However that wouldn't work
        /// for these cases because it would result in a boolean value for each row instead of 
        /// one single "aggregated" boolean value.
        /// </summary>
        /// <param name="context">The translation context</param>
        /// <returns>The scalar Any collection</returns>
        private static Collection ConvertToScalarAnyCollection(TranslationContext context)
        {
            SqlQuery query = context.currentQuery.FlattenAsPossible().GetSqlQuery();
            SqlCollection subqueryCollection = SqlSubqueryCollection.Create(query);

            ParameterExpression parameterExpression = context.GenFreshParameter(typeof(object), ExpressionToSql.DefaultParameterName);
            Binding binding = new Binding(parameterExpression, subqueryCollection, isInCollection: false, isInputParameter: true);

            context.currentQuery = new QueryUnderConstruction(context.GetGenFreshParameterFunc());
            context.currentQuery.AddBinding(binding);

            SqlSelectSpec selectSpec = SqlSelectValueSpec.Create(
                SqlBinaryScalarExpression.Create(
                    SqlBinaryScalarOperatorKind.GreaterThan,
                    SqlFunctionCallScalarExpression.CreateBuiltin(
                        SqlFunctionCallScalarExpression.Names.Count,
                        SqlPropertyRefScalarExpression.Create(null, SqlIdentifier.Create(parameterExpression.Name))),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0))));
            SqlSelectClause selectClause = SqlSelectClause.Create(selectSpec);
            context.currentQuery.AddSelectClause(selectClause);

            return new Collection(LinqMethods.Any);
        }

        private static SqlScalarExpression VisitNonSubqueryScalarExpression(Expression expression, ReadOnlyCollection<ParameterExpression> parameters, TranslationContext context)
        {
            foreach (ParameterExpression par in parameters)
            {
                context.PushParameter(par, context.CurrentSubqueryBinding.ShouldBeOnNewQuery);
            }

            SqlScalarExpression scalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(expression, context);

            foreach (ParameterExpression par in parameters)
            {
                context.PopParameter();
            }

            return scalarExpression;
        }

        private static SqlScalarExpression VisitNonSubqueryScalarLambda(LambdaExpression lambdaExpression, TranslationContext context)
        {
            ReadOnlyCollection<ParameterExpression> parameters = lambdaExpression.Parameters;
            if (parameters.Count != 1)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, lambdaExpression.Body, 1, parameters.Count));
            }

            return ExpressionToSql.VisitNonSubqueryScalarExpression(lambdaExpression.Body, parameters, context);
        }

        private static Collection VisitCollectionExpression(Expression expression, ReadOnlyCollection<ParameterExpression> parameters, TranslationContext context)
        {
            foreach (ParameterExpression par in parameters)
            {
                context.PushParameter(par, context.CurrentSubqueryBinding.ShouldBeOnNewQuery);
            }

            Collection collection = ExpressionToSql.VisitCollectionExpression(expression, context, parameters.Count > 0 ? parameters.First().Name : ExpressionToSql.DefaultParameterName);

            foreach (ParameterExpression par in parameters)
            {
                context.PopParameter();
            }

            return collection;
        }

        private static Collection VisitCollectionExpression(Expression expression, TranslationContext context, string parameterName)
        {
            Collection result;
            switch (expression.NodeType)
            {
                case ExpressionType.Call:
                    result = ExpressionToSql.Translate(expression, context);
                    break;

                case ExpressionType.MemberAccess:
                    result = ExpressionToSql.VisitMemberAccessCollectionExpression(expression, context, parameterName);
                    break;

                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.ExpressionTypeIsNotSupported, expression.NodeType));
            }

            return result;
        }

        /// <summary>
        /// Visit a lambda which is supposed to return a collection.
        /// </summary>
        /// <param name="lambdaExpression">LambdaExpression with a result which is a collection.</param>
        /// <param name="context">The translation context.</param>
        /// <returns>The collection computed by the lambda.</returns>
        private static Collection VisitCollectionLambda(LambdaExpression lambdaExpression, TranslationContext context)
        {
            ReadOnlyCollection<ParameterExpression> parameters = lambdaExpression.Parameters;
            if (parameters.Count != 1)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, lambdaExpression.Body, 1, parameters.Count));
            }

            return ExpressionToSql.VisitCollectionExpression(lambdaExpression.Body, lambdaExpression.Parameters, context);
        }

        /// <summary>
        /// Visit an expression, usually a MemberAccess, then trigger parameter binding for that expression.
        /// </summary>
        /// <param name="inputExpression">The input expression</param>
        /// <param name="context">The current translation context</param>
        /// <param name="parameterName">Parameter name is merely for readability</param>
        private static Collection VisitMemberAccessCollectionExpression(Expression inputExpression, TranslationContext context, string parameterName)
        {
            SqlScalarExpression body = ExpressionToSql.VisitNonSubqueryScalarExpression(inputExpression, context);
            Type type = inputExpression.Type;

            Collection collection = ExpressionToSql.ConvertToCollection(body);
            context.PushCollection(collection);
            ParameterExpression parameter = context.GenFreshParameter(type, parameterName);
            context.PushParameter(parameter, context.CurrentSubqueryBinding.ShouldBeOnNewQuery);
            context.PopParameter();
            context.PopCollection();

            return new Collection(parameter.Name);
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

            Collection result = new Collection(inputExpression.Method.Name);
            bool shouldBeOnNewQuery = context.currentQuery.ShouldBeOnNewQuery(inputExpression.Method.Name, inputExpression.Arguments.Count);
            context.PushSubqueryBinding(shouldBeOnNewQuery);
            switch (inputExpression.Method.Name)
            {
                case LinqMethods.Select:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitSelect(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Where:
                    {
                        SqlWhereClause where = ExpressionToSql.VisitWhere(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddWhereClause(where, context);
                        break;
                    }
                case LinqMethods.SelectMany:
                    {
                        context.currentQuery = context.PackageCurrentQueryIfNeccessary();
                        result = ExpressionToSql.VisitSelectMany(inputExpression.Arguments, context);
                        break;
                    }
                case LinqMethods.OrderBy:
                    {
                        SqlOrderByClause orderBy = ExpressionToSql.VisitOrderBy(inputExpression.Arguments, false, context);
                        context.currentQuery = context.currentQuery.AddOrderByClause(orderBy, context);
                        break;
                    }
                case LinqMethods.OrderByDescending:
                    {
                        SqlOrderByClause orderBy = ExpressionToSql.VisitOrderBy(inputExpression.Arguments, true, context);
                        context.currentQuery = context.currentQuery.AddOrderByClause(orderBy, context);
                        break;
                    }
                case LinqMethods.ThenBy:
                    {
                        SqlOrderByClause thenBy = ExpressionToSql.VisitOrderBy(inputExpression.Arguments, false, context);
                        context.currentQuery = context.currentQuery.UpdateOrderByClause(thenBy, context);
                        break;
                    }
                case LinqMethods.ThenByDescending:
                    {
                        SqlOrderByClause thenBy = ExpressionToSql.VisitOrderBy(inputExpression.Arguments, true, context);
                        context.currentQuery = context.currentQuery.UpdateOrderByClause(thenBy, context);
                        break;
                    }
                case LinqMethods.Skip:
                    {
                        SqlOffsetSpec offsetSpec = ExpressionToSql.VisitSkip(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddOffsetSpec(offsetSpec, context);
                        break;
                    }
                case LinqMethods.Take:
                    {
                        if (context.currentQuery.HasOffsetSpec())
                        {
                            SqlLimitSpec limitSpec = ExpressionToSql.VisitTakeLimit(inputExpression.Arguments, context);
                            context.currentQuery = context.currentQuery.AddLimitSpec(limitSpec, context);
                        }
                        else
                        {
                            SqlTopSpec topSpec = ExpressionToSql.VisitTakeTop(inputExpression.Arguments, context);
                            context.currentQuery = context.currentQuery.AddTopSpec(topSpec);
                        }
                        break;
                    }
                case LinqMethods.Distinct:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitDistinct(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Max:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, SqlFunctionCallScalarExpression.Names.Max);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Min:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, SqlFunctionCallScalarExpression.Names.Min);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Average:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, SqlFunctionCallScalarExpression.Names.Avg);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Count:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitCount(inputExpression.Arguments, context);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Sum:
                    {
                        SqlSelectClause select = ExpressionToSql.VisitAggregateFunction(inputExpression.Arguments, context, SqlFunctionCallScalarExpression.Names.Sum);
                        context.currentQuery = context.currentQuery.AddSelectClause(select, context);
                        break;
                    }
                case LinqMethods.Any:
                    {
                        result = new Collection(string.Empty);
                        if (inputExpression.Arguments.Count == 2)
                        {
                            // Any is translated to an SELECT VALUE EXISTS() where Any operation itself is treated as a Where.
                            SqlWhereClause where = ExpressionToSql.VisitWhere(inputExpression.Arguments, context);
                            context.currentQuery = context.currentQuery.AddWhereClause(where, context);
                        }
                        break;
                    }
                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, inputExpression.Method.Name));
            }

            context.PopSubqueryBinding();
            context.PopCollection();
            context.PopMethod();
            return result;
        }

        /// <summary>
        /// Determine if an expression should be translated to a subquery.
        /// This only applies to expression that is inside a lamda.
        /// </summary>
        /// <param name="expression">The input expression</param>
        /// <param name="expressionObjKind">The expression object kind of the expression</param>
        /// <param name="isMinMaxAvgMethod">True if the method is either Min, Max, or Avg</param>
        /// <returns>True if subquery is needed, otherwise false</returns>
        private static bool IsSubqueryScalarExpression(Expression expression, out SubqueryKind? expressionObjKind, out bool isMinMaxAvgMethod)
        {
            if (!(expression is MethodCallExpression methodCallExpression))
            {
                expressionObjKind = null;
                isMinMaxAvgMethod = false;
                return false;
            }

            string methodName = methodCallExpression.Method.Name;
            bool isSubqueryExpression;

            isMinMaxAvgMethod = false;

            switch (methodName)
            {
                case LinqMethods.Min:
                case LinqMethods.Max:
                case LinqMethods.Average:
                    isMinMaxAvgMethod = true;
                    isSubqueryExpression = true;
                    expressionObjKind = SubqueryKind.SubqueryScalarExpression;
                    break;

                case LinqMethods.Sum:
                    isSubqueryExpression = true;
                    expressionObjKind = SubqueryKind.SubqueryScalarExpression;
                    break;

                case LinqMethods.Count:
                    if (methodCallExpression.Arguments.Count > 1)
                    {
                        isSubqueryExpression = true;
                        expressionObjKind = SubqueryKind.SubqueryScalarExpression;
                    }
                    else
                    {
                        SubqueryKind? objKind;
                        bool isMinMaxAvg;
                        isSubqueryExpression = ExpressionToSql.IsSubqueryScalarExpression(
                            methodCallExpression.Arguments[0] as MethodCallExpression,
                            out objKind, out isMinMaxAvg);

                        if (isSubqueryExpression)
                        {
                            isSubqueryExpression = true;
                            expressionObjKind = SubqueryKind.SubqueryScalarExpression;
                        }
                        else
                        {
                            isSubqueryExpression = false;
                            expressionObjKind = null;
                        }
                    }
                    break;

                case LinqMethods.Any:
                    isSubqueryExpression = true;
                    expressionObjKind = SubqueryKind.ExistsScalarExpression;
                    break;

                case LinqMethods.Select:
                case LinqMethods.SelectMany:
                case LinqMethods.Where:
                case LinqMethods.OrderBy:
                case LinqMethods.OrderByDescending:
                case LinqMethods.ThenBy:
                case LinqMethods.ThenByDescending:
                case LinqMethods.Skip:
                case LinqMethods.Take:
                case LinqMethods.Distinct:
                    isSubqueryExpression = true;
                    expressionObjKind = SubqueryKind.ArrayScalarExpression;
                    break;

                default:
                    isSubqueryExpression = false;
                    expressionObjKind = null;
                    break;
            }

            return isSubqueryExpression;
        }

        /// <summary>
        /// Visit an lambda expression which is in side a lambda and translate it to a scalar expression or a subquery scalar expression.
        /// See the other overload of this method for more details.
        /// </summary>
        /// <param name="lambda">The input lambda expression</param>
        /// <param name="context">The translation context</param>
        /// <returns>A scalar expression representing the input expression</returns>
        private static SqlScalarExpression VisitScalarExpression(LambdaExpression lambda, TranslationContext context)
        {
            return ExpressionToSql.VisitScalarExpression(
                lambda.Body,
                lambda.Parameters,
                context);
        }

        /// <summary>
        /// Visit an lambda expression which is in side a lambda and translate it to a scalar expression or a collection scalar expression.
        /// If it is a collection scalar expression, e.g. should be translated to subquery such as SELECT VALUE ARRAY, SELECT VALUE EXISTS, 
        /// SELECT VALUE [aggregate], the subquery will be aliased to a new binding for the FROM clause. E.g. consider 
        /// Select(family => family.Children.Select(child => child.Grade)). Since the inner Select corresponds to a subquery, this method would 
        /// create a new binding of v0 to the subquery SELECT VALUE ARRAY(), and the inner expression will be just SELECT v0.
        /// </summary>
        /// <param name="expression">The input expression</param>
        /// <param name="context">The translation context</param>
        /// <returns>A scalar expression representing the input expression</returns>
        internal static SqlScalarExpression VisitScalarExpression(Expression expression, TranslationContext context)
        {
            return ExpressionToSql.VisitScalarExpression(
                expression,
                new ReadOnlyCollection<ParameterExpression>(new ParameterExpression[] { }),
                context);
        }

        internal static bool TryGetSqlNumberLiteral(object value, out SqlNumberLiteral sqlNumberLiteral)
        {
            sqlNumberLiteral = default(SqlNumberLiteral);
            if (value is byte byteValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(byteValue);
            }
            else if (value is sbyte sbyteValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(sbyteValue);
            }
            else if (value is decimal decimalValue)
            {
                if ((decimalValue >= long.MinValue) && (decimalValue <= long.MaxValue) && (decimalValue % 1 == 0))
                {
                    sqlNumberLiteral = SqlNumberLiteral.Create(Convert.ToInt64(decimalValue));
                }
                else
                {
                    sqlNumberLiteral = SqlNumberLiteral.Create(Convert.ToDouble(decimalValue));
                }
            }
            else if (value is double doubleValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(doubleValue);
            }
            else if (value is float floatVlaue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(floatVlaue);
            }
            else if (value is int intValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(intValue);
            }
            else if (value is uint uintValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(uintValue);
            }
            else if (value is long longValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(longValue);
            }
            else if (value is ulong ulongValue)
            {
                if (ulongValue <= long.MaxValue)
                {
                    sqlNumberLiteral = SqlNumberLiteral.Create(Convert.ToInt64(ulongValue));
                }
                else
                {
                    sqlNumberLiteral = SqlNumberLiteral.Create(Convert.ToDouble(ulongValue));
                }
            }
            else if (value is short shortValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(shortValue);
            }
            else if (value is ushort ushortValue)
            {
                sqlNumberLiteral = SqlNumberLiteral.Create(ushortValue);
            }

            return sqlNumberLiteral != default(SqlNumberLiteral);
        }

        /// <summary>
        /// Visit an lambda expression which is in side a lambda and translate it to a scalar expression or a collection scalar expression.
        /// See the other overload of this method for more details.
        /// </summary>
        private static SqlScalarExpression VisitScalarExpression(Expression expression,
            ReadOnlyCollection<ParameterExpression> parameters,
            TranslationContext context)
        {
            SubqueryKind? expressionObjKind;
            bool isMinMaxAvgMethod;
            bool shouldUseSubquery = ExpressionToSql.IsSubqueryScalarExpression(expression, out expressionObjKind, out isMinMaxAvgMethod);

            SqlScalarExpression sqlScalarExpression;
            if (!shouldUseSubquery)
            {
                sqlScalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(expression, parameters, context);
            }
            else
            {
                SqlQuery query = ExpressionToSql.CreateSubquery(expression, parameters, context);

                ParameterExpression parameterExpression = context.GenFreshParameter(typeof(object), ExpressionToSql.DefaultParameterName);
                SqlCollection subqueryCollection = ExpressionToSql.CreateSubquerySqlCollection(
                    query, context,
                    isMinMaxAvgMethod ? SubqueryKind.ArrayScalarExpression : expressionObjKind.Value);

                Binding newBinding = new Binding(parameterExpression, subqueryCollection,
                    isInCollection: false, isInputParameter: context.IsInMainBranchSelect());

                context.CurrentSubqueryBinding.NewBindings.Add(newBinding);

                if (isMinMaxAvgMethod)
                {
                    sqlScalarExpression = SqlMemberIndexerScalarExpression.Create(
                        SqlPropertyRefScalarExpression.Create(null, SqlIdentifier.Create(parameterExpression.Name)),
                        SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0)));
                }
                else
                {
                    sqlScalarExpression = SqlPropertyRefScalarExpression.Create(null, SqlIdentifier.Create(parameterExpression.Name));
                }
            }

            return sqlScalarExpression;
        }

        /// <summary>
        /// Create a subquery SQL collection object for a SQL query
        /// </summary>
        /// <param name="query">The SQL query object</param>
        /// <param name="context">The translation context</param>
        /// <param name="subqueryType">The subquery type</param>
        private static SqlCollection CreateSubquerySqlCollection(SqlQuery query, TranslationContext context, SubqueryKind subqueryType)
        {
            SqlCollection subqueryCollection;
            switch (subqueryType)
            {
                case SubqueryKind.ArrayScalarExpression:
                    SqlArrayScalarExpression arrayScalarExpression = SqlArrayScalarExpression.Create(query);
                    query = SqlQuery.Create(
                        SqlSelectClause.Create(SqlSelectValueSpec.Create(arrayScalarExpression)),
                        fromClause: null, whereClause: null, groupByClause: null, orderByClause: null, offsetLimitClause: null);
                    break;

                case SubqueryKind.ExistsScalarExpression:
                    SqlExistsScalarExpression existsScalarExpression = SqlExistsScalarExpression.Create(query);
                    query = SqlQuery.Create(
                        SqlSelectClause.Create(SqlSelectValueSpec.Create(existsScalarExpression)),
                        fromClause: null, whereClause: null, groupByClause: null, orderByClause: null, offsetLimitClause: null);
                    break;

                case SubqueryKind.SubqueryScalarExpression:
                    // No need to wrap query as in ArrayScalarExpression, or ExistsScalarExpression
                    break;

                default:
                    throw new DocumentQueryException($"Unsupported subquery type {subqueryType}");
            }

            subqueryCollection = SqlSubqueryCollection.Create(query);
            return subqueryCollection;
        }

        /// <summary>
        /// Create a subquery from a subquery scalar expression.
        /// By visiting the collection expression, this builds a new QueryUnderConstruction on top of the current one
        /// and then translate it to a SQL query while keeping the current QueryUnderConstruction in tact.
        /// </summary>
        /// <param name="expression">The subquery scalar expression</param>
        /// <param name="parameters">The list of parameters of the expression</param>
        /// <param name="context">The translation context</param>
        /// <returns>A query corresponding to the collection expression</returns>
        /// <remarks>The QueryUnderConstruction remains unchanged after this.</remarks>
        private static SqlQuery CreateSubquery(Expression expression, ReadOnlyCollection<ParameterExpression> parameters, TranslationContext context)
        {
            bool shouldBeOnNewQuery = context.CurrentSubqueryBinding.ShouldBeOnNewQuery;

            QueryUnderConstruction queryBeforeVisit = context.currentQuery;
            QueryUnderConstruction packagedQuery = new QueryUnderConstruction(context.GetGenFreshParameterFunc(), context.currentQuery);
            packagedQuery.fromParameters.SetInputParameter(typeof(object), context.currentQuery.GetInputParameterInContext(shouldBeOnNewQuery).Name, context.InScope);
            context.currentQuery = packagedQuery;

            if (shouldBeOnNewQuery) context.CurrentSubqueryBinding.ShouldBeOnNewQuery = false;

            Collection collection = ExpressionToSql.VisitCollectionExpression(expression, parameters, context);

            QueryUnderConstruction subquery = context.currentQuery.GetSubquery(queryBeforeVisit);
            context.CurrentSubqueryBinding.ShouldBeOnNewQuery = shouldBeOnNewQuery;
            context.currentQuery = queryBeforeVisit;

            SqlQuery sqlSubquery = subquery.FlattenAsPossible().GetSqlQuery();
            return sqlSubquery;
        }

        #endregion Scalar and CollectionScalar Visitors

        #region LINQ Specific Visitors

        private static SqlWhereClause VisitWhere(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Where, 2, arguments.Count));
            }

            LambdaExpression function = Utilities.GetLambda(arguments[1]);
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarExpression(function, context);
            SqlWhereClause where = SqlWhereClause.Create(sqlfunc);
            return where;
        }

        private static SqlSelectClause VisitSelect(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Select, 2, arguments.Count));
            }

            LambdaExpression lambda = Utilities.GetLambda(arguments[1]);

            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarExpression(lambda, context);
            SqlSelectSpec sqlSpec = SqlSelectValueSpec.Create(sqlfunc);
            SqlSelectClause select = SqlSelectClause.Create(sqlSpec, null);
            return select;
        }

        private static Collection VisitSelectMany(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.SelectMany, 2, arguments.Count));
            }

            LambdaExpression lambda = Utilities.GetLambda(arguments[1]);

            // If there is Distinct, Take or OrderBy the lambda then it needs to be in a subquery.
            bool requireLocalExecution = false;

            for (MethodCallExpression methodCall = lambda.Body as MethodCallExpression;
                methodCall != null;
                methodCall = methodCall.Arguments[0] as MethodCallExpression)
            {
                string methodName = methodCall.Method.Name;
                requireLocalExecution |= methodName.Equals(LinqMethods.Distinct) || methodName.Equals(LinqMethods.Take) || methodName.Equals(LinqMethods.OrderBy) || methodName.Equals(LinqMethods.OrderByDescending);
            }

            Collection collection;
            if (!requireLocalExecution)
            {
                collection = ExpressionToSql.VisitCollectionLambda(lambda, context);
            }
            else
            {
                collection = new Collection(string.Empty);
                Binding binding;
                SqlQuery query = ExpressionToSql.CreateSubquery(lambda.Body, lambda.Parameters, context);
                SqlCollection subqueryCollection = SqlSubqueryCollection.Create(query);
                ParameterExpression parameterExpression = context.GenFreshParameter(typeof(object), ExpressionToSql.DefaultParameterName);
                binding = new Binding(parameterExpression, subqueryCollection, isInCollection: false, isInputParameter: true);
                context.currentQuery.fromParameters.Add(binding);
            }

            return collection;
        }

        private static SqlOrderByClause VisitOrderBy(ReadOnlyCollection<Expression> arguments, bool isDescending, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.OrderBy, 2, arguments.Count));
            }

            LambdaExpression lambda = Utilities.GetLambda(arguments[1]);
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitScalarExpression(lambda, context);
            SqlOrderByItem orderByItem = SqlOrderByItem.Create(sqlfunc, isDescending);
            SqlOrderByClause orderby = SqlOrderByClause.Create(new SqlOrderByItem[] { orderByItem });
            return orderby;
        }

        private static bool TryGetTopSkipTakeLiteral(
            SqlScalarExpression scalarExpression,
            TranslationContext context,
            out SqlNumberLiteral literal)
        {
            literal = default(SqlNumberLiteral);

            if (scalarExpression is SqlLiteralScalarExpression literalScalarExpression)
            {
                if (literalScalarExpression.Literal is SqlNumberLiteral numberLiteral)
                {
                    // After a member access in SelectMany's lambda, if there is only Top/Skip/Take then
                    // it is necessary to trigger the binding because Skip is just a spec with no binding on its own. 
                    // This can be done by pushing and popping a temporary parameter. E.g. In SelectMany(f => f.Children.Skip(1)), 
                    // it's necessary to consider Skip as Skip(x => x, 1) to bind x to f.Children. Similarly for Top and Limit.
                    ParameterExpression parameter = context.GenFreshParameter(typeof(object), ExpressionToSql.DefaultParameterName);
                    context.PushParameter(parameter, context.CurrentSubqueryBinding.ShouldBeOnNewQuery);
                    context.PopParameter();

                    literal = numberLiteral;
                }
            }

            return (literal != default(SqlNumberLiteral)) && (literal.Value >= 0);
        }

        private static bool TryGetTopSkipTakeParameter(
            SqlScalarExpression scalarExpression,
            TranslationContext context,
            out SqlParameter sqlParameter)
        {
            sqlParameter = default(SqlParameter);
            SqlParameterRefScalarExpression parameterRefScalarExpression = scalarExpression as SqlParameterRefScalarExpression;
            if (parameterRefScalarExpression != null)
            {
                sqlParameter = parameterRefScalarExpression.Parameter;
            }

            return (sqlParameter != default(SqlParameter)) && !string.IsNullOrEmpty(sqlParameter.Name);
        }

        private static SqlOffsetSpec VisitSkip(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Skip, 2, arguments.Count));
            }

            Expression expression = arguments[1];
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            SqlScalarExpression scalarExpression = ExpressionToSql.VisitScalarExpression(expression, context);
            SqlNumberLiteral offsetNumberLiteral;
            SqlParameter sqlParameter;
            SqlOffsetSpec offsetSpec;

            // skipExpression must be number literal
            if (TryGetTopSkipTakeLiteral(scalarExpression, context, out offsetNumberLiteral))
            {
                offsetSpec = SqlOffsetSpec.Create(offsetNumberLiteral);
            }
            else if (TryGetTopSkipTakeParameter(scalarExpression, context, out sqlParameter))
            {
                offsetSpec = SqlOffsetSpec.Create(sqlParameter);
            }
            else
            {
                // .Skip() has only one overload that takes int
                // so we really should always get a number (integer) literal here
                // the below throw serves as assert
                throw new ArgumentException(ClientResources.InvalidSkipValue);
            }

            return offsetSpec;
        }

        private static SqlLimitSpec VisitTakeLimit(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Take, 2, arguments.Count));
            }

            Expression expression = arguments[1];
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            SqlScalarExpression scalarExpression = ExpressionToSql.VisitScalarExpression(expression, context);
            SqlNumberLiteral takeNumberLiteral;
            SqlParameter sqlParameter;
            SqlLimitSpec limitSpec;

            // takeExpression must be number literal
            if (TryGetTopSkipTakeLiteral(scalarExpression, context, out takeNumberLiteral))
            {
                limitSpec = SqlLimitSpec.Create(takeNumberLiteral);
            }
            else if (TryGetTopSkipTakeParameter(scalarExpression, context, out sqlParameter))
            {
                limitSpec = SqlLimitSpec.Create(sqlParameter);
            }
            else
            {
                // .Take() has only one overload that takes int
                // so we really should always get a number (integer) literal here
                // the below throw serves as assert
                throw new ArgumentException(ClientResources.InvalidTakeValue);
            }

            return limitSpec;
        }

        private static SqlTopSpec VisitTakeTop(ReadOnlyCollection<Expression> arguments, TranslationContext context)
        {
            if (arguments.Count != 2)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Take, 2, arguments.Count));
            }

            Expression expression = arguments[1];
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            SqlScalarExpression scalarExpression = ExpressionToSql.VisitScalarExpression(expression, context);
            SqlNumberLiteral takeNumberLiteral;
            SqlParameter sqlParameter;
            SqlTopSpec topSpec;

            // takeExpression must be number literal
            if (TryGetTopSkipTakeLiteral(scalarExpression, context, out takeNumberLiteral))
            {
                topSpec = SqlTopSpec.Create(takeNumberLiteral);
            }
            else if (TryGetTopSkipTakeParameter(scalarExpression, context, out sqlParameter))
            {
                topSpec = SqlTopSpec.Create(sqlParameter);
            }
            else
            {
                // .Take() has only one overload that takes int
                // so we really should always get a number (integer) literal here
                // the below throw serves as assert
                throw new ArgumentException(ClientResources.InvalidTakeValue);
            }

            return topSpec;
        }

        private static SqlSelectClause VisitAggregateFunction(
            ReadOnlyCollection<Expression> arguments,
            TranslationContext context,
            string aggregateFunctionName)
        {
            SqlScalarExpression aggregateExpression;
            if (arguments.Count == 1)
            {
                // Need to trigger parameter binding for cases where a aggregate function immediately follows a member access.
                ParameterExpression parameter = context.GenFreshParameter(typeof(object), ExpressionToSql.DefaultParameterName);
                context.PushParameter(parameter, context.CurrentSubqueryBinding.ShouldBeOnNewQuery);
                aggregateExpression = ExpressionToSql.VisitParameter(parameter, context);
                context.PopParameter();
            }
            else if (arguments.Count == 2)
            {
                LambdaExpression lambda = Utilities.GetLambda(arguments[1]);
                aggregateExpression = ExpressionToSql.VisitScalarExpression(lambda, context);
            }
            else
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, aggregateFunctionName, 2, arguments.Count));
            }

            SqlFunctionCallScalarExpression aggregateFunctionCall;
            aggregateFunctionCall = SqlFunctionCallScalarExpression.CreateBuiltin(aggregateFunctionName, aggregateExpression);

            SqlSelectSpec selectSpec = SqlSelectValueSpec.Create(aggregateFunctionCall);
            SqlSelectClause selectClause = SqlSelectClause.Create(selectSpec, null);
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

            // We consider Distinct as Distinct(v0 => v0)
            // It's necessary to visit this identity method to replace the parameters names
            ParameterExpression parameter = context.GenFreshParameter(typeof(object), ExpressionToSql.DefaultParameterName);
            LambdaExpression identityLambda = Expression.Lambda(parameter, parameter);
            SqlScalarExpression sqlfunc = ExpressionToSql.VisitNonSubqueryScalarLambda(identityLambda, context);
            SqlSelectSpec sqlSpec = SqlSelectValueSpec.Create(sqlfunc);
            SqlSelectClause select = SqlSelectClause.Create(sqlSpec, topSpec: null, hasDistinct: true);
            return select;
        }

        private static SqlSelectClause VisitCount(
            ReadOnlyCollection<Expression> arguments,
            TranslationContext context)
        {
            SqlScalarExpression countExpression;
            countExpression = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create((Number64)1));

            if (arguments.Count == 2)
            {
                SqlWhereClause whereClause = ExpressionToSql.VisitWhere(arguments, context);
                context.currentQuery = context.currentQuery.AddWhereClause(whereClause, context);
            }
            else if (arguments.Count != 1)
            {
                throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.InvalidArgumentsCount, LinqMethods.Count, 2, arguments.Count));
            }

            SqlSelectSpec selectSpec = SqlSelectValueSpec.Create(SqlFunctionCallScalarExpression.CreateBuiltin(SqlFunctionCallScalarExpression.Names.Count, countExpression));
            SqlSelectClause selectClause = SqlSelectClause.Create(selectSpec, null);
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
                identifiers.Add(propRef.Identifier);
                SqlScalarExpression parent = propRef.Member;
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
                path = SqlIdentifierPathExpression.Create(path, identifer);
            }

            SqlIdentifier last = identifiers[identifiers.Count - 1];
            SqlInputPathCollection result = SqlInputPathCollection.Create(last, path);
            return result;
        }

        private static SqlInputPathCollection ConvertMemberIndexerToPath(SqlMemberIndexerScalarExpression memberIndexerExpression)
        {
            // root.Children.Age ==> root["Children"]["Age"]
            List<SqlStringLiteral> literals = new List<SqlStringLiteral>();
            while (true)
            {
                literals.Add((SqlStringLiteral)((SqlLiteralScalarExpression)memberIndexerExpression.Indexer).Literal);
                SqlScalarExpression parent = memberIndexerExpression.Member;
                if (parent == null)
                {
                    break;
                }

                if (parent is SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
                {
                    literals.Add(SqlStringLiteral.Create(sqlPropertyRefScalarExpression.Identifier.Value));
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
                path = SqlStringPathExpression.Create(path, literals[i]);
            }

            SqlInputPathCollection result = SqlInputPathCollection.Create(SqlIdentifier.Create(literals[literals.Count - 1].Value), path);
            return result;
        }

        #endregion LINQ Specific Visitors

        private sealed class CosmosElementToSqlScalarExpressionVisitor : ICosmosElementVisitor<SqlScalarExpression>
        {
            public static readonly CosmosElementToSqlScalarExpressionVisitor Singleton = new CosmosElementToSqlScalarExpressionVisitor();

            private CosmosElementToSqlScalarExpressionVisitor()
            {
                // Private constructor, since this class is a singleton.
            }

            public SqlScalarExpression Visit(CosmosArray cosmosArray)
            {
                List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                foreach (CosmosElement item in cosmosArray)
                {
                    items.Add(item.Accept(this));
                }

                return SqlArrayCreateScalarExpression.Create(items.ToImmutableArray());
            }

            public SqlScalarExpression Visit(CosmosBinary cosmosBinary)
            {
                // Can not convert binary to scalar expression without knowing the API type.
                throw new NotImplementedException();
            }

            public SqlScalarExpression Visit(CosmosBoolean cosmosBoolean)
            {
                return SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(cosmosBoolean.Value));
            }

            public SqlScalarExpression Visit(CosmosGuid cosmosGuid)
            {
                // Can not convert guid to scalar expression without knowing the API type.
                throw new NotImplementedException();
            }

            public SqlScalarExpression Visit(CosmosNull cosmosNull)
            {
                return SqlLiteralScalarExpression.Create(SqlNullLiteral.Create());
            }

            public SqlScalarExpression Visit(CosmosNumber cosmosNumber)
            {
                if (!(cosmosNumber is CosmosNumber64 cosmosNumber64))
                {
                    throw new ArgumentException($"Unknown {nameof(CosmosNumber)} type: {cosmosNumber.GetType()}.");
                }

                return SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(cosmosNumber64.GetValue()));
            }

            public SqlScalarExpression Visit(CosmosObject cosmosObject)
            {
                List<SqlObjectProperty> properties = new List<SqlObjectProperty>();
                foreach (KeyValuePair<string, CosmosElement> prop in cosmosObject)
                {
                    SqlPropertyName name = SqlPropertyName.Create(prop.Key);
                    CosmosElement value = prop.Value;
                    SqlScalarExpression expression = value.Accept(this);
                    SqlObjectProperty property = SqlObjectProperty.Create(name, expression);
                    properties.Add(property);
                }

                return SqlObjectCreateScalarExpression.Create(properties.ToImmutableArray());
            }

            public SqlScalarExpression Visit(CosmosString cosmosString)
            {
                return SqlLiteralScalarExpression.Create(SqlStringLiteral.Create(cosmosString.Value));
            }
        }
        private enum SubqueryKind
        {
            ArrayScalarExpression,
            ExistsScalarExpression,
            SubqueryScalarExpression,
        }
    }
}
