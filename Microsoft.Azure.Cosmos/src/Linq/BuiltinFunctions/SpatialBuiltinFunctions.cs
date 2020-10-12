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
            SpatialBuiltinFunctionDefinitions = new Dictionary<string, BuiltinFunctionVisitor>();

            SpatialBuiltinFunctionDefinitions.Add("Distance",
                new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StDistance,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(Geometry)},
                    }));

            SpatialBuiltinFunctionDefinitions.Add("Within",
                new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StWithin,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(Geometry)},
                    }));

            SpatialBuiltinFunctionDefinitions.Add("IsValidDetailed",
                new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StIsvaliddetailed,
                    true,
                    new List<Type[]>()));

            SpatialBuiltinFunctionDefinitions.Add("IsValid",
                new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StIsvalid,
                    true,
                    new List<Type[]>()));

            SpatialBuiltinFunctionDefinitions.Add("Intersects",
                new SqlBuiltinFunctionVisitor(SqlFunctionCallScalarExpression.Names.StIntersects,
                    true,
                    new List<Type[]>()
                    {
                        new Type[]{typeof(Geometry)},
                    }));
        }

        public static SqlScalarExpression Visit(MethodCallExpression methodCallExpression, TranslationContext context)
        {
            BuiltinFunctionVisitor visitor = null;
            if (SpatialBuiltinFunctionDefinitions.TryGetValue(methodCallExpression.Method.Name, out visitor))
            {
                return visitor.Visit(methodCallExpression, context);
            }

            throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.MethodNotSupported, methodCallExpression.Method.Name));
        }
    }
}
