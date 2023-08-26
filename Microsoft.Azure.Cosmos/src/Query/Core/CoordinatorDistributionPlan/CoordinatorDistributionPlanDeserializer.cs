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
        private static class Constants
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
            public const string CoordinatorDistributionPlan = "coordinatorDistributionPlan";
            public const string ClientQL = "clientQL";
            public const string SourceExpression = "SourceExpression";
            public const string VecExpression = "VecExpression";
            public const string VecKeys = "VecKeys";
            public const string VecAggregates = "VecAggregates";
            public const string VecItems = "VecItems";
            public const string DeclaredVariable = "DeclaredVariable";
            public const string EnumerationKind = "EnumerationKind";
            public const string SelectorExpression = "SelectorExpression";
            public const string SkipValue = "SkipValue";
            public const string TakeValue = "TakeValue";
            public const string Delegate = "Delegate";
            public const string Kind = "Kind";
            public const string Index = "Index";
            public const string ArrayKind = "ArrayKind";
            public const string Expression = "Expression";
            public const string OperatorKind = "OperatorKind";
            public const string LeftExpression = "LeftExpression";
            public const string RightExpression = "RightExpression";
            public const string MaxDepth = "MaxDepth";
            public const string DeclaredVariableExpression = "DeclaredVariableExpression";
            public const string ConditionExpression = "ConditionExpression";
            public const string Properties = "Properties";
            public const string PropertyName = "PropertyName";
            public const string FunctionKind = "FunctionKind";
            public const string VecArguments = "VecArguments";
            public const string Identifier = "Identifier";
            public const string Builtin = "Builtin";
            public const string Type = "Type";
            public const string Name = "Name";
            public const string UniqueId = "UniqueId";
            public const string SortOrder = "SortOrder";
            public const string Variable = "Variable";
            public const string ObjectKind = "ObjectKind";
            public const string Items = "Items";
        }

        public static CoordinatorDistributionPlan DeserializeCoordinatorDistributionPlan(string jsonString)
        {
            JObject token = JObject.Parse(jsonString);
            JsonSerializer serializer = new JsonSerializer();

            ClientQLExpression clientQL = DeserializeClientQLEnumerableExpression(token[Constants.CoordinatorDistributionPlan][Constants.ClientQL], serializer);

            return new CoordinatorDistributionPlan(clientQL);
        }

        private static ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            string kind = GetValue<string>(token, Constants.Kind);

            switch (kind)
            {
                case Constants.Aggregate:
                    return DeserializeAggregateEnumerableExpression(token, serializer);
                case Constants.Distinct:
                    return DeserializeDistinctEnumerableExpression(token, serializer);
                case Constants.GroupBy:
                    return DeserializeGroupByEnumerableExpression(token, serializer);
                case Constants.Flatten:
                    return DeserializeFlattenEnumerableExpression(token, serializer);
                case Constants.Input:
                    return DeserializeInputEnumerableExpression(token, serializer);
                case Constants.OrderBy:
                    return DeserializeOrderByEnumerableExpression(token, serializer);
                case Constants.ScalarAsEnumerable:
                    return DeserializeScalarAsEnumerableExpression(token, serializer);
                case Constants.Select:
                    return DeserializeSelectEnumerableExpression(token, serializer);
                case Constants.SelectMany:
                    return DeserializeSelectManyExpression(token, serializer);
                case Constants.Take:
                    return DeserializeTakeEnumerableExpression(token, serializer);
                case Constants.Where:
                    return DeserializeWhereEnumerableExpression(token, serializer);
                default:
                    throw new NotSupportedException($"Unsupported ClientQLExpression kind: {kind}");
            }
        }

        private static ClientQLAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            ClientQLAggregate aggregate = DeserializeAggregate(token[Constants.Aggregate]);

            return new ClientQLAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        private static ClientQLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token[Constants.DeclaredVariable]);
            IReadOnlyList<ClientQLScalarExpression> vecExpressions = DeserializeScalarExpressionArray(token[Constants.VecExpression], serializer);

            return new ClientQLDistinctEnumerableExpression(sourceExpression, declaredVariable, vecExpressions);
        }

        private static ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token["SourceExpression"], serializer);
            IReadOnlyList<ClientQLGroupByKey> vecKeys = GetValue<IReadOnlyList<ClientQLGroupByKey>>(token, Constants.VecKeys);
            IReadOnlyList<ClientQLAggregate> vecAggregates = GetValue<IReadOnlyList<ClientQLAggregate>>(token, Constants.VecAggregates);

            return new ClientQLGroupByEnumerableExpression(sourceExpression, vecKeys, vecAggregates);
        }

        private static ClientQLFlattenEnumerableExpression DeserializeFlattenEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);

            return new ClientQLFlattenEnumerableExpression(sourceExpression);
        }

        private static ClientQLInputEnumerableExpression DeserializeInputEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            return new ClientQLInputEnumerableExpression(GetValue<string>(token, Constants.Name));
        }

        private static ClientQLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression source = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token[Constants.DeclaredVariable]);
            IReadOnlyList<ClientQLOrderByItem> orderByItems = GetValue<IReadOnlyList<ClientQLOrderByItem>>(token, Constants.VecItems);

            return new ClientQLOrderByEnumerableExpression(source, declaredVariable, orderByItems);
        }

        private static ClientQLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);
            if (Enum.TryParse(GetValue<string>(token, Constants.EnumerationKind), out ClientQLEnumerationKind enumerationKind))
            {
                return new ClientQLScalarAsEnumerableExpression(expression, enumerationKind);
            }
            else
            {
                throw new NotSupportedException($"Invalid ScalarAsEnumerable Expression: {enumerationKind}");
            }
        }

        private static ClientQLSelectEnumerableExpression DeserializeSelectEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token[Constants.DeclaredVariable]);
            ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);

            return new ClientQLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        private static ClientQLSelectManyEnumerableExpression DeserializeSelectManyExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token[Constants.DeclaredVariable]);
            ClientQLEnumerableExpression selectorExpression = DeserializeClientQLEnumerableExpression(token[Constants.SelectorExpression], serializer);

            return new ClientQLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        private static ClientQLTakeEnumerableExpression DeserializeTakeEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            int skipValue = GetValue<int>(token, Constants.SkipValue);
            int takeExpression = GetValue<int>(token, Constants.TakeValue);

            return new ClientQLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
        }

        private static ClientQLWhereEnumerableExpression DeserializeWhereEnumerableExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(token[Constants.SourceExpression], serializer);
            ClientQLDelegate clientDelegate = DeserializeDelegateExpression(token[Constants.Delegate]);

            return new ClientQLWhereEnumerableExpression(sourceExpression, clientDelegate);
        }

        private static ClientQLScalarExpression DeserializeScalarExpression(JToken token, JsonSerializer serializer)
        {
            object scalarExpressionKind = (ClientQLScalarExpressionKind)Enum.Parse(typeof(ClientQLScalarExpressionKind), GetValue<string>(token, Constants.Kind));

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
                    throw new NotSupportedException($"Invalid ClientQLScalarExpressionKind: {scalarExpressionKind}");
            }
        }

        private static ClientQLArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLArrayKind arrayKind = GetValue<ClientQLArrayKind>(token, Constants.ArrayKind);
            IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray(token[Constants.VecItems], serializer);

            return new ClientQLArrayCreateScalarExpression(arrayKind, vecItems);
        }

        private static ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);
            int index = GetValue<int>(token, Constants.Index);
            
            return new ClientQLArrayIndexerScalarExpression(expression, index);
        }

        private static ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.OperatorKind), out ClientQLBinaryScalarOperatorKind operatorKind))
            {
                int maxDepth = GetValue<int>(token, Constants.MaxDepth);
                ClientQLScalarExpression leftExpression = DeserializeScalarExpression(token[Constants.LeftExpression], serializer);
                ClientQLScalarExpression rightExpression = DeserializeScalarExpression(token[Constants.RightExpression], serializer);

                return new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
            }
            else
            {
                throw new NotSupportedException($"Invalid Binary Operator Expression: {operatorKind}");
            }
        }

        private static ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(JToken token, JsonSerializer serializer)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.OperatorKind), out ClientQLIsOperatorKind operatorKind))
            {
                ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);
                return new ClientQLIsOperatorScalarExpression(operatorKind, expression);
            }
            else
            {
                throw new NotSupportedException($"Invalid Operator Scalar Expression: {operatorKind}");
            }
        }

        private static ClientQLLetScalarExpression DeserializeLetScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(token[Constants.DeclaredVariable]);
            ClientQLScalarExpression declaredVariableExpression = DeserializeScalarExpression(token[Constants.DeclaredVariableExpression], serializer);
            ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);

            return new ClientQLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        private static ClientQLLiteralScalarExpression DeserializeLiteralScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLLiteral literal = new ClientQLLiteral((ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal);

            return new ClientQLLiteralScalarExpression(literal);
            
        }

        private static ClientQLMuxScalarExpression DeserializeMuxScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression conditionExpression = DeserializeScalarExpression(token[Constants.ConditionExpression], serializer);
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(token[Constants.LeftExpression], serializer);
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(token[Constants.RightExpression], serializer);

            return new ClientQLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        private static ClientQLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            string objectKindString = GetValue<string>(token, Constants.ObjectKind);
            if (!Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind))
            {
                throw new NotSupportedException($"Invalid ClientQLObjectKind: {objectKindString}");
            }

            ValidateArrayProperty(token, "Properties");
            IReadOnlyList<ClientQLObjectProperty> properties = DeserializeObjectProperties(token[Constants.Properties], serializer);

            return new ClientQLObjectCreateScalarExpression(properties, objectKind);
        }

        private static ClientQLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);
            string propertyName = GetValue<string>(token, Constants.PropertyName);

            return new ClientQLPropertyRefScalarExpression(expression, propertyName);
        }

        private static ClientQLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.FunctionKind), out ClientQLBuiltinScalarFunctionKind functionKind))
            {
                IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray(token[Constants.VecArguments], serializer);
                return new ClientQLSystemFunctionCallScalarExpression(functionKind, vecArguments);
            }
            else
            {
                throw new NotSupportedException($"Invalid System Function Call Expression: {functionKind}");
            }
        }

        private static ClientQLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(JToken token, JsonSerializer serializer)
        {
            IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray(token[Constants.Items], serializer);

            return new ClientQLTupleCreateScalarExpression(vecItems);
        }

        private static ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);
            int index = GetValue<int>(token, Constants.Index);

            return new ClientQLTupleItemRefScalarExpression(expression, index);
        }

        private static ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(JToken token, JsonSerializer serializer)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.OperatorKind), out ClientQLUnaryScalarOperatorKind operatorKind))
            {
                ClientQLScalarExpression expression = DeserializeScalarExpression(token[Constants.Expression], serializer);
                return new ClientQLUnaryScalarExpression(operatorKind, expression);
            }
            else
            {
                throw new NotSupportedException($"Invalid Unary Scalar Expression: {operatorKind}");
            }
        }

        private static ClientQLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLFunctionIdentifier identifier = token[Constants.Identifier].ToObject<ClientQLFunctionIdentifier>(serializer);

            ValidateArrayProperty(token, Constants.VecArguments);
            IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray(token[Constants.VecArguments], serializer);
            bool builtin = GetValue<bool>(token, Constants.Builtin);

            return new ClientQLUserDefinedFunctionCallScalarExpression(identifier, vecArguments, builtin);
        }

        private static ClientQLVariableRefScalarExpression DeserializeVariableRefScalarExpression(JToken token, JsonSerializer serializer)
        {
            ClientQLVariable variable = DeserializeClientQLVariable(token[Constants.Variable]);

            return new ClientQLVariableRefScalarExpression(variable);
        }

        private static ClientQLDelegate DeserializeDelegateExpression(JToken token)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.Kind), out ClientQLDelegateKind kind))
            {
                ClientQLType type = DeserializeType(token[Constants.Type]);
                return new ClientQLDelegate(kind, type);
            }
            else
            {
                throw new NotSupportedException($"Invalid Delegate Expression: {kind}");
            }
        }

        private static ClientQLType DeserializeType(JToken token)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.Kind), out ClientQLTypeKind kind))
            {
                return new ClientQLType(kind);
            }
            else
            {
                throw new NotSupportedException($"Invalid Type Expression: {kind}");
            }
        }

        private static ClientQLAggregate DeserializeAggregate(JToken token)
        {
            if (Enum.TryParse(GetValue<string>(token, Constants.Kind), out ClientQLAggregateKind kind))
            {
                string operatorKind = null;
                if (token[Constants.OperatorKind] != null)
                {
                    operatorKind = token[Constants.OperatorKind].ToString();
                }

                return new ClientQLAggregate(kind, operatorKind);
            }
            else
            {
                throw new NotSupportedException($"Invalid Aggregate Expression: {kind}");
            }
        }

        private static ClientQLVariable DeserializeClientQLVariable(JToken token)
        {
            string name = GetValue<string>(token, Constants.Name);
            int uniqueId = GetValue<int>(token, Constants.UniqueId);

            return new ClientQLVariable(name, uniqueId);
        }

        private static List<ClientQLObjectProperty> DeserializeObjectProperties(JToken token, JsonSerializer serializer)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            foreach (JToken propertyToken in token)
            {
                string name = GetValue<string>(propertyToken, Constants.Name);
                ClientQLScalarExpression expression = DeserializeScalarExpression(propertyToken[Constants.Expression], serializer);
                properties.Add(new ClientQLObjectProperty(name, expression));
            }

            return properties;
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

        private static T GetValue<T>(JToken token, string expression)
        {
            try
            {
                return token.Value<T>(expression);
            }
            catch (Exception ex)
            {
                string errorMessage = GetExceptionMessageFormat();
                errorMessage += ex.InnerException;

                throw new ArgumentNullException(errorMessage);
            }
        }

        private static void ValidateArrayProperty(JToken token, string property)
        {
            string errorMessage;
            if (token[property] == null)
            {
                string nullErrorMessage = $"{property} could not be found in the deserialized plan.";
                errorMessage = GetExceptionMessageFormat();
                throw new ArgumentNullException(errorMessage + nullErrorMessage);
            }
            
            if (token[property] is not JArray)
            {
                string arrayErrorMessage = $"{property} is not of type array when it should be.";
                errorMessage = GetExceptionMessageFormat();
                throw new ArgumentException(errorMessage + arrayErrorMessage);
            }
        }

        private static string GetExceptionMessageFormat()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            string clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
            
            return $"Exception occurred while deserializing query plan. Version : '{clientSDKVersion}', Exception/Reason : '{1}'.";
        }
    }
}