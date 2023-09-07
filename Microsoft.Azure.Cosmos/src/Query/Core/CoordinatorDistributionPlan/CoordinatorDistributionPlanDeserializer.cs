//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using ClientQL;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal static class CoordinatorDistributionPlanDeserializer
    {
        private static class Constants
        {
            public const string Aggregate = "Aggregate";
            public const string Aggregates = "Aggregates";
            public const string Distinct = "Distinct";
            public const string GroupBy = "GroupBy";
            public const string Flatten = "Flatten";
            public const string Input = "Input";
            public const string OrderBy = "OrderBy";
            public const string ScalarAsEnumerable = "ScalarAsEnumerable";
            public const string Select = "Select";
            public const string SelectMany = "SelectMany";
            public const string Arguments = "Arguments";
            public const string Take = "Take";
            public const string Where = "Where";
            public const string Tuple = "Tuple";
            public const string CoordinatorDistributionPlan = "coordinatorDistributionPlan";
            public const string ClientQL = "clientQL";
            public const string SourceExpression = "SourceExpression";
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
            public const string Identifier = "Identifier";
            public const string Builtin = "Builtin";
            public const string Type = "Type";
            public const string KeyCount = "KeyCount";
            public const string Name = "Name";
            public const string UniqueId = "UniqueId";
            public const string SortOrder = "SortOrder";
            public const string Variable = "Variable";
            public const string ObjectKind = "ObjectKind";
            public const string Items = "Items";
        }

        public static CoordinatorDistributionPlan DeserializeCoordinatorDistributionPlan(string jsonString)
        {
            CosmosObject cosmosObject = CosmosObject.Parse(jsonString);
            CosmosElement coordinatorDistributionPlanElement = GetValue<CosmosElement>(cosmosObject, Constants.CoordinatorDistributionPlan);
            CosmosElement clientQLElement = GetValue<CosmosElement>((CosmosObject)coordinatorDistributionPlanElement, Constants.ClientQL);
            ClientQLEnumerableExpression expression = DeserializeClientQLEnumerableExpression((CosmosObject)clientQLElement);

            return new CoordinatorDistributionPlan(expression);
        }

        private static ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(CosmosObject cosmosObject)
        {
            string kindProperty = GetValue<CosmosString>(cosmosObject, Constants.Kind).Value;
            switch (kindProperty)
            {
                case Constants.Aggregate:
                    return DeserializeAggregateEnumerableExpression(cosmosObject);
                case Constants.Distinct:
                    return DeserializeDistinctEnumerableExpression(cosmosObject);
                case Constants.GroupBy:
                    return DeserializeGroupByEnumerableExpression(cosmosObject);
                case Constants.Flatten:
                    return DeserializeFlattenEnumerableExpression(cosmosObject);
                case Constants.Input:
                    return DeserializeInputEnumerableExpression(cosmosObject);
                case Constants.OrderBy:
                    return DeserializeOrderByEnumerableExpression(cosmosObject);
                case Constants.ScalarAsEnumerable:
                    return DeserializeScalarAsEnumerableExpression(cosmosObject);
                case Constants.Select:
                    return DeserializeSelectEnumerableExpression(cosmosObject);
                case Constants.SelectMany:
                    return DeserializeSelectManyExpression(cosmosObject);
                case Constants.Take:
                    return DeserializeTakeEnumerableExpression(cosmosObject);
                case Constants.Where:
                    return DeserializeWhereEnumerableExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid ClientQLExpression kind: {kindProperty}");
            }
        }
        
        private static ClientQLAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLAggregate aggregate = DeserializeAggregate(GetValue<CosmosObject>(cosmosObject, Constants.Aggregate));
            return new ClientQLAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        private static ClientQLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<ClientQLScalarExpression> expressions = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Expression));
            return new ClientQLDistinctEnumerableExpression(sourceExpression, declaredVariable, expressions);
        }

        private static ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            int keyCount = (int)Number64.ToDouble(GetValue<CosmosNumber>(cosmosObject, Constants.KeyCount).Value);
            IReadOnlyList<ClientQLAggregate> aggregates = DeserializeAggregateArray(GetValue<CosmosArray>(cosmosObject, Constants.Aggregates));
            return new ClientQLGroupByEnumerableExpression(sourceExpression, keyCount, aggregates);
        }

        private static ClientQLFlattenEnumerableExpression DeserializeFlattenEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            return new ClientQLFlattenEnumerableExpression(sourceExpression);
        }

        private static ClientQLInputEnumerableExpression DeserializeInputEnumerableExpression(CosmosObject cosmosObject)
        {
            return new ClientQLInputEnumerableExpression(GetValue<CosmosString>(cosmosObject, Constants.Name).Value);
        }

        private static ClientQLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<ClientQLOrderByItem> orderByItems = DeserializeOrderByItemArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new ClientQLOrderByEnumerableExpression(sourceExpression, declaredVariable, orderByItems);
        }

        private static ClientQLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            GetEnumValue<ClientQLEnumerationKind>(GetValue<CosmosString>(cosmosObject, Constants.EnumerationKind).Value, out ClientQLEnumerationKind enumerationKind);
            return new ClientQLScalarAsEnumerableExpression(expression, enumerationKind);
        }

        private static ClientQLSelectEnumerableExpression DeserializeSelectEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        private static ClientQLSelectManyEnumerableExpression DeserializeSelectManyExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            ClientQLEnumerableExpression selectorExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SelectorExpression));
            return new ClientQLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        private static ClientQLTakeEnumerableExpression DeserializeTakeEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            int skipValue = (int)Number64.ToDouble(GetValue<CosmosNumber>(cosmosObject, Constants.SkipValue).Value);
            int takeExpression = (int)Number64.ToDouble(GetValue<CosmosNumber>(cosmosObject, Constants.TakeValue).Value);
            return new ClientQLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
        }

        private static ClientQLWhereEnumerableExpression DeserializeWhereEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLDelegate clientDelegate = DeserializeDelegateExpression(GetValue<CosmosObject>(cosmosObject, Constants.Delegate));
            return new ClientQLWhereEnumerableExpression(sourceExpression, clientDelegate);
        }

        private static ClientQLScalarExpression DeserializeScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLScalarExpressionKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value, out ClientQLScalarExpressionKind scalarExpressionKind);
            switch (scalarExpressionKind)
            {
                case ClientQLScalarExpressionKind.ArrayCreate:
                    return DeserializeArrayCreateScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.ArrayIndexer:
                    return DeserializeArrayIndexerScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.BinaryOperator:
                    return DeserializeBinaryOperatorScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.IsOperator:
                    return DeserializeIsOperatorScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.Let:
                    return DeserializeLetScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.Literal:
                    return DeserializeLiteralScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.Mux:
                    return DeserializeMuxScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.ObjectCreate:
                    return DeserializeObjectCreateScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.PropertyRef:
                    return DeserializePropertyRefScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.SystemFunctionCall:
                    return DeserializeSystemFunctionCallScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.TupleCreate:
                    return DeserializeTupleCreateScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.TupleItemRef:
                    return DeserializeTupleItemRefScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.UnaryOperator:
                    return DeserializeUnaryScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.UserDefinedFunctionCall:
                    return DeserializeUserDefinedFunctionCallScalarExpression(cosmosObject);
                case ClientQLScalarExpressionKind.VariableRef:
                    return DeserializeVariableRefScalarExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid ClientQLExpression kind: {scalarExpressionKind}");
            }
        }

        private static ClientQLArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLArrayKind>(GetValue<CosmosString>(cosmosObject, Constants.ArrayKind).Value, out ClientQLArrayKind arrayKind);
            IReadOnlyList<ClientQLScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new ClientQLArrayCreateScalarExpression(arrayKind, items);
        }

        private static ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            int index = (int)Number64.ToDouble(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new ClientQLArrayIndexerScalarExpression(expression, index);
        }

        private static ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLBinaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value, out ClientQLBinaryScalarOperatorKind operatorKind);
            bool success = TryGetValue<CosmosNumber>(cosmosObject, Constants.MaxDepth, out CosmosNumber cosmosNumber);
            int maxDepth = success ? (int)Number64.ToDouble(cosmosNumber.Value) : default;
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
        }

        private static ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLIsOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value, out ClientQLIsOperatorKind operatorKind);
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLIsOperatorScalarExpression(operatorKind, expression);
        }

        private static ClientQLLetScalarExpression DeserializeLetScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            ClientQLScalarExpression declaredVariableExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariableExpression));
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        private static ClientQLLiteralScalarExpression DeserializeLiteralScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLLiteral literal = new ClientQLLiteral((ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal);
            return new ClientQLLiteralScalarExpression(literal);
        }

        private static ClientQLMuxScalarExpression DeserializeMuxScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression conditionExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.ConditionExpression));
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new ClientQLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        private static ClientQLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLObjectKind>(GetValue<CosmosString>(cosmosObject, Constants.ObjectKind).Value, out ClientQLObjectKind objectKind);
            IReadOnlyList<ClientQLObjectProperty> properties = DeserializeObjectProperties(GetValue<CosmosArray>(cosmosObject, Constants.Properties));
            return new ClientQLObjectCreateScalarExpression(properties, objectKind);
        }

        private static ClientQLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            string propertyName = GetValue<CosmosString>(cosmosObject, Constants.PropertyName).Value;
            return new ClientQLPropertyRefScalarExpression(expression, propertyName);
        }

        private static ClientQLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLBuiltinScalarFunctionKind>(GetValue<CosmosString>(cosmosObject, Constants.FunctionKind).Value, out ClientQLBuiltinScalarFunctionKind functionKind);
            IReadOnlyList<ClientQLScalarExpression> arguments = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Arguments));
            return new ClientQLSystemFunctionCallScalarExpression(functionKind, arguments);
        }

        private static ClientQLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(CosmosObject cosmosObject)
        {
            IReadOnlyList<ClientQLScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new ClientQLTupleCreateScalarExpression(items);
        }

        private static ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            int index = (int)Number64.ToDouble(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new ClientQLTupleItemRefScalarExpression(expression, index);
        }

        private static ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLUnaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value, out ClientQLUnaryScalarOperatorKind operatorKind);
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLUnaryScalarExpression(operatorKind, expression);
        }

        private static ClientQLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            string identifierString = GetValue<CosmosString>(cosmosObject, Constants.Identifier).Value;
            ClientQLFunctionIdentifier functionIdentifier = new ClientQLFunctionIdentifier(identifierString);
            IReadOnlyList<ClientQLScalarExpression> arguments = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Arguments));
            bool builtin = GetValue<CosmosBoolean>(cosmosObject, Constants.Builtin).Value;
            return new ClientQLUserDefinedFunctionCallScalarExpression(functionIdentifier, arguments, builtin);
        }

        private static ClientQLVariableRefScalarExpression DeserializeVariableRefScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLVariable variable = DeserializeClientQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.Variable));
            return new ClientQLVariableRefScalarExpression(variable);
        }

        private static ClientQLDelegate DeserializeDelegateExpression(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLDelegateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value, out ClientQLDelegateKind kind);
            ClientQLType type = DeserializeType(GetValue<CosmosObject>(cosmosObject, Constants.Type));
            return new ClientQLDelegate(kind, type);
        }

        private static ClientQLType DeserializeType(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLTypeKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value, out ClientQLTypeKind kind);
            return new ClientQLType(kind);
        }

        private static ClientQLAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            GetEnumValue<ClientQLAggregateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value, out ClientQLAggregateKind kind);
            string operatorKind = null;
            if (cosmosObject[Constants.OperatorKind] != null)
            {
                operatorKind = GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value;
            }

            return new ClientQLAggregate(kind, operatorKind);
        }

        private static ClientQLVariable DeserializeClientQLVariable(CosmosObject cosmosObject)
        {
            bool success = TryGetValue<CosmosString>(cosmosObject, Constants.Name, out CosmosString cosmosString);
            string name = success ? cosmosString.Value : default;

            success = TryGetValue<CosmosNumber>(cosmosObject, Constants.UniqueId, out CosmosNumber cosmosNumber);
            int uniqueId = success ? (int)Number64.ToDouble(cosmosNumber.Value) : default;

            return new ClientQLVariable(name, uniqueId);
        }

        private static List<ClientQLObjectProperty> DeserializeObjectProperties(CosmosArray cosmosArray)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = propertyElement as CosmosObject;
                string objectPropertyName = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(propertyObject, Constants.Expression)); 
                properties.Add(new ClientQLObjectProperty(objectPropertyName, expression));
            }

            return properties;
        }

        private static ClientQLTupleAggregate DeserializeTupleAggregateExpression(CosmosObject cosmosObject)
        {
            string tupleAggregateKind = GetValue<CosmosString>(cosmosObject, Constants.Kind).Value;
            List<ClientQLAggregate> expression = new List<ClientQLAggregate>();
            foreach (CosmosElement propertyElement in GetValue<CosmosArray>(cosmosObject, Constants.Items))
            {
                ClientQLAggregate aggregateExpression = DeserializeAggregate((CosmosObject)propertyElement);
                expression.Add(aggregateExpression);
            }

            return new ClientQLTupleAggregate(tupleAggregateKind, expression);
        }

        private static List<ClientQLScalarExpression> DeserializeScalarExpressionArray(CosmosArray cosmosArray)
        {
            List<ClientQLScalarExpression> expressions = new List<ClientQLScalarExpression>();
            if (cosmosArray != null)
            {
                foreach (CosmosElement propertyElement in cosmosArray)
                {
                    ClientQLScalarExpression expression = DeserializeScalarExpression((CosmosObject)propertyElement);
                    expressions.Add(expression);
                }
            }
            return expressions;
        }

        private static List<ClientQLOrderByItem> DeserializeOrderByItemArray(CosmosArray cosmosArray)
        {
            List<ClientQLOrderByItem> expressions = new List<ClientQLOrderByItem>();
            if (cosmosArray != null)
            {
                foreach (CosmosElement propertyElement in cosmosArray)
                {
                    CosmosObject propertyObject = (CosmosObject)propertyElement;
                    GetEnumValue<ClientQLScalarExpressionKind>(GetValue<CosmosString>(propertyObject, Constants.Kind).Value, out ClientQLScalarExpressionKind kind);
                    ClientQLScalarExpression scalarExpression = new ClientQLScalarExpression(kind);
                    expressions.Add(new ClientQLOrderByItem(scalarExpression, ClientQLSortOrder.Ascending));
                }
            }

            return expressions;
        }

        private static List<ClientQLAggregate> DeserializeAggregateArray(CosmosArray cosmosArray)
        {
            List<ClientQLAggregate> expressions = new List<ClientQLAggregate>();
            if (cosmosArray != null)
            {
                foreach (CosmosElement propertyElement in cosmosArray)
                {
                    ClientQLAggregate aggregateExpression = null;
                    CosmosObject propertyObject = (CosmosObject)propertyElement;
                    string kindProperty = GetValue<CosmosString>(propertyObject, Constants.Kind).Value;

                    if (kindProperty.Equals(Constants.Builtin))
                    {
                        aggregateExpression = DeserializeAggregate((CosmosObject)propertyElement);
                    }
                    else if (kindProperty.Equals(Constants.Tuple))
                    {
                        aggregateExpression = DeserializeTupleAggregateExpression((CosmosObject)propertyElement);
                    }
                    
                    expressions.Add(aggregateExpression);
                }
            }

            return expressions;
        }

        private static T GetValue<T>(CosmosObject cosmosObject, string propertyName)
            where T : CosmosElement
        {
            bool found = TryGetValue(cosmosObject, propertyName, out T value);

            if (!found)
            {
                throw new InvalidOperationException($"{GetExceptionMessage()}. The required property {propertyName} was not found in {cosmosObject}");
            }

            return value;
        }

        private static bool TryGetValue<T>(CosmosObject cosmosObject, string propertyName, out T result)
            where T : CosmosElement
        {
            bool found = cosmosObject.TryGetValue(propertyName, out CosmosElement value);

            if (found)
            {
                result = value as T;
                if (result == null)
                {
                    throw new InvalidOperationException($"{GetExceptionMessage()}. The required property {propertyName} was not found in {cosmosObject}");
                }

                return found;
            }

            result = null;
            return found;
        }

        private static TEnum GetEnumValue<TEnum>(string propertyName, out TEnum result)
            where TEnum : struct
        {
            bool success = Enum.TryParse(propertyName, out TEnum enumValue);
            if (!success) 
            {
                throw new InvalidOperationException($"{GetExceptionMessage()}. The string representation of this {propertyName} enumerated constant was not able to be converted to an equivalent enumerated object");
            }

            result = enumValue;
            return result;
        }

        private static string GetExceptionMessage()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            string clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
            
            return $"Exception occurred while deserializing query plan. Version : '{clientSDKVersion}', Exception/Reason : '{1}'.";
        }
    }
}