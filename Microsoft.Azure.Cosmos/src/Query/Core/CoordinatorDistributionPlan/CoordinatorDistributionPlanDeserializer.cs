//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using ClientQL;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class CoordinatorDistributionPlanDeserializer
    {
        private static class EnumerableConstants
        {
            public const string Aggregate = "Aggregate";
            public const string Distinct = "Distinct";
            public const string GroupBy = "GroupBy";
            public const string Flatten = "Flatten";
            public const string Input = "Input";
            public const string OrderBy = "OrderBy";
            public const string ScalarAsEnumerable = "ScalarAsEnumerable";
            public const string Select = "Select";
            public const string SelectMany = "SelectMany";
            public const string Take = "Take";
            public const string Where = "Where";
        }

        public static CoordinatorDistributionPlan DeserializeCoordinatorDistributionPlan(string jsonString)
        {
            JObject token = JObject.Parse(jsonString);
            JsonSerializer serializer = new JsonSerializer();

            ClientQLExpression clientQL = DeserializeClientQLEnumerableExpression(token["coordinatorDistributionPlan"]["clientQL"], serializer);

            return new CoordinatorDistributionPlan(clientQL);
        }

        private static ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            string kind = TryGetValue<string>(token, "Kind");

            switch (kind)
            {
                case EnumerableConstants.Aggregate:
                    return DeserializeAggregateEnumerableExpression(token, serializer);
                case EnumerableConstants.Distinct:
                    return DeserializeDistinctEnumerableExpression(token, serializer);
                case EnumerableConstants.GroupBy:
                    return DeserializeGroupByEnumerableExpression(token, serializer);
                case EnumerableConstants.Flatten:
                    return DeserializeFlattenEnumerableExpression(token, serializer);
                case EnumerableConstants.Input:
                    return DeserializeInputEnumerableExpression(token, serializer);
                case EnumerableConstants.OrderBy:
                    return DeserializeOrderByEnumerableExpression(token, serializer);
                case EnumerableConstants.ScalarAsEnumerable:
                    return DeserializeScalarAsEnumerableExpression(token, serializer);
                case EnumerableConstants.Select:
                    return DeserializeSelectEnumerableExpression(token, serializer);
                case EnumerableConstants.SelectMany:
                    return DeserializeSelectManyExpression(token, serializer);
                case EnumerableConstants.Take:
                    return DeserializeTakeEnumerableExpression(token, serializer);
                case EnumerableConstants.Where:
                    return DeserializeWhereEnumerableExpression(token, serializer);
                default:
                    throw new JsonException($"Invalid ClientQLExpression kind: {kind}");
            }
        }

        private static ClientQLAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLAggregate aggregate = DeserializeAggregate(token["Aggregate"]);

            return new ClientQLAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        private static ClientQLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token["DeclaredVariable"]);
            IReadOnlyList<ClientQLScalarExpression> vecExpressions = DeserializeScalarExpressionArray(token["VecExpression"], serializer);

            return new ClientQLDistinctEnumerableExpression(sourceExpression, declaredVariable, vecExpressions);
        }

        private static ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            IReadOnlyList<ClientQLGroupByKey> vecKeys = DeserializeGroupByKeys(token["VecKeys"]);
            IReadOnlyList<ClientQLAggregate> vecAggregates = DeserializeAggregates(token["VecAggregates"]);

            return new ClientQLGroupByEnumerableExpression(sourceExpression, vecKeys, vecAggregates);
        }

        private static ClientQLFlattenEnumerableExpression DeserializeFlattenEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);

            return new ClientQLFlattenEnumerableExpression(sourceExpression);
        }

        private static ClientQLInputEnumerableExpression DeserializeInputEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            return new ClientQLInputEnumerableExpression(TryGetValue<string>(token, "Name"));
        }

        private static ClientQLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression source = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token["DeclaredVariable"]);
            IReadOnlyList<ClientQLOrderByItem> orderByItems = DeserializeOrderByItems(token["VecItems"], serializer);

            return new ClientQLOrderByEnumerableExpression(source, declaredVariable, orderByItems);
        }

        private static ClientQLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);
            ClientQLEnumerationKind enumerationKind = (ClientQLEnumerationKind)Enum.Parse(typeof(ClientQLEnumerationKind), TryGetValue<string>(token, "EnumerationKind"));

            return new ClientQLScalarAsEnumerableExpression(expression, enumerationKind);
        }

        private static ClientQLSelectEnumerableExpression DeserializeSelectEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token["DeclaredVariable"]);
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        private static ClientQLSelectManyEnumerableExpression DeserializeSelectManyExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token["DeclaredVariable"]);
            ClientQLEnumerableExpression selectorExpression = DeserializeClientQLEnumerableExpression(token["SelectorExpression"], serializer);

            return new ClientQLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        private static ClientQLTakeEnumerableExpression DeserializeTakeEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            int skipValue = TryGetValue<int>(token, "SkipValue");
            int takeExpression = TryGetValue<int>(token, "TakeValue");

            return new ClientQLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
        }

        private static ClientQLWhereEnumerableExpression DeserializeWhereEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLDelegate clientDelegate = DeserializeDelegateExpression(token["Delegate"]);

            return new ClientQLWhereEnumerableExpression(sourceExpression, clientDelegate);
        }

        private static ClientQLScalarExpression DeserializeScalarExpression(JToken token, JsonSerializer serializer)
        {
            object scalarExpressionKind = (ClientQLScalarExpressionKind)Enum.Parse(typeof(ClientQLScalarExpressionKind), TryGetValue<string>(token, "Kind"));

            switch (scalarExpressionKind)
            {
                case ClientQLScalarExpressionKind.ArrayCreate:
                    return DeserializeArrayCreateScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.ArrayIndexer:
                    return DeserializeArrayIndexerScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.BinaryOperator:
                    return DeserializeBinaryOperatorScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.IsOperator:
                    return DeserializeIsOperatorScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.Let:
                    return DeserializeLetScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.Literal:
                    return DeserializeLiteralScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.Mux:
                    return DeserializeMuxScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.ObjectCreate:
                    return DeserializeObjectCreateScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.PropertyRef:
                    return DeserializePropertyRefScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.SystemFunctionCall:
                    return DeserializeSystemFunctionCallScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.TupleCreate:
                    return DeserializeTupleCreateScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.TupleItemRef:
                    return DeserializeTupleItemRefScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.UnaryOperator:
                    return DeserializeUnaryScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.UserDefinedFunctionCall:
                    return DeserializeUserDefinedFunctionCallScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.VariableRef:
                    return DeserializeVariableRefScalarExpression(token, serializer);
                default:
                    throw new JsonException($"Invalid ClientQLScalarExpressionKind: {scalarExpressionKind}");
            }
        }

        private static ClientQLArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLArrayKind arrayKind = TryGetValue<ClientQLArrayKind>(token, "ArrayKind");
            IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray(token["VecItems"], serializer);

            return new ClientQLArrayCreateScalarExpression(arrayKind, vecItems);
        }

        private static ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);
            int index = TryGetValue<int>(token, "Index");
            
            return new ClientQLArrayIndexerScalarExpression(expression, index);
        }

        private static ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLBinaryScalarOperatorKind operatorKind = (ClientQLBinaryScalarOperatorKind)Enum.Parse(typeof(ClientQLBinaryScalarOperatorKind), TryGetValue<string>(token, "OperatorKind"));
            int maxDepth = TryGetValue<int>(token, "MaxDepth");
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(token["LeftExpression"], serializer);
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(token["RightExpression"], serializer);

            return new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
        }

        private static ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLIsOperatorKind operatorKind = (ClientQLIsOperatorKind)Enum.Parse(typeof(ClientQLIsOperatorKind), TryGetValue<string>(token, "OperatorKind"));
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLIsOperatorScalarExpression(operatorKind, expression);
        }

        private static ClientQLLetScalarExpression DeserializeLetScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token["DeclaredVariable"]);
            ClientQLScalarExpression declaredVariableExpression = DeserializeScalarExpression(token["DeclaredVariableExpression"], serializer);
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        private static ClientQLLiteralScalarExpression DeserializeLiteralScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLLiteral literal = new ClientQLLiteral((ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal);

            return new ClientQLLiteralScalarExpression(literal);
            
        }

        private static ClientQLMuxScalarExpression DeserializeMuxScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression conditionExpression = DeserializeScalarExpression(token["ConditionExpression"], serializer);
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(token["LeftExpression"], serializer);
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(token["RightExpression"], serializer);

            return new ClientQLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        private static ClientQLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            string objectKindString = TryGetValue<string>(token, "ObjectKind");
            if (!Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind))
            {
                throw new JsonException($"Invalid ClientQLObjectKind: {objectKindString}");
            }

            IReadOnlyList<ClientQLObjectProperty> properties = DeserializeObjectProperties(token["Properties"], serializer);

            return new ClientQLObjectCreateScalarExpression(properties, objectKind);
        }

        private static ClientQLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);
            string propertyName = TryGetValue<string>(token, "PropertyName");

            return new ClientQLPropertyRefScalarExpression(expression, propertyName);
        }

        private static ClientQLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLBuiltinScalarFunctionKind functionKind = (ClientQLBuiltinScalarFunctionKind)Enum.Parse(typeof(ClientQLBuiltinScalarFunctionKind), TryGetValue<string>(token, "FunctionKind"));
            IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray(token["VecArguments"], serializer);

            return new ClientQLSystemFunctionCallScalarExpression(functionKind, vecArguments);
        }

        private static ClientQLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray(token["Items"], serializer);

            return new ClientQLTupleCreateScalarExpression(vecItems);
        }

        private static ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);
            int index = TryGetValue<int>(token, "Index");

            return new ClientQLTupleItemRefScalarExpression(expression, index);
        }

        private static ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLUnaryScalarOperatorKind operatorKind = (ClientQLUnaryScalarOperatorKind)Enum.Parse(typeof(ClientQLUnaryScalarOperatorKind), TryGetValue<string>(token, "OperatorKind"));
            ClientQLScalarExpression expression = DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLUnaryScalarExpression(operatorKind, expression);
        }

        private static ClientQLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLFunctionIdentifier identifier = token["Identifier"].ToObject<ClientQLFunctionIdentifier>(serializer);
            IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray(token["VecArguments"], serializer);
            bool builtin = TryGetValue<bool>(token, "Builtin");

            return new ClientQLUserDefinedFunctionCallScalarExpression(identifier, vecArguments, builtin);
        }

        private static ClientQLVariableRefScalarExpression DeserializeVariableRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariable variable = DeserializeClientQLVariable(token["Variable"]);

            return new ClientQLVariableRefScalarExpression(variable);
        }

        private static ClientQLDelegate DeserializeDelegateExpression(JToken token)
        {
            ClientQLDelegateKind kind = (ClientQLDelegateKind)Enum.Parse(typeof(ClientQLDelegateKind), TryGetValue<string>(token, "Kind"));
            ClientQLType type = DeserializeType(token["Type"]);

            return new ClientQLDelegate(kind, type);
        }

        private static ClientQLType DeserializeType(JToken token)
        {
            ClientQLTypeKind kind = (ClientQLTypeKind)Enum.Parse(typeof(ClientQLTypeKind), TryGetValue<string>(token, "Kind"));

            return new ClientQLType(kind);
        }

        private static ClientQLAggregate DeserializeAggregate(JToken token)
        {
            ClientQLAggregateKind kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), TryGetValue<string>(token, "Kind"));
            string operatorKind = token["OperatorKind"].ToString();

            return new ClientQLAggregate(kind, operatorKind);
        }

        private static ClientQLVariable DeserializeClientQLVariable(JToken token)
        {
            string name = TryGetValue<string>(token, "Name");
            int uniqueId = TryGetValue<int>(token, "UniqueId");

            return new ClientQLVariable(name, uniqueId);
        }

        private static List<ClientQLObjectProperty> DeserializeObjectProperties(JToken token, JsonSerializer serializer)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken propertyToken in token)
                {
                    string name = TryGetValue<string>(propertyToken, "Name");
                    ClientQLScalarExpression expression = DeserializeScalarExpression(propertyToken["Expression"], serializer);
                    properties.Add(new ClientQLObjectProperty(name, expression));
                }
            }
            return properties;
        }

        private static List<ClientQLGroupByKey> DeserializeGroupByKeys(JToken token)
        {
            List<ClientQLGroupByKey> groupByKeys = new List<ClientQLGroupByKey>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken keyToken in token)
                {
                    ClientQLType type = DeserializeType(keyToken["Type"]);
                    groupByKeys.Add(new ClientQLGroupByKey(type));
                }
            }
            return groupByKeys;
        }

        private static List<ClientQLAggregate> DeserializeAggregates(JToken token)
        {
            List<ClientQLAggregate> aggregates = new List<ClientQLAggregate>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken aggregateToken in token)
                {
                    ClientQLAggregateKind kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), TryGetValue<string>(aggregateToken, "Kind"));
                    string operatorKind = TryGetValue<string>(aggregateToken, "OperatorKind");
                    aggregates.Add(new ClientQLAggregate(kind, operatorKind));
                }
            }
            return aggregates;
        }

        private static List<ClientQLOrderByItem> DeserializeOrderByItems(JToken token, JsonSerializer serializer)
        {
            List<ClientQLOrderByItem> orderByItems = new List<ClientQLOrderByItem>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken itemToken in token)
                {
                    ClientQLScalarExpression expression = DeserializeScalarExpression(itemToken["Expression"], serializer);
                    ClientQLSortOrder sortOrder = (ClientQLSortOrder)Enum.Parse(typeof(ClientQLSortOrder), TryGetValue<string>(itemToken, "SortOrder"));
                    orderByItems.Add(new ClientQLOrderByItem(expression, sortOrder));
                }
            }
            return orderByItems;
        }

        private static List<ClientQLScalarExpression> DeserializeScalarExpressionArray(JToken token, JsonSerializer serializer)
        {
            List<ClientQLScalarExpression> properties = new List<ClientQLScalarExpression>();
            if (token != null)
            {
                foreach (JToken propertyToken in token)
                {
                    ClientQLScalarExpression expression = DeserializeScalarExpression(propertyToken, serializer);
                    properties.Add(expression);
                }
            }
            return properties;
        }

        private static T TryGetValue<T>(JToken token, string expression)
        {
            try
            {
                return token.Value<T>(expression);
            }
            catch (Exception ex)
            {
                Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
                string clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
                throw new ArgumentNullException($"Customer SDK version is {clientSDKVersion}. Please upgrade if need be. " +
                    $"Error occured during deserialization of distribution plan. Please reach out to the CosmosDB query team to fix  " +
                    $"Error Message: {ex.InnerException}");
            }
        }
    }
}