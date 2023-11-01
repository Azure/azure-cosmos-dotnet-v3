//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal interface ICosmosLinqSerializer
    {
        /// <summary>
        /// Applies specified custom converters to an expression.
        /// </summary>
        SqlScalarExpression ApplyCustomConverters(Expression left, SqlLiteralScalarExpression right);

        /// <summary>
        /// Serializes a ConstantExpression as a SqlScalarExpression.
        /// </summary>
        SqlScalarExpression ConvertToSqlScalarExpression(ConstantExpression inputExpression, IDictionary<object, string> parameters);

        /// <summary>
        /// Gets a member name with any LINQ serializer options applied.
        /// </summary>
        string GetMemberName(MemberInfo memberInfo, CosmosLinqSerializerOptions linqSerializerOptions = null);
    }
}