//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlObjectCreateScalarExpression : SqlScalarExpression
    {
        private SqlObjectCreateScalarExpression(ImmutableArray<SqlObjectProperty> properties, CosmosSerializer userSerializer = null)
        {
            if (properties == null)
            {
                throw new ArgumentNullException($"{nameof(properties)} must not be null.");
            }

            foreach (SqlObjectProperty property in properties)
            {
                if (property == null)
                {
                    throw new ArgumentException($"{nameof(properties)} must not have null items.");
                }
            }

            this.Properties = properties;
            this.UserSerializer = userSerializer; //nullable
        }

        public ImmutableArray<SqlObjectProperty> Properties { get; }

        public CosmosSerializer UserSerializer { get; }

        public static SqlObjectCreateScalarExpression Create(CosmosSerializer userSerializer, params SqlObjectProperty[] properties) => new SqlObjectCreateScalarExpression(properties.ToImmutableArray(), userSerializer);

        public static SqlObjectCreateScalarExpression Create(CosmosSerializer userSerializer, ImmutableArray<SqlObjectProperty> properties) => new SqlObjectCreateScalarExpression(properties, userSerializer);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override string ToString()
        {
            if (this.UserSerializer != null)
            {
                StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);

                // Use the user serializer for the parameter values so custom conversions are correctly handled
                using (Stream str = this.UserSerializer.ToStream(this.Properties))
                {
                    using (StreamReader streamReader = new StreamReader(str))
                    {
                        string propertyValue = streamReader.ReadToEnd();
                        writer.Write(propertyValue);
                        return writer.ToString();
                    }
                }
            }

            return base.ToString();
        }
    }
}
