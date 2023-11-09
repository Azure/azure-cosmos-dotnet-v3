//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    internal class DataContractCosmosLinqSerializer : ICosmosLinqSerializer
    {
        private readonly CosmosPropertyNamingPolicy PropertyNamingPolicy;

        public DataContractCosmosLinqSerializer(CosmosPropertyNamingPolicy propertyNamingPolicy)
        {
            this.PropertyNamingPolicy = propertyNamingPolicy;
        }

        public bool RequiresCustomSerialization(MemberExpression memberExpression, Type memberType)
        {
            return false;
        }

        public string Serialize(object value, MemberExpression memberExpression, Type memberType)
        {
            throw new InvalidOperationException($"{nameof(DefaultCosmosLinqSerializer)} Assert! - should not reach this function.");
        }

        public string SerializeScalarExpression(ConstantExpression inputExpression)
        {
            if (this.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase)
            {
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                return JsonConvert.SerializeObject(inputExpression.Value, serializerSettings);
            }

            return JsonConvert.SerializeObject(inputExpression.Value);
        }

        public string SerializeMemberName(MemberInfo memberInfo)
        {
            string memberName = null;

            // Check if Newtonsoft JsonExtensionDataAttribute is present on the member, if so, return empty member name.
            Newtonsoft.Json.JsonExtensionDataAttribute jsonExtensionDataAttribute = memberInfo.GetCustomAttribute<Newtonsoft.Json.JsonExtensionDataAttribute>(true);
            if (jsonExtensionDataAttribute != null && jsonExtensionDataAttribute.ReadData)
            {
                return null;
            }

            DataContractAttribute dataContractAttribute = memberInfo.DeclaringType.GetCustomAttribute<DataContractAttribute>(true);
            if (dataContractAttribute != null)
            {
                DataMemberAttribute dataMemberAttribute = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
                if (dataMemberAttribute != null && !string.IsNullOrEmpty(dataMemberAttribute.Name))
                {
                    memberName = dataMemberAttribute.Name;
                }
            }

            memberName ??= memberInfo.Name;

            memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(this.PropertyNamingPolicy, memberName);

            return memberName;
        }
    }
}
