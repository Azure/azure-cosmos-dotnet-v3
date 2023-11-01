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
        SqlScalarExpression ApplyCustomConverters(Expression left, SqlLiteralScalarExpression right);

        string GetMemberName(MemberInfo memberInfo, CosmosLinqSerializerOptions linqSerializerOptions = null);

        SqlScalarExpression VisitConstant(ConstantExpression inputExpression, IDictionary<object, string> parameters);
    }
}