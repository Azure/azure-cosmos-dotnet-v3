//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal sealed class CosmosElementToSqlScalarExpressionVisitor : ICosmosElementVisitor<SqlScalarExpression>
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
            Debug.Fail("CosmosElementToSqlScalarExpressionVisitor Assert", "Unreachable");
            throw new InvalidOperationException();
        }

        public SqlScalarExpression Visit(CosmosBoolean cosmosBoolean)
        {
            return SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(cosmosBoolean.Value));
        }

        public SqlScalarExpression Visit(CosmosGuid cosmosGuid)
        {
            // Can not convert guid to scalar expression without knowing the API type.
            Debug.Fail("CosmosElementToSqlScalarExpressionVisitor Assert", "Unreachable");
            throw new InvalidOperationException();
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

        public SqlScalarExpression Visit(CosmosUndefined cosmosUndefined)
        {
            return SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());
        }
    }
}
