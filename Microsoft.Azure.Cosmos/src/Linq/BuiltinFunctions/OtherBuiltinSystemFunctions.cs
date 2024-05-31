//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class OtherBuiltinSystemFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> FunctionsDefinitions { get; set; }

        static OtherBuiltinSystemFunctions()
        {
            FunctionsDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                [nameof(CosmosLinqExtensions.DocumentId)] = new SqlBuiltinFunctionVisitor(
                    sqlName: "DOCUMENTID",
                    isStatic: true,
                    argumentLists: new List<Type[]>()
                    {
                        new Type[]{typeof(object)},
                    })
            };
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            return FunctionsDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor)
                ? visitor.Visit(methodCallExpression, context)
                : throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
