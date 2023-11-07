//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    internal interface ICosmosLinqSerializer
    {
        /// <summary>
        /// Returns true if there are custom attributes on a member expression.
        /// </summary>
        bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType);

        // TODO : Clean up this interface member for better generalizability
        /// <summary>
        /// Serializes object.
        /// </summary>
        string Serialize(object value, MemberExpression memberExpression, Type memberType);

        /// <summary>
        /// Serializes a ConstantExpression.
        /// </summary>
        string SerializeScalarExpression(ConstantExpression inputExpression);

        /// <summary>
        /// Gets a member name with LINQ serializer options applied.
        /// </summary>
        string GetMemberName(MemberInfo memberInfo, CosmosLinqSerializerOptions linqSerializerOptions = null);
    }
}
