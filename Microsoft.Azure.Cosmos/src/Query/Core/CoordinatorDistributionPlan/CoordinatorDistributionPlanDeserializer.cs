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
            public const string FunctionKind = "FunctionKind";
            public const string GroupBy = "GroupBy";
            public const string Identifier = "Identifier";
            public const string Index = "Index";
            public const string Input = "Input";
            public const string Items = "Items";
            public const string KeyCount = "KeyCount";
            public const string Kind = "Kind";
            public const string LeftExpression = "LeftExpression";
            public const string Literal = "Literal";
            public const string MaxDepth = "MaxDepth";
            public const string Name = "Name";
            public const string ObjectKind = "ObjectKind";
            public const string OperatorKind = "OperatorKind";
            public const string Options = "Options";
            public const string OrderBy = "OrderBy";
            public const string Pattern = "Pattern";
            public const string Properties = "Properties";
            public const string PropertyName = "PropertyName";
            public const string RightExpression = "RightExpression";
            public const string ScalarAsEnumerable = "ScalarAsEnumerable";
            public const string Select = "Select";
            public const string SelectorExpression = "SelectorExpression";
            public const string SelectMany = "SelectMany";
            public const string SingletonKind = "SingletonKind";
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

        #region Enumerable Expressions

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
            return new ClientQLWhereEnumerableExpression(sourceExpression);
        }

        #endregion
        #region Scalar Expressions

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
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new ClientQLBinaryScalarExpression(operatorKind, leftExpression, rightExpression);
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
            CosmosObject literalObject = GetValue<CosmosObject>(cosmosObject, Constants.Literal);
            return new ClientQLLiteralScalarExpression(DeserializeClientQLLiteral(literalObject));
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

        #endregion
        #region Literal Expressions
        private static ClientQLLiteral DeserializeClientQLLiteral(CosmosObject cosmosObject)
        {
            ClientQLLiteralKind literalKind = GetEnumValue<ClientQLLiteralKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (literalKind)
            {
                case ClientQLLiteralKind.Undefined:
                    return ClientQLUndefinedLiteral.Singleton;
                case ClientQLLiteralKind.Array:
                    return new ClientQLArrayLiteral(DeserializeLiteralArray(GetValue<CosmosArray>(cosmosObject, Constants.Items)));
                case ClientQLLiteralKind.Binary:
                    return new ClientQLBinaryLiteral(DeserializeBinaryArray(GetValue<CosmosArray>(cosmosObject, Constants.Value)));
                case ClientQLLiteralKind.Boolean:
                    return new ClientQLBooleanLiteral(GetValue<CosmosBoolean>(cosmosObject, Constants.Value).Value);
                case ClientQLLiteralKind.CGuid:
                    return new ClientQLCGuidLiteral(GetValue<CosmosGuid>(cosmosObject, Constants.Value).Value);
                case ClientQLLiteralKind.CNumber:
                    return new ClientQLCNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case ClientQLLiteralKind.MDateTime:
                    return new ClientQLMDateTimeLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case ClientQLLiteralKind.MJavaScript:
                    return new ClientQLMJavaScriptLiteral(GetValue<CosmosString>(cosmosObject, Constants.Name).Value);
                case ClientQLLiteralKind.MNumber:
                    return new ClientQLMNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case ClientQLLiteralKind.MRegex:
                    string pattern = GetValue<CosmosString>(cosmosObject, Constants.Pattern).Value;
                    string options = GetValue<CosmosString>(cosmosObject, Constants.Options).Value;
                    return new ClientQLMRegexLiteral(pattern, options);
                case ClientQLLiteralKind.MSingleton:
                    return new ClientQLMSingletonLiteral(GetEnumValue<ClientQLMSingletonLiteral.Kind>(GetValue<CosmosString>(cosmosObject, Constants.SingletonKind).Value));
                case ClientQLLiteralKind.MSymbol:
                    return new ClientQLMSymbolLiteral(GetValue<CosmosString>(cosmosObject, Constants.Value).Value);
                case ClientQLLiteralKind.Null:
                    return ClientQLNullLiteral.Singleton;
                case ClientQLLiteralKind.Number:
                    return new ClientQLNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case ClientQLLiteralKind.Object:
                    return new ClientQLObjectLiteral(DeserializeObjectLiteralArray(GetValue<CosmosArray>(cosmosObject, Constants.Properties)));
                case ClientQLLiteralKind.String:
                    return new ClientQLStringLiteral(GetValue<CosmosString>(cosmosObject, Constants.Value).Value);
                default:
                    throw new NotSupportedException($"Invalid ClientQLExpression kind: {literalKind}");
            }
        }

        private static ClientQLAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            ClientQLAggregateKind aggregateKind = GetEnumValue<ClientQLAggregateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (aggregateKind)
            {
                case ClientQLAggregateKind.Tuple:
                    return DeserializeTupleAggregateExpression(cosmosObject);
                case ClientQLAggregateKind.Builtin:
                    return DeserializeBuiltInAggregateExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid ClientQLExpression kind: {aggregateKind}");
            }
        }

        private static ClientQLVariable DeserializeClientQLVariable(CosmosObject cosmosObject)
        {
            string name = GetValue<CosmosString>(cosmosObject, Constants.Name).Value;
            long uniqueId = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.UniqueId).Value);

            return new ClientQLVariable(name, uniqueId);
        }

        private static ClientQLBuiltinAggregate DeserializeBuiltInAggregateExpression(CosmosObject cosmosObject)
        {
            ClientQLAggregateOperatorKind aggregateOperatorKind = GetEnumValue<ClientQLAggregateOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            return new ClientQLBuiltinAggregate(aggregateOperatorKind);
        }

        private static ClientQLTupleAggregate DeserializeTupleAggregateExpression(CosmosObject cosmosObject)
        {
            CosmosArray propertyArray = GetValue<CosmosArray>(cosmosObject, Constants.Items);
            List<ClientQLAggregate> expression = new List<ClientQLAggregate>(propertyArray.Count);

            foreach (CosmosElement propertyElement in propertyArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                ClientQLAggregate aggregateExpression = DeserializeAggregate(propertyObject);
                expression.Add(aggregateExpression);
            }

            return new ClientQLTupleAggregate(expression);
        }

        private static IReadOnlyList<ClientQLObjectProperty> DeserializeObjectProperties(CosmosArray cosmosArray)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                string objectPropertyName = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                ClientQLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(propertyObject, Constants.Expression)); 
                properties.Add(new ClientQLObjectProperty(objectPropertyName, expression));
            }

            return properties;
        }

        private static IReadOnlyList<ClientQLScalarExpression> DeserializeScalarExpressionArray(CosmosArray cosmosArray)
        {
            List<ClientQLScalarExpression> expressions = new List<ClientQLScalarExpression>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                ClientQLScalarExpression expression = DeserializeScalarExpression(propertyObject);
                expressions.Add(expression);
            }

            return expressions;
        }

        private static IReadOnlyList<ClientQLOrderByItem> DeserializeOrderByItemArray(CosmosArray cosmosArray)
        {
            List<ClientQLOrderByItem> expressions = new List<ClientQLOrderByItem>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                ClientQLSortOrder sortOrder = GetEnumValue<ClientQLSortOrder>(GetValue<CosmosString>(propertyObject, Constants.SortOrder).Value);
                ClientQLScalarExpression scalarExpression = DeserializeScalarExpression(propertyObject);
                expressions.Add(new ClientQLOrderByItem(scalarExpression, sortOrder));
            }

            return expressions;
        }

        private static IReadOnlyList<ClientQLAggregate> DeserializeAggregateArray(CosmosArray cosmosArray)
        {
            List<ClientQLAggregate> expressions = new List<ClientQLAggregate>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                expressions.Add(DeserializeAggregate(propertyObject));
            }

            return expressions;
        }

        private static IReadOnlyList<ClientQLLiteral> DeserializeLiteralArray(CosmosArray cosmosArray)
        {
            List<ClientQLLiteral> expressions = new List<ClientQLLiteral>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                expressions.Add(DeserializeClientQLLiteral(propertyObject));
            }
        
            return expressions;
        }

        private static IReadOnlyList<ClientQLObjectLiteralProperty> DeserializeObjectLiteralArray(CosmosArray cosmosArray)
        {
            List<ClientQLObjectLiteralProperty> expressions = new List<ClientQLObjectLiteralProperty>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                string name = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                ClientQLLiteral literal = DeserializeClientQLLiteral(propertyObject);
                ClientQLObjectLiteralProperty objectLiteralProperty = new ClientQLObjectLiteralProperty(name, literal);
                expressions.Add(objectLiteralProperty);
            }

            return expressions;
        }

        private static byte[] DeserializeBinaryArray(CosmosArray cosmosArray)
        {
            List<ReadOnlyMemory<byte>> expressions = new List<ReadOnlyMemory<byte>>(cosmosArray.Count);
            int memoryLength = 0;
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = ConvertToCosmosObject(propertyElement);
                ReadOnlyMemory<byte> binaryValue = GetValue<CosmosBinary>(propertyObject, Constants.Kind).Value;
                memoryLength += binaryValue.Length;
                expressions.Add(binaryValue);
            }

            byte[] newArray = new byte[memoryLength];
            int currentIndex = 0;
            foreach (ReadOnlyMemory<byte> byteCollection in expressions)
            {
                byteCollection.CopyTo(newArray.AsMemory(currentIndex));
                currentIndex += byteCollection.Length;
            }

            return newArray;
        }

        #endregion
        #region Helper Functions

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

        private static CosmosObject ConvertToCosmosObject(CosmosElement cosmosElement)
        {
            CosmosObject propertyObject = cosmosElement as CosmosObject;
            if (propertyObject != null)
            {
                return propertyObject;
            }
            else
            {
                throw new InvalidOperationException("Unable to cast CosmosElement to CosmosObject.");
            }
        }

        #endregion
    }
}