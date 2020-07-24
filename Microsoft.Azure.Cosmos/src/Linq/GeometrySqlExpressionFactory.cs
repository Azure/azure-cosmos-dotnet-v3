//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using global::Azure.Core.GeoJson;
    using Microsoft.Azure.Cosmos.Sql;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Constructs <see cref="SqlScalarExpression"/> from a geometry <see cref="Expression"/>.
    /// </summary>
    internal static class GeometrySqlExpressionFactory
    {
        /// <summary>
        /// Constructs <see cref="SqlScalarExpression"/> from a geometry <see cref="Expression"/>.
        /// </summary>
        /// <param name="geometryExpression">
        /// Expression of type <see cref="GeoObject"/>.
        /// </param>
        /// <returns>Instance of <see cref="SqlScalarExpression"/> representing geometry <paramref name="geometryExpression"/>.</returns>.
        public static SqlScalarExpression Construct(Expression geometryExpression)
        {
            if (!typeof(GeoObject).IsAssignableFrom(geometryExpression.Type))
            {
                throw new ArgumentException("geometryExpression");
            }

            if (geometryExpression.NodeType == ExpressionType.Constant)
            {
                // This is just optimization - if got constant, we don't need to compile expression etc.
                JObject jsonObject = JObject.FromObject(((ConstantExpression)geometryExpression).Value);
                return GeometrySqlExpressionFactory.FromJToken(jsonObject);
            }

            GeoObject geometry;

            try
            {
                Expression<Func<GeoObject>> le = Expression.Lambda<Func<GeoObject>>(geometryExpression);
                Func<GeoObject> compiledExpression = le.Compile();
                geometry = compiledExpression();
            }
            catch (Exception ex)
            {
                throw new DocumentQueryException(
                    string.Format(CultureInfo.CurrentCulture, ClientResources.FailedToEvaluateSpatialExpression), ex);
            }

            return GeometrySqlExpressionFactory.FromJToken(JObject.FromObject(geometry));
        }

        /// <summary>
        /// Constructs <see cref="SqlScalarExpression"/> from a geometry <see cref="JToken"/>.
        /// </summary>
        /// <param name="jToken">Json token.</param>
        /// <returns>Instance of <see cref="SqlScalarExpression"/>.</returns>
        private static SqlScalarExpression FromJToken(JToken jToken)
        {
            switch (jToken.Type)
            {
                case JTokenType.Array:
                    return SqlArrayCreateScalarExpression.Create(jToken.Select(FromJToken).ToArray());

                case JTokenType.Boolean:
                    return SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(jToken.Value<bool>()));

                case JTokenType.Null:
                    return SqlLiteralScalarExpression.SqlNullLiteralScalarExpression;

                case JTokenType.String:
                    return SqlLiteralScalarExpression.Create(SqlStringLiteral.Create(jToken.Value<string>()));

                case JTokenType.Object:

                    SqlObjectProperty[] properties =
                        ((JObject)jToken).Properties()
                            .Select(
                                p =>
                                SqlObjectProperty.Create(
                                    SqlPropertyName.Create(p.Name),
                                    FromJToken(p.Value)))
                            .ToArray();

                    return SqlObjectCreateScalarExpression.Create(properties);

                case JTokenType.Float:
                case JTokenType.Integer:
                    SqlNumberLiteral sqlNumberLiteral = SqlNumberLiteral.Create(jToken.Value<double>());
                    return SqlLiteralScalarExpression.Create(sqlNumberLiteral);

                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.UnexpectedTokenType, jToken.Type));
            }
        }
    }
}
