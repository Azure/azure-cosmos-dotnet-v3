//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal interface ICosmosLinqSerializer
    {
        /// <summary>
        /// Gets custom attributes on a member expression. Returns null if none exist.
        /// </summary>
        CustomAttributeData GetConverterAttribute(MemberExpression memberExpression, Type memberType);

        /// <summary>
        /// Applies specified custom converter to an object.
        /// </summary>
        string SerializeWithConverter(object value, Type converterType);

        /// <summary>
        /// Serializes a ConstantExpression as a SqlScalarExpression.
        /// </summary>
        SqlScalarExpression ConvertToSqlScalarExpression(ConstantExpression inputExpression, IDictionary<object, string> parameters);

        /// <summary>
        /// Gets a member name with LINQ serializer options applied.
        /// </summary>
        string GetMemberName(MemberInfo memberInfo, CosmosLinqSerializerOptions linqSerializerOptions = null);
    }
}