//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class SpatialBuiltinFunctions
    {
        private static Dictionary<string, BuiltinFunctionVisitor> SpatialBuiltinFunctionDefinitions { get; set; }

        static SpatialBuiltinFunctions()
        {
            SpatialBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>
            {
                {
                    "Distance",
                    new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StDistance,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(Geometry)},
                    })
                },

                {
                    "Within",
                    new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StWithin,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(Geometry)},
                    })
                },

                {
                    "IsValidDetailed",
                    new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StIsvaliddetailed,
                    true,
                    new List<Type[]>())
                },

                {
                    "IsValid",
                    new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StIsvalid,
                    true,
                    new List<Type[]>())
                },

                {
                    "Intersects",
                    new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StIntersects,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(Geometry)},
                    })
                }
            };
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            if (SpatialBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out BuiltinFunctionVisitor visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
