//-----------------------------------------------------------------------
// <copyright file="JTokenToSqlScalarExpression.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Class used to Convert between a JToken to its semantically equivalent SqlScalarExpression.
    /// </summary>
    internal sealed class JTokenToSqlScalarExpression
    {
        private static readonly SqlScalarExpression Undefined = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());

        /// <summary>
        /// Converts a JToken to a semantically equivalent SqlScalarExpression.
        /// </summary>
        /// <param name="token">The JToken to convert.</param>
        /// <returns>The semantically equivalent SqlScalarExpression.</returns>
        public static SqlScalarExpression Convert(JToken token)
        {
            if (token == null)
            {
                return Undefined;
            }

            switch (token.Type)
            {
                case JTokenType.Array:
                    {
                        List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                        foreach (JToken element in token)
                        {
                            items.Add(JTokenToSqlScalarExpression.Convert(element));
                        }

                        return SqlArrayCreateScalarExpression.Create(items.ToArray());
                    }

                case JTokenType.Boolean:
                    {
                        SqlBooleanLiteral literal = SqlBooleanLiteral.Create(token.ToObject<bool>());
                        return SqlLiteralScalarExpression.Create(literal);
                    }

                case JTokenType.Null:
                    {
                        SqlNullLiteral literal = SqlNullLiteral.Singleton;
                        return SqlLiteralScalarExpression.Create(literal);
                    }

                case JTokenType.Integer:
                case JTokenType.Float:
                    {
                        SqlNumberLiteral literal = SqlNumberLiteral.Create(token.ToObject<double>());
                        return SqlLiteralScalarExpression.Create(literal);
                    }

                case JTokenType.Object:
                    {
                        List<SqlObjectProperty> properties = new List<SqlObjectProperty>();

                        foreach (JProperty prop in (JToken)token)
                        {
                            SqlPropertyName name = SqlPropertyName.Create(prop.Name);
                            JToken value = prop.Value;
                            SqlScalarExpression expression = JTokenToSqlScalarExpression.Convert(value);
                            SqlObjectProperty property = SqlObjectProperty.Create(name, expression);
                            properties.Add(property);
                        }

                        return SqlObjectCreateScalarExpression.Create(properties.ToArray());
                    }

                case JTokenType.String:
                    {
                        SqlStringLiteral literal = SqlStringLiteral.Create(token.ToObject<string>());
                        return SqlLiteralScalarExpression.Create(literal);
                    }

                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported JsonType {0}", token.Type));
            }
        }
    }
}