﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq.Expressions;
    using System.Reflection;

    internal class CustomCosmosLinqSerializer : ICosmosLinqSerializerInternal
    {
        private readonly CosmosLinqSerializer CustomCosmosSerializer;

        public CustomCosmosLinqSerializer(CosmosLinqSerializer customCosmosLinqSerializer)
        {
            this.CustomCosmosSerializer = customCosmosLinqSerializer;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            return true;
        }

        public string Serialize(object value, MemberExpression memberExpression, Type memberType)
        {
            return this.SerializeWithCustomSerializer(value);
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            return this.SerializeWithCustomSerializer(inputExpression.Value);
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            return this.CustomCosmosSerializer.SerializeMemberName(memberInfo);
        }

        private string SerializeWithCustomSerializer(object value)
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);

            using (Stream stream = this.CustomCosmosSerializer.ToStream(value))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    string propertyValue = streamReader.ReadToEnd();
                    writer.Write(propertyValue);
                    return writer.ToString();
                }
            }
        }
    }
}
