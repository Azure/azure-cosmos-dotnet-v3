//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Documents;

    internal abstract class BuiltinFunctionVisitor
    {
        public SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            SqlScalarExpression result = this.VisitExplicit(methodCallExpression, context);
            if (result != null)
            {
                return result;
            }

            result = this.VisitImplicit(methodCallExpression, context);
            if (result != null)
            {
                return result;
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }

        public static SqlScalarExpression VisitBuiltinFunctionCall(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            Type declaringType;

            // Method could be an extension method
            if (methodCallExpression.Method.IsStatic && methodCallExpression.Method.IsExtensionMethod())
            {
                if (methodCallExpression.Arguments.Count < 1)
                {
                    // Extension methods should has at least 1 argument, this should never happen
                    // Throwing ArgumentException instead of assert
                    throw new ArgumentException();
                }

                declaringType = methodCallExpression.Arguments[0].Type;

                if (methodCallExpression.Method.DeclaringType.GeUnderlyingSystemType() == typeof(CosmosLinqExtensions))
                {
                    // CosmosLinq Extensions can be RegexMatch, DocumentId or Type check functions (IsString, IsBool, etc.)
                    if (methodCallExpression.Method.Name == nameof(CosmosLinqExtensions.RegexMatch))
                    {
                        return StringBuiltinFunctions.Visit(methodCallExpression, context);
                    }

                    if (methodCallExpression.Method.Name == nameof(CosmosLinqExtensions.DocumentId))
                    {
                        return OtherBuiltinSystemFunctions.Visit(methodCallExpression, context);
                    }

                    return TypeCheckFunctions.Visit(methodCallExpression, context);
                }
            }
            else
            {
                declaringType = methodCallExpression.Method.DeclaringType;
            }

            // Check order matters, some extension methods work for both strings and arrays

            // Math functions
            if (declaringType == typeof(Math))
            {
                return MathBuiltinFunctions.Visit(methodCallExpression, context);
            }

            // ToString with String and Guid only becomes passthrough
            if (methodCallExpression.Method.Name == "ToString" &&
                 methodCallExpression.Arguments.Count == 0 &&
                 methodCallExpression.Object != null && 
                 ((methodCallExpression.Object.Type == typeof(string)) ||
                 (methodCallExpression.Object.Type == typeof(Guid))))
            {
                return ExpressionToSql.VisitNonSubqueryScalarExpression(methodCallExpression.Object, context);
            }

            // String functions or ToString with Objects that are not strings and guids
            if ((declaringType == typeof(string)) ||
                (methodCallExpression.Method.Name == "ToString" &&
                 methodCallExpression.Arguments.Count == 0 &&
                 methodCallExpression.Object != null))
            {
                return StringBuiltinFunctions.Visit(methodCallExpression, context);
            }

            // Array functions
            if (declaringType.IsEnumerable())
            {
                return ArrayBuiltinFunctions.Visit(methodCallExpression, context);
            }

            // Spatial functions
            if (typeof(Geometry).IsAssignableFrom(declaringType))
            {
                return SpatialBuiltinFunctions.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }

        protected abstract SqlScalarExpression VisitExplicit(MethodCallExpression methodCallExpression, TranslationContext context);

        protected abstract SqlScalarExpression VisitImplicit(MethodCallExpression methodCallExpression, TranslationContext context);
    }
}
