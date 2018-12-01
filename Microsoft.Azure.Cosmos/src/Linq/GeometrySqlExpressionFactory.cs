//------------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Sql;

    /// <summary>
    /// Constructs <see cref="SqlScalarExpression"/> from a geometry <see cref="Expression"/>.
    /// </summary>
    internal static class GeometrySqlExpressionFactory 
    {
        /// <summary>
        /// Constructs <see cref="SqlScalarExpression"/> from a geometry <see cref="Expression"/>.
        /// </summary>
        /// <param name="geometryExpression">
        /// Expression of type <see cref="Geometry"/>.
        /// </param>
        /// <returns>Instance of <see cref="SqlScalarExpression"/> representing geometry <paramref name="geometryExpression"/>.</returns>.
        public static SqlScalarExpression Construct(Expression geometryExpression)
        {
            if (!typeof(Geometry).IsAssignableFrom(geometryExpression.Type))
            {
                throw new ArgumentException("geometryExpression");
            }

            if (geometryExpression.NodeType == ExpressionType.Constant)
            {
                // This is just optimization - if got constant, we don't need to compile expression etc.
                JObject jsonObject = JObject.FromObject(((ConstantExpression)geometryExpression).Value);
                return GeometrySqlExpressionFactory.FromJToken(jsonObject);
            }

            Geometry geometry;

            try
            {
                var le = Expression.Lambda<Func<Geometry>>(geometryExpression);
                Func<Geometry> compiledExpression = le.Compile();
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
                    return new SqlArrayCreateScalarExpression(jToken.Select(FromJToken).ToArray());

                case JTokenType.Boolean:
                    SqlBooleanLiteral sqlBooleanLiteral = new SqlBooleanLiteral(jToken.Value<bool>());
                    return new SqlLiteralScalarExpression(sqlBooleanLiteral);

                case JTokenType.Null:
                    return new SqlLiteralScalarExpression(new SqlNullLiteral());

                case JTokenType.String:
                    SqlStringLiteral sqlStringLiteral = new SqlStringLiteral(jToken.Value<string>());
                    return new SqlLiteralScalarExpression(sqlStringLiteral);

                case JTokenType.Object:

                    var properties =
                        ((JObject)jToken).Properties()
                            .Select(
                                p =>
                                new SqlObjectProperty(
                                    new SqlPropertyName(p.Name),
                                    FromJToken(p.Value)))
                            .ToArray();

                    return new SqlObjectCreateScalarExpression(properties);

                case JTokenType.Float:
                case JTokenType.Integer:
                    SqlNumberLiteral sqlNumberLiteral = new SqlNumberLiteral(jToken.Value<double>());
                    return new SqlLiteralScalarExpression(sqlNumberLiteral);

                default:
                    throw new DocumentQueryException(string.Format(CultureInfo.CurrentCulture, ClientResources.UnexpectedTokenType, jToken.Type));
            }
        }
    }
}
