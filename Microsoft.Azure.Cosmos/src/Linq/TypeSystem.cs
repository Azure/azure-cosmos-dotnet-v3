//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal static class TypeSystem
    {
        public static Type GetElementType(Type type)
        {
            return GetElementType(type, new HashSet<Type>());
        }

        public static string GetMemberName(this MemberInfo memberInfo, CosmosSerializationOptions cosmosSerializationOptions = null)
        {
            string memberName = null;
            // Json.Net honors JsonPropertyAttribute more than DataMemberAttribute
            // So we check for JsonPropertyAttribute first.
            JsonPropertyAttribute jsonPropertyAttribute = memberInfo.GetCustomAttribute<JsonPropertyAttribute>(true);
            if (jsonPropertyAttribute != null && !string.IsNullOrEmpty(jsonPropertyAttribute.PropertyName))
            {
                memberName = jsonPropertyAttribute.PropertyName;
            }
            else
            {
                DataContractAttribute dataContractAttribute = memberInfo.DeclaringType.GetCustomAttribute<DataContractAttribute>(true);
                if (dataContractAttribute != null)
                {
                    DataMemberAttribute dataMemberAttribute = memberInfo.GetCustomAttribute<DataMemberAttribute>(true);
                    if (dataMemberAttribute != null && !string.IsNullOrEmpty(dataMemberAttribute.Name))
                    {
                        memberName = dataMemberAttribute.Name;
                    }
                }
            }

            if (memberName == null)
            {
                memberName = memberInfo.Name;
            }

            if (cosmosSerializationOptions != null)
            {
                memberName = CosmosSerializationUtil.GetStringWithPropertyNamingPolicy(cosmosSerializationOptions, memberName);
            }

            return memberName;
        }

        private static Type GetElementType(Type type, HashSet<Type> visitedSet)
        {
            Debug.Assert(type != null);
            Debug.Assert(visitedSet != null);

            // Check if we visited the type, to prevent exponential recursion in case multi-inheritance.
            if (visitedSet.Contains(type))
            {
                return null;
            }

            // Mark type as visited.
            visitedSet.Add(type);

            // Check if type is array.
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            // Attempt to find most specific element type among base class and all implemented interfaces.
            Type elementType = null;

            // Check if type is intance of generic IEnumerable<>
            if (type.IsInterface() && type.IsGenericType() && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = GetMoreSpecificType(elementType, type.GetGenericArguments()[0]);
            }

            foreach (Type interfaceType in type.GetInterfaces())
            {
                elementType = GetMoreSpecificType(elementType, GetElementType(interfaceType, visitedSet));
            }

            if (type.GetBaseType() != null && type.GetBaseType() != typeof(object))
            {
                elementType = GetMoreSpecificType(elementType, GetElementType(type.GetBaseType(), visitedSet));
            }

            return elementType;
        }

        private static Type GetMoreSpecificType(Type left, Type right)
        {
            if (left != null && right != null)
            {
                // Prefer left type over right type, in case both are assignable from each other or none.
                if (right.IsAssignableFrom(left))
                {
                    return left;
                }
                else if (left.IsAssignableFrom(right))
                {
                    return right;
                }
                else
                {
                    return left;
                }
            }
            else
            {
                return left ?? right;
            }
        }

        /// <summary>
        /// True if type is anonymous.
        /// </summary>
        /// <param name="type">Type to check.</param>
        /// <returns>Trye if the type is anonymous.</returns>
        public static Boolean IsAnonymousType(this Type type)
        {
            Boolean hasCompilerGeneratedAttribute = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any();
            Boolean nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
            Boolean isAnonymousType = hasCompilerGeneratedAttribute && nameContainsAnonymousType;

            return isAnonymousType;
        }

        public static bool IsEnumerable(this Type type)
        {
            if (type == typeof(Enumerable)) return true;
            if (type.IsGenericType() && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return true;

            IEnumerable<Type> types = type.GetInterfaces().Where(interfaceType => interfaceType.IsGenericType() && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return types.FirstOrDefault() != null;
        }

        public static bool IsExtensionMethod(this MethodInfo methodInfo)
        {
            return methodInfo.GetCustomAttribute(typeof(ExtensionAttribute)) != null;
        }

        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType() && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static Type NullableUnderlyingType(this Type type)
        {
            if (type.IsNullable())
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }
    }
}
