//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ClientQL;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class CoordinatorDistributionPlanDeserializer
    {
        public CoordinatorDistributionPlan DeserializeCoordinatorDistributionPlan(string jsonString)
        {
            JObject token = JObject.Parse(jsonString);
            JsonSerializer serializer = new JsonSerializer();

            CoordinatorDistributionPlan plan = new CoordinatorDistributionPlan
            {
                ClientQL = this.DeserializeClientQLEnumerableExpression(token["coordinatorDistributionPlan"]["clientQL"], serializer)
            };

            return plan;
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
            ClientQLAggregateEnumerableExpression aggregateExpression = new ClientQLAggregateEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Aggregate,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                Aggregate = this.DeserializeAggregate(token["Aggregate"])
            };

            return aggregateExpression;
        }

        public ClientQLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLDistinctEnumerableExpression distinctExpression = new ClientQLDistinctEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Distinct,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                DeclaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]),
                VecExpression = token["VecExpression"].ToObject<List<ClientQLScalarExpression>>(serializer)
            };

            return distinctExpression;
        }

        public ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLGroupByEnumerableExpression groupByExpression = new ClientQLGroupByEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.GroupBy,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                VecKeys = this.DeserializeGroupByKeys(token["VecKeys"]),
                VecAggregates = this.DeserializeAggregates(token["VecAggregates"])
            };

            return groupByExpression;
        }

        public ClientQLFlattenEnumerableExpression DeserializeFlattenEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLFlattenEnumerableExpression flattenExpression = new ClientQLFlattenEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Flatten,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer)
            };

            return flattenExpression;
        }

        public ClientQLInputEnumerableExpression DeserializeInputEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLInputEnumerableExpression inputExpression = new ClientQLInputEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Input,
                Name = token.Value<string>("Name")
            };

            return inputExpression;
        }

        public ClientQLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLOrderByEnumerableExpression orderByExpression = new ClientQLOrderByEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.OrderBy,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                DeclaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]),
                VecItems = this.DeserializeOrderByItems(token["VecItems"], serializer)
            };

            return orderByExpression;
        }

        public ClientQLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarAsEnumerableExpression scalarAsEnumerableExpression = new ClientQLScalarAsEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.ScalarAsEnumerable,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                EnumerationKind = (ClientQLEnumerationKind)Enum.Parse(typeof(ClientQLEnumerationKind), token.Value<string>("EnumerationKind"))
            };

            return scalarAsEnumerableExpression;
        }

        public ClientQLSelectEnumerableExpression DeserializeSelectEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLSelectEnumerableExpression selectExpression = new ClientQLSelectEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Select,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                DeclaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]),
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer)
            };

            return selectExpression;
        }

        public ClientQLSelectManyEnumerableExpression DeserializeSelectManyExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLSelectManyEnumerableExpression selectManyExpression = new ClientQLSelectManyEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.SelectMany,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                DeclaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]),
                SelectorExpression = this.DeserializeClientQLEnumerableExpression(token["SelectorExpression"], serializer)
            };

            return selectManyExpression;
        }

        public ClientQLTakeEnumerableExpression DeserializeTakeEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLTakeEnumerableExpression takeExpression = new ClientQLTakeEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Take,

                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                SkipValue = token.Value<int>("SkipValue"),
                TakeValue = token.Value<int>("TakeValue")
            };

            return takeExpression;
        }

        public ClientQLWhereEnumerableExpression DeserializeWhereEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLWhereEnumerableExpression whereExpression = new ClientQLWhereEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Where,

                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                Delegate = this.DeserializeDelegateExpression(token["Delegate"])
            };

            return whereExpression;
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
            ClientQLArrayCreateScalarExpression arrayCreateExpression = new ClientQLArrayCreateScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.ArrayCreate,
                ArrayKind = token.Value<ClientQLArrayKind>("ArrayKind"),
                VecItems = token["VecItems"].ToObject<List<ClientQLScalarExpression>>(serializer)
            };

            return arrayCreateExpression;
        }

        public ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLArrayIndexerScalarExpression arrayIndexerExpression = new ClientQLArrayIndexerScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.ArrayIndexer,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                Index = token.Value<int>("Index")
            };

            return arrayIndexerExpression;
        }

        public ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLBinaryScalarExpression binaryExpression = new ClientQLBinaryScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.BinaryOperator,
                OperatorKind = (ClientQLBinaryScalarOperatorKind)Enum.Parse(typeof(ClientQLBinaryScalarOperatorKind), token.Value<string>("OperatorKind")),
                MaxDepth = token.Value<int>("MaxDepth"),
                LeftExpression = this.DeserializeScalarExpression(token["LeftExpression"], serializer),
                RightExpression = this.DeserializeScalarExpression(token["RightExpression"], serializer)
            };

            return binaryExpression;
        }

        public ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLIsOperatorScalarExpression isOperatorExpression = new ClientQLIsOperatorScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.IsOperator,
                OperatorKind = (ClientQLIsOperatorKind)Enum.Parse(typeof(ClientQLIsOperatorKind), token.Value<string>("OperatorKind")),
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer)
            };

            return isOperatorExpression;
        }

        public ClientQLLetScalarExpression DeserializeLetScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLLetScalarExpression letExpression = new ClientQLLetScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.Let,
                DeclaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"]),
                DeclaredVariableExpression = this.DeserializeScalarExpression(token["DeclaredVariableExpression"], serializer),
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer)
            };

            return letExpression;
        }

        public ClientQLLiteralScalarlExpression DeserializeLiteralScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLLiteralScalarlExpression literalExpression = new ClientQLLiteralScalarlExpression
            {
                Kind = (ClientQLScalarExpressionKind)Enum.Parse(typeof(ClientQLScalarExpressionKind), token.Value<string>("Kind")),
                Literal = new ClientQLLiteral
                {
                    Kind = (ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal
                }
            };

            return literalExpression;
        }

        public ClientQLMuxScalarExpression DeserializeMuxScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLMuxScalarExpression muxExpression = new ClientQLMuxScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.Mux,
                ConditionExpression = this.DeserializeScalarExpression(token["ConditionExpression"], serializer),
                LeftExpression = this.DeserializeScalarExpression(token["LeftExpression"], serializer),
                RightExpression = this.DeserializeScalarExpression(token["RightExpression"], serializer)
            };

            return muxExpression;
        }

        public ClientQLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLObjectCreateScalarExpression objectCreateExpression = new ClientQLObjectCreateScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.ObjectCreate
            };

            string objectKindString = token.Value<string>("ObjectKind");
            if (!Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind))
            {
                throw new JsonException($"Invalid ClientQLObjectKind: {objectKindString}");
            }

            objectCreateExpression.ObjectKind = objectKind;
            objectCreateExpression.Properties = this.DeserializeObjectProperties(token["Properties"], serializer);

            return objectCreateExpression;
        }

        public ClientQLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLPropertyRefScalarExpression propertyRefExpression = new ClientQLPropertyRefScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.PropertyRef,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                PropertyName = token.Value<string>("PropertyName")
            };

            return propertyRefExpression;
        }

        public ClientQLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLSystemFunctionCallScalarExpression functionCallExpression = new ClientQLSystemFunctionCallScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.SystemFunctionCall,
                FunctionKind = (ClientQLBuiltinScalarFunctionKind)Enum.Parse(typeof(ClientQLBuiltinScalarFunctionKind), token.Value<string>("FunctionKind")),
                VecArguments = token["VecArguments"]
                .Select(argToken => this.DeserializeScalarExpression(argToken, serializer))
                .ToList()
            };

            return functionCallExpression;
        }

        public ClientQLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLTupleCreateScalarExpression tupleCreateExpression = new ClientQLTupleCreateScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.TupleCreate,
                VecItems = token["Items"].ToObject<List<ClientQLScalarExpression>>(serializer)
            };

            return tupleCreateExpression;
        }

        public ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLTupleItemRefScalarExpression tupleItemRefExpression = new ClientQLTupleItemRefScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.TupleItemRef,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                Index = token.Value<int>("Index")
            };

            return tupleItemRefExpression;
        }

        public ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLUnaryScalarExpression unaryExpression = new ClientQLUnaryScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.UnaryOperator,
                OperatorKind = (ClientQLUnaryScalarOperatorKind)Enum.Parse(typeof(ClientQLUnaryScalarOperatorKind), token.Value<string>("OperatorKind")),
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer)
            };

            return unaryExpression;
        }

        public ClientQLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLUserDefinedFunctionCallScalarExpression userDefinedFunctionCallExpression = new ClientQLUserDefinedFunctionCallScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.UserDefinedFunctionCall,
                Identifier = token["Identifier"].ToObject<ClientQLFunctionIdentifier>(serializer),
                VecArguments = token["VecArguments"].ToObject<List<ClientQLScalarExpression>>(serializer),
                Builtin = token.Value<bool>("Builtin")
            };

            return userDefinedFunctionCallExpression;
        }

        public ClientQLVariableRefScalarExpression DeserializeVariableRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariableRefScalarExpression variableRefExpression = new ClientQLVariableRefScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.VariableRef,
                Variable = this.DeserializeClientQLVariable(token["Variable"])
            };

            return variableRefExpression;
        }

        private List<ClientQLOrderByItem> DeserializeOrderByItems(JToken token, JsonSerializer serializer)
        {
            List<ClientQLOrderByItem> orderByItems = new List<ClientQLOrderByItem>();

            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken itemToken in token)
                {
                    ClientQLOrderByItem orderByItem = new ClientQLOrderByItem
                    {
                        Expression = this.DeserializeScalarExpression(itemToken["Expression"], serializer),
                        SortOrder = (ClientQLSortOrder)Enum.Parse(typeof(ClientQLSortOrder), itemToken.Value<string>("SortOrder"))
                    };

                    orderByItems.Add(orderByItem);
                }
            }

            return orderByItems;
        }

        public ClientQLDelegate DeserializeDelegateExpression(JToken token)
        {
            ClientQLDelegate delegateExpression = new ClientQLDelegate
            {
                Kind = (ClientQLDelegateKind)Enum.Parse(typeof(ClientQLDelegateKind), token.Value<string>("Kind")),
                Type = this.DeserializeType(token["Type"])
            };

            return delegateExpression;
        }

        private ClientQLType DeserializeType(JToken token)
        {
            ClientQLType type = new ClientQLType
            {
                Kind = (ClientQLTypeKind)Enum.Parse(typeof(ClientQLTypeKind), token.Value<string>("Kind"))
            };

            return type;
        }

        public ClientQLAggregate DeserializeAggregate(JToken token)
        {
            ClientQLAggregate aggregate = new ClientQLAggregate
            {
                Kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), token.Value<string>("Kind")),
                OperatorKind = token["OperatorKind"].ToString()
            };

            return aggregate;
        }

        public ClientQLVariable DeserializeClientQLVariable(JToken token)
        {
            ClientQLVariable variable = new ClientQLVariable
            {
                Name = token.Value<string>("Name"),
                UniqueId = token.Value<int>("UniqueId")
            };

            return variable;
        }

        public List<ClientQLObjectProperty> DeserializeObjectProperties(JToken token, JsonSerializer serializer)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            if (token != null && token.Type == JTokenType.Array)
            {
                foreach (JToken propertyToken in token)
                {
                    ClientQLObjectProperty property = new ClientQLObjectProperty
                    {
                        Name = propertyToken.Value<string>("Name"),
                        Expression = this.DeserializeScalarExpression(propertyToken["Expression"], serializer)
                    };
                    properties.Add(property);
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
                    ClientQLGroupByKey groupByKey = new ClientQLGroupByKey
                    {
                        Type = this.DeserializeType(keyToken["Type"])
                    };
                    groupByKeys.Add(groupByKey);
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
                    ClientQLAggregate aggregate = new ClientQLAggregate
                    {
                        Kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), aggregateToken.Value<string>("Kind")),
                        OperatorKind = aggregateToken.Value<string>("OperatorKind")
                    };
                    aggregates.Add(aggregate);
                }
            }

            return aggregates;
        }
    }
}