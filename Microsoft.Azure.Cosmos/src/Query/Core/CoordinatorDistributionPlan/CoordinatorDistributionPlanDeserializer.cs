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
            public const string Arguments = "Arguments";
            public const string ArrayKind = "ArrayKind";
            public const string Aggregate = "Aggregate";
            public const string Aggregates = "Aggregates";
            public const string Builtin = "Builtin";
            public const string ClientQL = "clientQL";
            public const string ConditionExpression = "ConditionExpression";
            public const string CoordinatorDistributionPlan = "coordinatorDistributionPlan";
            public const string DeclaredVariable = "DeclaredVariable";
            public const string DeclaredVariableExpression = "DeclaredVariableExpression";
            public const string Delegate = "Delegate";
            public const string Distinct = "Distinct";
            public const string EnumerationKind = "EnumerationKind";
            public const string Expression = "Expression";
            public const string Flatten = "Flatten";
            public const string FunctionKind = "FunctionKind";
            public const string GroupBy = "GroupBy";
            public const string Identifier = "Identifier";
            public const string Index = "Index";
            public const string Input = "Input";
            public const string Items = "Items";
            public const string KeyCount = "KeyCount";
            public const string Kind = "Kind";
            public const string LeftExpression = "LeftExpression";
            public const string MaxDepth = "MaxDepth";
            public const string Name = "Name";
            public const string ObjectKind = "ObjectKind";
            public const string OperatorKind = "OperatorKind";
            public const string OrderBy = "OrderBy";
            public const string Properties = "Properties";
            public const string PropertyName = "PropertyName";
            public const string RightExpression = "RightExpression";
            public const string ScalarAsEnumerable = "ScalarAsEnumerable";
            public const string Select = "Select";
            public const string SelectorExpression = "SelectorExpression";
            public const string SelectMany = "SelectMany";
            public const string SkipValue = "SkipValue";
            public const string SortOrder = "SortOrder";
            public const string SourceExpression = "SourceExpression";
            public const string Take = "Take";
            public const string TakeValue = "TakeValue";
            public const string Tuple = "Tuple";
            public const string Type = "Type";
            public const string UniqueId = "UniqueId";
            public const string Value = "Value";
            public const string Variable = "Variable";
            public const string Where = "Where";
        }

        public static CoordinatorDistributionPlan DeserializeCoordinatorDistributionPlan(string jsonString)
        {
            CosmosObject cosmosObject = CosmosObject.Parse(jsonString);
            CosmosObject coordinatorDistributionPlanElement = GetValue<CosmosObject>(cosmosObject, Constants.CoordinatorDistributionPlan);
            CosmosObject clientQLElement = GetValue<CosmosObject>(coordinatorDistributionPlanElement, Constants.ClientQL);
            ClientQLEnumerableExpression expression = DeserializeClientQLEnumerableExpression(clientQLElement);

            return new CoordinatorDistributionPlan(expression);
        }

        private static ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(CosmosObject cosmosObject)
        {
            CosmosString kindProperty = GetValue<CosmosString>(cosmosObject, Constants.Kind);
            switch (kindProperty.Value)
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
                    throw new NotSupportedException($"Invalid ClientQLExpression kind: {kindProperty.Value}");
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
            long keyCount = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.KeyCount).Value);
            IReadOnlyList<ClientQLAggregate> aggregates = DeserializeAggregateArray(GetValue<CosmosArray>(cosmosObject, Constants.Aggregates));
            return new ClientQLGroupByEnumerableExpression(sourceExpression, Convert.ToUInt64(keyCount), aggregates);
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
            ClientQLEnumerationKind enumerationKind = GetEnumValue<ClientQLEnumerationKind>(GetValue<CosmosString>(cosmosObject, Constants.EnumerationKind).Value);
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
            long skipValue = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.SkipValue).Value);
            long takeExpression = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.TakeValue).Value);
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
            ClientQLScalarExpressionKind scalarExpressionKind = GetEnumValue<ClientQLScalarExpressionKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
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
            ClientQLArrayKind arrayKind = GetEnumValue<ClientQLArrayKind>(GetValue<CosmosString>(cosmosObject, Constants.ArrayKind).Value);
            IReadOnlyList<ClientQLScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new ClientQLArrayCreateScalarExpression(arrayKind, items);
        }

        private static ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            long index = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new ClientQLArrayIndexerScalarExpression(expression, index);
        }

        private static ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLBinaryScalarOperatorKind operatorKind = GetEnumValue<ClientQLBinaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            bool success = TryGetValue<CosmosNumber>(cosmosObject, Constants.MaxDepth, out CosmosNumber cosmosNumber);
            long? maxDepth = success ? Number64.ToLong(cosmosNumber.Value) : null;
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
        }

        private static ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLIsOperatorKind operatorKind = GetEnumValue<ClientQLIsOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
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
            CosmosObject literalObject = GetValue<CosmosObject>(cosmosObject, "Literal");
            ClientQLLiteralKind literalKind = GetEnumValue<ClientQLLiteralKind>(GetValue<CosmosString>(literalObject, Constants.Kind).Value);
            switch (literalKind)
            {
                case ClientQLLiteralKind.Undefined:
                    ClientQLUndefinedLiteral undefinedLiteral = new ClientQLUndefinedLiteral();
                    return new ClientQLLiteralScalarExpression(undefinedLiteral);
                case ClientQLLiteralKind.Array:
                    IReadOnlyList<ClientQLLiteral> literalExpressions = DeserializeLiteralArray(GetValue<CosmosArray>(literalObject, Constants.Items));
                    return new ClientQLLiteralScalarExpression(new ClientQLArrayLiteral(literalExpressions));
                case ClientQLLiteralKind.Binary:
                    IReadOnlyList<BinaryData> binaryExpressions = DeserializeBinaryArray(GetValue<CosmosArray>(literalObject, Constants.Value));
                    return new ClientQLLiteralScalarExpression(new ClientQLBinaryLiteral(binaryExpressions));
                case ClientQLLiteralKind.Boolean:
                    ClientQLBooleanLiteral booleanLiteral = new ClientQLBooleanLiteral(GetValue<CosmosBoolean>(literalObject, Constants.Value).Value);
                    return new ClientQLLiteralScalarExpression(booleanLiteral);
                case ClientQLLiteralKind.CGuid:
                    ClientQLCGuidLiteral cGuidLiteral = new ClientQLCGuidLiteral(GetValue<CosmosGuid>(literalObject, Constants.Value).Value);
                    return new ClientQLLiteralScalarExpression(cGuidLiteral);
                case ClientQLLiteralKind.CNumber:
                    ClientQLCNumberLiteral cNumberLiteral = new ClientQLCNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(literalObject, Constants.Value).Value));
                    return new ClientQLLiteralScalarExpression(cNumberLiteral);
                case ClientQLLiteralKind.MDateTime:
                    ClientQLMDateTimeLiteral mDateTimeLiteral = new ClientQLMDateTimeLiteral(Number64.ToLong(GetValue<CosmosNumber>(literalObject, Constants.Value).Value));
                    return new ClientQLLiteralScalarExpression(mDateTimeLiteral);
                case ClientQLLiteralKind.MJavaScript:
                    ClientQLMJavaScriptLiteral mJavaScriptLiteral = new ClientQLMJavaScriptLiteral(GetValue<CosmosString>(literalObject, Constants.Name).Value);
                    return new ClientQLLiteralScalarExpression(mJavaScriptLiteral);
                case ClientQLLiteralKind.MNumber:
                    ClientQLMNumberLiteral mNumberLiteral = new ClientQLMNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(literalObject, Constants.Value).Value));
                    return new ClientQLLiteralScalarExpression(mNumberLiteral);
                case ClientQLLiteralKind.MRegex:
                    string pattern = GetValue<CosmosString>(literalObject, "Pattern").Value;
                    string options = GetValue<CosmosString>(literalObject, "Options").Value;
                    ClientQLMRegexLiteral mRegexLiteral = new ClientQLMRegexLiteral(pattern, options);
                    return new ClientQLLiteralScalarExpression(mRegexLiteral);
                case ClientQLLiteralKind.MSingleton:
                    ClientQLMSingletonLiteral.Kind functionKind = GetEnumValue<ClientQLMSingletonLiteral.Kind>(GetValue<CosmosString>(literalObject, "SingletonKind").Value);
                    return new ClientQLLiteralScalarExpression(new ClientQLMSingletonLiteral(functionKind));
                case ClientQLLiteralKind.MSymbol:
                    ClientQLMSymbolLiteral mSymbolLiteral = new ClientQLMSymbolLiteral(GetValue<CosmosString>(literalObject, Constants.Value).Value);
                    return new ClientQLLiteralScalarExpression(mSymbolLiteral);
                case ClientQLLiteralKind.Null:
                    ClientQLNullLiteral nullLiteral = new ClientQLNullLiteral();
                    return new ClientQLLiteralScalarExpression(nullLiteral);
                case ClientQLLiteralKind.Number:
                    ClientQLNumberLiteral numberLiteral = new ClientQLNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(literalObject, Constants.Value).Value));
                    return new ClientQLLiteralScalarExpression(numberLiteral);
                case ClientQLLiteralKind.Object:
                    IReadOnlyList<ClientQLObjectLiteralProperty> properties = DeserializeObjectLiteralArray(GetValue<CosmosArray>(literalObject, Constants.Properties));
                    return new ClientQLLiteralScalarExpression(new ClientQLObjectLiteral(properties));
                case ClientQLLiteralKind.String:
                    ClientQLStringLiteral stringLiteral = new ClientQLStringLiteral(GetValue<CosmosString>(literalObject, Constants.Value).Value);
                    return new ClientQLLiteralScalarExpression(stringLiteral);
                default:
                    throw new NotSupportedException($"Invalid ClientQLExpression kind: {literalKind}");
            }
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
            ClientQLObjectKind objectKind = GetEnumValue<ClientQLObjectKind>(GetValue<CosmosString>(cosmosObject, Constants.ObjectKind).Value);
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
            ClientQLBuiltinScalarFunctionKind functionKind = GetEnumValue<ClientQLBuiltinScalarFunctionKind>(GetValue<CosmosString>(cosmosObject, Constants.FunctionKind).Value);
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
            long index = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new ClientQLTupleItemRefScalarExpression(expression, index);
        }

        private static ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLUnaryScalarOperatorKind operatorKind = GetEnumValue<ClientQLUnaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
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
            ClientQLDelegateKind kind = GetEnumValue<ClientQLDelegateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            ClientQLType type = DeserializeType(GetValue<CosmosObject>(cosmosObject, Constants.Type));
            return new ClientQLDelegate(kind, type);
        }

        private static ClientQLType DeserializeType(CosmosObject cosmosObject)
        {
            ClientQLTypeKind kind = GetEnumValue<ClientQLTypeKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            return new ClientQLType(kind);
        }

        private static ClientQLAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            ClientQLAggregateKind kind = GetEnumValue<ClientQLAggregateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            string operatorKind = GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value;

            return new ClientQLAggregate(kind, operatorKind);
        }

        private static ClientQLVariable DeserializeClientQLVariable(CosmosObject cosmosObject)
        {
            string name = GetValue<CosmosString>(cosmosObject, Constants.Name).Value;
            long uniqueId = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.UniqueId).Value);

            return new ClientQLVariable(name, uniqueId);
        }

        private static IReadOnlyList<ClientQLObjectProperty> DeserializeObjectProperties(CosmosArray cosmosArray)
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

        private static IReadOnlyList<ClientQLScalarExpression> DeserializeScalarExpressionArray(CosmosArray cosmosArray)
        {
            List<ClientQLScalarExpression> expressions = new List<ClientQLScalarExpression>();
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                ClientQLScalarExpression expression = DeserializeScalarExpression((CosmosObject)propertyElement);
                expressions.Add(expression);
            }

            return expressions;
        }

        private static IReadOnlyList<ClientQLOrderByItem> DeserializeOrderByItemArray(CosmosArray cosmosArray)
        {
            List<ClientQLOrderByItem> expressions = new List<ClientQLOrderByItem>();
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = (CosmosObject)propertyElement;
                ClientQLScalarExpressionKind kind = GetEnumValue<ClientQLScalarExpressionKind>(GetValue<CosmosString>(propertyObject, Constants.Kind).Value);
                ClientQLScalarExpression scalarExpression = new ClientQLScalarExpression(kind);
                expressions.Add(new ClientQLOrderByItem(scalarExpression, ClientQLSortOrder.Ascending));
            }

            return expressions;
        }

        private static IReadOnlyList<ClientQLAggregate> DeserializeAggregateArray(CosmosArray cosmosArray)
        {
            List<ClientQLAggregate> expressions = new List<ClientQLAggregate>();
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

            return expressions;
        }

        private static IReadOnlyList<ClientQLLiteral> DeserializeLiteralArray(CosmosArray cosmosArray)
        {
            List<ClientQLLiteral> expressions = new List<ClientQLLiteral>();
            foreach (CosmosElement propertyElement in cosmosArray)
            { 
                ClientQLLiteralKind kindProperty = GetEnumValue<ClientQLLiteralKind>(GetValue<CosmosString>((CosmosObject)propertyElement, Constants.Kind).Value);
                ClientQLLiteral literal = new ClientQLLiteral(kindProperty);
                expressions.Add(literal);
            }
        
            return expressions;
        }

        private static IReadOnlyList<ClientQLObjectLiteralProperty> DeserializeObjectLiteralArray(CosmosArray cosmosArray)
        {
            List<ClientQLObjectLiteralProperty> expressions = new List<ClientQLObjectLiteralProperty>();
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                string name = GetValue<CosmosString>((CosmosObject)propertyElement, Constants.Name).Value;
                ClientQLLiteralKind kindProperty = GetEnumValue<ClientQLLiteralKind>(GetValue<CosmosString>((CosmosObject)propertyElement, Constants.Kind).Value);
                ClientQLLiteral literal = new ClientQLLiteral(kindProperty);
                ClientQLObjectLiteralProperty objectLiteralProperty = new ClientQLObjectLiteralProperty(name, literal);
                expressions.Add(objectLiteralProperty);
            }

            return expressions;
        }

        private static IReadOnlyList<BinaryData> DeserializeBinaryArray(CosmosArray cosmosArray)
        {
            List<BinaryData> expressions = new List<BinaryData>();
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                ReadOnlyMemory<byte> binaryValue = GetValue<CosmosBinary>((CosmosObject)propertyElement, Constants.Kind).Value;
                BinaryData data = new BinaryData(binaryValue);
                expressions.Add(data);
            }

            return expressions;
        }

        private static T GetValue<T>(CosmosObject cosmosObject, string propertyName)
            where T : CosmosElement
        {
            bool found = TryGetValue(cosmosObject, propertyName, out T value);

            if (!found)
            {
                throw new InvalidOperationException($"{GetExceptionMessage()} The required property {propertyName} was not found in {cosmosObject}");
            }

            return value;
        }

        private static bool TryGetValue<T>(CosmosObject cosmosObject, string propertyName, out T result)
            where T : CosmosElement
        {
            bool found = cosmosObject.TryGetValue(propertyName, out CosmosElement value);

            if (found && value != null)
            {
                result = value as T;
                if (result == null)
                {
                    throw new InvalidOperationException($"{GetExceptionMessage()} Type mismatch for property {propertyName}. Expected {typeof(T)}, Actual {value?.GetType()}");
                }

                return found;
            }

            result = default(T);
            return found;
        }

        private static TEnum GetEnumValue<TEnum>(string propertyName)
            where TEnum : struct
        {
            bool success = Enum.TryParse(propertyName, out TEnum result);
            if (!success) 
            {
                throw new InvalidOperationException($"{GetExceptionMessage()} The string representation of {propertyName} enumerated constant was not able to be converted to an equivalent enumerated object");
            }

            return result;
        }

        private static string GetExceptionMessage()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            string clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
            
            return $"Exception occurred while deserializing query plan. Version : '{clientSDKVersion}', Exception/Reason : ";
        }
    }
}