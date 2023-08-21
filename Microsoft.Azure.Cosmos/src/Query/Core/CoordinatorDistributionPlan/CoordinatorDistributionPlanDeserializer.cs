//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using ClientQL;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class CoordinatorDistributionPlanDeserializer
    {
        public CoordinatorDistributionPlan DeserializeCoordinatorDistributionPlan(string jsonString)
        {
            JObject token = JObject.Parse(jsonString);
            JsonSerializer serializer = new JsonSerializer();

            ClientQLExpression clientQL = this.DeserializeClientQLEnumerableExpression(token["coordinatorDistributionPlan"]["clientQL"], serializer);

            return new CoordinatorDistributionPlan(clientQL);
        }

        public ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            string kind = token.Value<string>("Kind");

            switch (kind)
            {
                case "Aggregate":
                    return this.DeserializeAggregateEnumerableExpression(token, serializer);
                case "Distinct":
                    return this.DeserializeDistinctEnumerableExpression(token, serializer);
                case "GroupBy":
                    return this.DeserializeGroupByEnumerableExpression(token, serializer);
                case "Flatten":
                    return this.DeserializeFlattenEnumerableExpression(token, serializer);
                case "Input":
                    return this.DeserializeInputEnumerableExpression(token, serializer);
                case "OrderBy":
                    return this.DeserializeOrderByEnumerableExpression(token, serializer);
                case "ScalarAsEnumerable":
                    return this.DeserializeScalarAsEnumerableExpression(token, serializer);
                case "Select":
                    return this.DeserializeSelectEnumerableExpression(token, serializer);
                case "SelectMany":
                    return this.DeserializeSelectManyExpression(token, serializer);
                case "Take":
                    return this.DeserializeTakeEnumerableExpression(token, serializer);
                case "Where":
                    return this.DeserializeWhereEnumerableExpression(token, serializer);
                default:
                    throw new JsonException($"Invalid ClientQLExpression kind: {kind}");
            }
        }

        public ClientQLAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLAggregate aggregate = this.DeserializeAggregate(token["Aggregate"]);

            return new ClientQLAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        public ClientQLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]);
            List<ClientQLScalarExpression> vecExpressions = this.DeserializeScalarExpressionArray(token["VecExpression"], serializer);

            return new ClientQLDistinctEnumerableExpression(sourceExpression, declaredVariable, vecExpressions);
        }

        public ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            List<ClientQLGroupByKey> vecKeys = this.DeserializeGroupByKeys(token["VecKeys"]);
            List<ClientQLAggregate> vecAggregates = this.DeserializeAggregates(token["VecAggregates"]);

            return new ClientQLGroupByEnumerableExpression(sourceExpression, vecKeys, vecAggregates);
        }

        public ClientQLFlattenEnumerableExpression DeserializeFlattenEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);

            return new ClientQLFlattenEnumerableExpression(sourceExpression);
        }

        public ClientQLInputEnumerableExpression DeserializeInputEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            return new ClientQLInputEnumerableExpression(token.Value<string>("Name"));
        }

        public ClientQLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression source = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]);
            List<ClientQLOrderByItem> orderByItems = this.DeserializeOrderByItems(token["VecItems"], serializer);

            return new ClientQLOrderByEnumerableExpression(source, declaredVariable, orderByItems);
        }

        public ClientQLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);
            ClientQLEnumerationKind enumerationKind = (ClientQLEnumerationKind)Enum.Parse(typeof(ClientQLEnumerationKind), token.Value<string>("EnumerationKind"));

            return new ClientQLScalarAsEnumerableExpression(expression, enumerationKind);
        }

        public ClientQLSelectEnumerableExpression DeserializeSelectEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]);
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        public ClientQLSelectManyEnumerableExpression DeserializeSelectManyExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLVariable declaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]);
            ClientQLEnumerableExpression selectorExpression = this.DeserializeClientQLEnumerableExpression(token["SelectorExpression"], serializer);

            return new ClientQLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        public ClientQLTakeEnumerableExpression DeserializeTakeEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            int skipValue = token.Value<int>("SkipValue");
            int takeExpression = token.Value<int>("TakeValue");
            
            return new ClientQLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
        }

        public ClientQLWhereEnumerableExpression DeserializeWhereEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            ClientQLDelegate clientDelegate = this.DeserializeDelegateExpression(token["Delegate"]);

            return new ClientQLWhereEnumerableExpression(sourceExpression, clientDelegate);
        }

        public ClientQLScalarExpression DeserializeScalarExpression(JToken token, JsonSerializer serializer)
        {
            object scalarExpressionKind = (ClientQLScalarExpressionKind)Enum.Parse(typeof(ClientQLScalarExpressionKind), token.Value<string>("Kind"));

            switch (scalarExpressionKind)
            {
                case ClientQLScalarExpressionKind.ArrayCreate:
                    return this.DeserializeArrayCreateScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.ArrayIndexer:
                    return this.DeserializeArrayIndexerScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.BinaryOperator:
                    return this.DeserializeBinaryOperatorScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.IsOperator:
                    return this.DeserializeIsOperatorScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.Let:
                    return this.DeserializeLetScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.Literal:
                    return this.DeserializeLiteralScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.Mux:
                    return this.DeserializeMuxScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.ObjectCreate:
                    return this.DeserializeObjectCreateScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.PropertyRef:
                    return this.DeserializePropertyRefScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.SystemFunctionCall:
                    return this.DeserializeSystemFunctionCallScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.TupleCreate:
                    return this.DeserializeTupleCreateScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.TupleItemRef:
                    return this.DeserializeTupleItemRefScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.UnaryOperator:
                    return this.DeserializeUnaryScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.UserDefinedFunctionCall:
                    return this.DeserializeUserDefinedFunctionCallScalarExpression(token, serializer);
                case ClientQLScalarExpressionKind.VariableRef:
                    return this.DeserializeVariableRefScalarExpression(token, serializer);
                default:
                    throw new JsonException($"Invalid ClientQLScalarExpressionKind: {scalarExpressionKind}");
            }
        }

        public ClientQLArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLArrayKind arrayKind = token.Value<ClientQLArrayKind>("ArrayKind");
            List<ClientQLScalarExpression> vecItems = this.DeserializeScalarExpressionArray(token["VecItems"], serializer);

            return new ClientQLArrayCreateScalarExpression(arrayKind, vecItems);
        }

        public ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);
            int index = token.Value<int>("Index");
            
            return new ClientQLArrayIndexerScalarExpression(expression, index);
        }

        public ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLBinaryScalarOperatorKind operatorKind = (ClientQLBinaryScalarOperatorKind)Enum.Parse(typeof(ClientQLBinaryScalarOperatorKind), token.Value<string>("OperatorKind"));
            int maxDepth = token.Value<int>("MaxDepth");
            ClientQLScalarExpression leftExpression = this.DeserializeScalarExpression(token["LeftExpression"], serializer);
            ClientQLScalarExpression rightExpression = this.DeserializeScalarExpression(token["RightExpression"], serializer);

            return new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
        }

        public ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLIsOperatorKind operatorKind = (ClientQLIsOperatorKind)Enum.Parse(typeof(ClientQLIsOperatorKind), token.Value<string>("OperatorKind"));
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLIsOperatorScalarExpression(operatorKind, expression);
        }

        public ClientQLLetScalarExpression DeserializeLetScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariable declaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]);
            ClientQLScalarExpression declaredVariableExpression = this.DeserializeScalarExpression(token["DeclaredVariableExpression"], serializer);
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        public ClientQLLiteralScalarExpression DeserializeLiteralScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLLiteral literal = new ClientQLLiteral((ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal);

            return new ClientQLLiteralScalarExpression(literal);
            
        }

        public ClientQLMuxScalarExpression DeserializeMuxScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression conditionExpression = this.DeserializeScalarExpression(token["ConditionExpression"], serializer);
            ClientQLScalarExpression leftExpression = this.DeserializeScalarExpression(token["LeftExpression"], serializer);
            ClientQLScalarExpression rightExpression = this.DeserializeScalarExpression(token["RightExpression"], serializer);

            return new ClientQLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        public ClientQLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            string objectKindString = token.Value<string>("ObjectKind");
            if (!Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind))
            {
                throw new JsonException($"Invalid ClientQLObjectKind: {objectKindString}");
            }

            List<ClientQLObjectProperty> properties = this.DeserializeObjectProperties(token["Properties"], serializer);

            return new ClientQLObjectCreateScalarExpression(properties, objectKind);
        }

        public ClientQLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);
            string propertyName = token.Value<string>("PropertyName");

            return new ClientQLPropertyRefScalarExpression(expression, propertyName);
        }

        public ClientQLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLBuiltinScalarFunctionKind functionKind = (ClientQLBuiltinScalarFunctionKind)Enum.Parse(typeof(ClientQLBuiltinScalarFunctionKind), token.Value<string>("FunctionKind"));
            List<ClientQLScalarExpression> vecArguments = this.DeserializeScalarExpressionArray(token["VecArguments"], serializer);

            return new ClientQLSystemFunctionCallScalarExpression(functionKind, vecArguments);
        }

        public ClientQLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            List<ClientQLScalarExpression> vecItems = this.DeserializeScalarExpressionArray(token["Items"], serializer);

            return new ClientQLTupleCreateScalarExpression(vecItems);
        }

        public ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);
            int index = token.Value<int>("Index");

            return new ClientQLTupleItemRefScalarExpression(expression, index);
        }

        public ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLUnaryScalarOperatorKind operatorKind = (ClientQLUnaryScalarOperatorKind)Enum.Parse(typeof(ClientQLUnaryScalarOperatorKind), token.Value<string>("OperatorKind"));
            ClientQLScalarExpression expression = this.DeserializeScalarExpression(token["Expression"], serializer);

            return new ClientQLUnaryScalarExpression(operatorKind, expression);
        }

        public ClientQLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLFunctionIdentifier identifier = token["Identifier"].ToObject<ClientQLFunctionIdentifier>(serializer);
            List<ClientQLScalarExpression> vecArguments = this.DeserializeScalarExpressionArray(token["VecArguments"], serializer);
            bool builtin = token.Value<bool>("Builtin");

            return new ClientQLUserDefinedFunctionCallScalarExpression(identifier, vecArguments, builtin);
        }

        public ClientQLVariableRefScalarExpression DeserializeVariableRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariable variable = this.DeserializeClientQLVariable(token["Variable"]);

            return new ClientQLVariableRefScalarExpression(variable);
        }

        public ClientQLDelegate DeserializeDelegateExpression(JToken token)
        {
            ClientQLDelegateKind kind = (ClientQLDelegateKind)Enum.Parse(typeof(ClientQLDelegateKind), token.Value<string>("Kind"));
            ClientQLType type = this.DeserializeType(token["Type"]);

            return new ClientQLDelegate(kind, type);
        }

        private ClientQLType DeserializeType(JToken token)
        {
            ClientQLTypeKind kind = (ClientQLTypeKind)Enum.Parse(typeof(ClientQLTypeKind), token.Value<string>("Kind"));

            return new ClientQLType(kind);
        }

        public ClientQLAggregate DeserializeAggregate(JToken token)
        {
            ClientQLAggregateKind kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), token.Value<string>("Kind"));
            string operatorKind = token["OperatorKind"].ToString();

            return new ClientQLAggregate(kind, operatorKind);
        }

        public ClientQLVariable DeserializeClientQLVariable(JToken token)
        {
            string name = token.Value<string>("Name");
            int uniqueId = token.Value<int>("UniqueId");

            return new ClientQLVariable(name, uniqueId);
        }

        public List<ClientQLObjectProperty> DeserializeObjectProperties(JToken token, JsonSerializer serializer)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken propertyToken in token)
                {
                    string name = propertyToken.Value<string>("Name");
                    ClientQLScalarExpression expression = this.DeserializeScalarExpression(propertyToken["Expression"], serializer);
                    properties.Add(new ClientQLObjectProperty(name, expression));
                }
            }
            return properties;
        }

        private List<ClientQLGroupByKey> DeserializeGroupByKeys(JToken token)
        {
            List<ClientQLGroupByKey> groupByKeys = new List<ClientQLGroupByKey>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken keyToken in token)
                {
                    ClientQLType type = this.DeserializeType(keyToken["Type"]);
                    groupByKeys.Add(new ClientQLGroupByKey(type));
                }
            }
            return groupByKeys;
        }

        private List<ClientQLAggregate> DeserializeAggregates(JToken token)
        {
            List<ClientQLAggregate> aggregates = new List<ClientQLAggregate>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken aggregateToken in token)
                {
                    ClientQLAggregateKind kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), aggregateToken.Value<string>("Kind"));
                    string operatorKind = aggregateToken.Value<string>("OperatorKind");
                    aggregates.Add(new ClientQLAggregate(kind, operatorKind));
                }
            }
            return aggregates;
        }

        private List<ClientQLOrderByItem> DeserializeOrderByItems(JToken token, JsonSerializer serializer)
        {
            List<ClientQLOrderByItem> orderByItems = new List<ClientQLOrderByItem>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken itemToken in token)
                {
                    ClientQLScalarExpression expression = this.DeserializeScalarExpression(itemToken["Expression"], serializer);
                    ClientQLSortOrder sortOrder = (ClientQLSortOrder)Enum.Parse(typeof(ClientQLSortOrder), itemToken.Value<string>("SortOrder"));
                    orderByItems.Add(new ClientQLOrderByItem(expression, sortOrder));
                }
            }
            return orderByItems;
        }

        private List<ClientQLScalarExpression> DeserializeScalarExpressionArray(JToken token, JsonSerializer serializer)
        {
            List<ClientQLScalarExpression> properties = new List<ClientQLScalarExpression>();
            if (token != null)
            {
                foreach (JToken propertyToken in token)
                {
                    ClientQLScalarExpression expression = this.DeserializeScalarExpression(propertyToken, serializer);
                    properties.Add(expression);
                }
            }
            return properties;
        }
    }
}