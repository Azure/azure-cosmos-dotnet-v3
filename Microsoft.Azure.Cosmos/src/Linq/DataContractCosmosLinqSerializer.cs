//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;

    internal class DataContractCosmosLinqSerializer : ICosmosLinqSerializer
    {
        private readonly CosmosSerializer CosmosSerializer;

        private readonly CosmosPropertyNamingPolicy PropertyNamingPolicy;

        public DataContractCosmosLinqSerializer(CosmosPropertyNamingPolicy propertyNamingPolicy)
        {
            this.CosmosSerializer = new CosmosJsonDotNetSerializer(new CosmosSerializationOptions()
            {
                PropertyNamingPolicy = propertyNamingPolicy
            });

            this.PropertyNamingPolicy = propertyNamingPolicy;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            return false;
        }

        public string Serialize(object value, MemberExpression memberExpression, Type memberType)
        {
            throw new InvalidOperationException($"{nameof(DefaultCosmosLinqSerializer)} Assert! Should not reach this function.");
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);

            using (Stream stream = this.CosmosSerializer.ToStream(inputExpression.Value))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    string propertyValue = streamReader.ReadToEnd();
                    writer.Write(propertyValue);
                    return writer.ToString();
                }
            }
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            string memberName = memberInfo.Name;

            DataContractAttribute dataContractAttribute = memberInfo.DeclaringType.GetCustomAttribute<DataContractAttribute>(true);
            if (dataContractAttribute != null)
            {
                DataMemberAttribute dataMemberAttribute = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
                if (dataMemberAttribute != null && !string.IsNullOrEmpty(dataMemberAttribute.Name))
                {
                    memberName = dataMemberAttribute.Name;
                }
            }

            memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(this.PropertyNamingPolicy, memberName);

            return memberName;
        }
    }
}
