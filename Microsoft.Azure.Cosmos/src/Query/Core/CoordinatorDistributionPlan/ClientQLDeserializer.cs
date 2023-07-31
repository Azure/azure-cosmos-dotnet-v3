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

    internal class ClientQLDeserializer : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(CoordinatorDistributionPlan);
        }

        public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            JObject jsonObject = JObject.Load(reader);
            CoordinatorDistributionPlan plan = new CoordinatorDistributionPlan
            {
                ClientQL = this.DeserializeClientQLEnumerableExpression(jsonObject.GetValue("coordinatorDistributionPlan")["clientQL"], serializer)
            };

            return plan;
        }

        public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            string kind = token.Value<string>("Kind");

            switch (kind)
            {
                case "Select":
                    return this.DeserializeSelectExpression(token, serializer);
                case "Input":
                    return this.DeserializeInputExpression(token, serializer);
                case "Aggregate":
                    return this.DeserializeAggregateExpression(token, serializer);
                case "GroupBy":
                    return this.DeserializeGroupByEnumerableExpression(token, serializer);
                default:
                    throw new System.Text.Json.JsonException($"Invalid ClientQLExpression kind: {kind}");
            }
        }

        public ClientQLSelectEnumerableExpression DeserializeSelectExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLSelectEnumerableExpression selectExpression = new ClientQLSelectEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Select,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                DeclaredVariable = this.DeserializeClientQLVariable(token["DeclaredVariable"], serializer),
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer)
            };

            return selectExpression;
        }

        public ClientQLInputEnumerableExpression DeserializeInputExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLInputEnumerableExpression inputExpression = new ClientQLInputEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Input,
                Name = token.Value<string>("Name")
            };

            return inputExpression;
        }

        public ClientQLAggregateEnumerableExpression DeserializeAggregateExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLAggregateEnumerableExpression aggregateExpression = new ClientQLAggregateEnumerableExpression
            {
                Kind = ClientQLEnumerableExpressionKind.Aggregate,
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                Aggregate = this.DeserializeAggregate(token["Aggregate"], serializer)
            };

            return aggregateExpression;
        }

        public ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLGroupByEnumerableExpression groupByExpression = new ClientQLGroupByEnumerableExpression
            {
                Kind = (ClientQLEnumerableExpressionKind)Enum.Parse(typeof(ClientQLEnumerableExpressionKind), token.Value<string>("Kind")),
                SourceExpression = this.DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer),
                //groupByExpression.VecKeys = List<ClientQLGroupByKey>(Enum.Parse<ClientQLEnumerableExpressionKind>(token.Value<string>("Kind")));//token["KeyCount"].ToObject<List<ClientQLGroupByKey>>(serializer); // some issue here
                VecAggregates = token["Aggregates"].ToObject<List<ClientQLAggregate>>(serializer)
            };

            return groupByExpression;
        }

        public ClientQLAggregate DeserializeAggregate(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLAggregate aggregate = new ClientQLAggregate
            {
                Kind = (ClientQLAggregateKind)Enum.Parse(typeof(ClientQLAggregateKind), token.Value<string>("Kind")),
                OperatorKind = token["OperatorKind"].ToString()
            };

            return aggregate;
        }

        public ClientQLObjectCreateScalarExpression DeserializeObjectCreateExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLObjectCreateScalarExpression objectCreateExpression = new ClientQLObjectCreateScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.ObjectCreate
            };

            string objectKindString = token.Value<string>("ObjectKind");
            if (!Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind))
            {
                throw new System.Text.Json.JsonException($"Invalid ClientQLObjectKind: {objectKindString}");
            }

            objectCreateExpression.ObjectKind = objectKind;

            objectCreateExpression.Properties = this.DeserializeObjectProperties(token["Properties"], serializer);
            return objectCreateExpression;
        }

        public ClientQLVariableRefScalarExpression DeserializeVariableRefExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLVariableRefScalarExpression variableRefExpression = new ClientQLVariableRefScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.VariableRef,
                Variable = this.DeserializeClientQLVariable(token["Variable"], serializer)
            };

            return variableRefExpression;
        }

        public ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLTupleItemRefScalarExpression tupleItemRefExpression = new ClientQLTupleItemRefScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.TupleItemRef,
                Expression = this.DeserializeScalarExpression(token["Expression"], serializer),
                Index = token.Value<int>("Index")
            };

            return tupleItemRefExpression;
        }

        public ClientQLMuxScalarExpression DeserializeMuxExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
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

        public ClientQLBinaryScalarExpression DeserializeBinaryOperatorExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
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

        public ClientQLLiteralScalarExpression DeserializeLiteralExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer) 
        {
            ClientQLLiteralScalarExpression literalExpression = new ClientQLLiteralScalarExpression
            {
                Kind = (ClientQLScalarExpressionKind)Enum.Parse(typeof(ClientQLScalarExpressionKind), token.Value<string>("Kind")),
                Literal = new ClientQLLiteral
                {
                    Kind = (ClientQLLiteralKind)Enum.Parse(typeof(ClientQLLiteralKind), token["Literal"]["Kind"].ToString())
                }
            };

            return literalExpression;
        }

        public ClientQLTupleCreateScalarExpression DeserializeTupleCreateExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLTupleCreateScalarExpression tupleCreateExpression = new ClientQLTupleCreateScalarExpression
            {
                Kind = ClientQLScalarExpressionKind.TupleCreate,
                VecItems = token["Items"].ToObject<List<ClientQLScalarExpression>>(serializer)
            };

            return tupleCreateExpression;
        }

        public ClientQLVariable DeserializeClientQLVariable(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLVariable variable = new ClientQLVariable
            {
                Name = token.Value<string>("Name"),
                UniqueId = token.Value<int>("UniqueId")
            };

            return variable;
        }

        public List<ClientQLObjectProperty> DeserializeObjectProperties(JToken token, Newtonsoft.Json.JsonSerializer serializer)
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

        public ClientQLScalarExpression DeserializeScalarExpression(JToken token, Newtonsoft.Json.JsonSerializer serializer)
        {
            ClientQLScalarExpressionKind scalarExpressionKind = (ClientQLScalarExpressionKind)Enum.Parse(typeof(ClientQLScalarExpressionKind), token.Value<string>("Kind"));

            switch (scalarExpressionKind)
            {
                case ClientQLScalarExpressionKind.ObjectCreate:
                    return this.DeserializeObjectCreateExpression(token, serializer);
                case ClientQLScalarExpressionKind.VariableRef:
                    return this.DeserializeVariableRefExpression(token, serializer);
                case ClientQLScalarExpressionKind.TupleItemRef:
                    return this.DeserializeTupleItemRefExpression(token, serializer);
                case ClientQLScalarExpressionKind.Mux:
                    return this.DeserializeMuxExpression(token, serializer);
                case ClientQLScalarExpressionKind.BinaryOperator:
                    return this.DeserializeBinaryOperatorExpression(token, serializer);
                case ClientQLScalarExpressionKind.Literal:
                    return this.DeserializeLiteralExpression(token, serializer);
                case ClientQLScalarExpressionKind.TupleCreate:
                    return this.DeserializeTupleCreateExpression(token, serializer);
                default:
                    throw new System.Text.Json.JsonException($"Invalid ClientQLScalarExpressionKind: {scalarExpressionKind}");
            }
        }
    }
}