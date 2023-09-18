//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using QL;

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
            public const string QL = "QL";
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
            QLEnumerableExpression expression = DeserializeQLEnumerableExpression(clientQLElement);

            return new CoordinatorDistributionPlan(expression);
        }

        #region Enumerable Expressions

        private static QLEnumerableExpression DeserializeQLEnumerableExpression(CosmosObject cosmosObject)
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
                    return DeserializeSelectManyEnumerableExpression(cosmosObject);
                case Constants.Take:
                    return DeserializeTakeEnumerableExpression(cosmosObject);
                case Constants.Where:
                    return DeserializeWhereEnumerableExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid QLExpression kind: {kindProperty.Value}");
            }
        }
        
        private static QLAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            QLAggregate aggregate = DeserializeAggregate(GetValue<CosmosObject>(cosmosObject, Constants.Aggregate));
            return new QLAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        private static QLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            QLVariable declaredVariable = DeserializeQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<QLScalarExpression> expressions = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Expression));
            return new QLDistinctEnumerableExpression(sourceExpression, declaredVariable, expressions);
        }

        private static QLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            long keyCount = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.KeyCount).Value);
            IReadOnlyList<QLAggregate> aggregates = DeserializeAggregateArray(GetValue<CosmosArray>(cosmosObject, Constants.Aggregates));
            return new QLGroupByEnumerableExpression(sourceExpression, Convert.ToUInt64(keyCount), aggregates);
        }

        private static QLInputEnumerableExpression DeserializeInputEnumerableExpression(CosmosObject cosmosObject)
        {
            return new QLInputEnumerableExpression(GetValue<CosmosString>(cosmosObject, Constants.Name).Value);
        }

        private static QLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            QLVariable declaredVariable = DeserializeQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<QLOrderByItem> orderByItems = DeserializeOrderByItemArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new QLOrderByEnumerableExpression(sourceExpression, declaredVariable, orderByItems);
        }

        private static QLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(CosmosObject cosmosObject)
        {
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            QLEnumerationKind enumerationKind = GetEnumValue<QLEnumerationKind>(GetValue<CosmosString>(cosmosObject, Constants.EnumerationKind).Value);
            return new QLScalarAsEnumerableExpression(expression, enumerationKind);
        }

        private static QLSelectEnumerableExpression DeserializeSelectEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            QLVariable declaredVariable = DeserializeQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new QLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        private static QLSelectManyEnumerableExpression DeserializeSelectManyEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            QLVariable declaredVariable = DeserializeQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            QLEnumerableExpression selectorExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SelectorExpression));
            return new QLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        private static QLTakeEnumerableExpression DeserializeTakeEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            long skipValue = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.SkipValue).Value);
            long takeExpression = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.TakeValue).Value);
            return new QLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
        }

        private static QLWhereEnumerableExpression DeserializeWhereEnumerableExpression(CosmosObject cosmosObject)
        {
            QLEnumerableExpression sourceExpression = DeserializeQLEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            return new QLWhereEnumerableExpression(sourceExpression);
        }

        #endregion
        #region Scalar Expressions

        private static QLScalarExpression DeserializeScalarExpression(CosmosObject cosmosObject)
        {
            QLScalarExpressionKind scalarExpressionKind = GetEnumValue<QLScalarExpressionKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (scalarExpressionKind)
            {
                case QLScalarExpressionKind.ArrayCreate:
                    return DeserializeArrayCreateScalarExpression(cosmosObject);
                case QLScalarExpressionKind.ArrayIndexer:
                    return DeserializeArrayIndexerScalarExpression(cosmosObject);
                case QLScalarExpressionKind.BinaryOperator:
                    return DeserializeBinaryOperatorScalarExpression(cosmosObject);
                case QLScalarExpressionKind.IsOperator:
                    return DeserializeIsOperatorScalarExpression(cosmosObject);
                case QLScalarExpressionKind.Let:
                    return DeserializeLetScalarExpression(cosmosObject);
                case QLScalarExpressionKind.Literal:
                    return DeserializeLiteralScalarExpression(cosmosObject);
                case QLScalarExpressionKind.Mux:
                    return DeserializeMuxScalarExpression(cosmosObject);
                case QLScalarExpressionKind.ObjectCreate:
                    return DeserializeObjectCreateScalarExpression(cosmosObject);
                case QLScalarExpressionKind.PropertyRef:
                    return DeserializePropertyRefScalarExpression(cosmosObject);
                case QLScalarExpressionKind.SystemFunctionCall:
                    return DeserializeSystemFunctionCallScalarExpression(cosmosObject);
                case QLScalarExpressionKind.TupleCreate:
                    return DeserializeTupleCreateScalarExpression(cosmosObject);
                case QLScalarExpressionKind.TupleItemRef:
                    return DeserializeTupleItemRefScalarExpression(cosmosObject);
                case QLScalarExpressionKind.UnaryOperator:
                    return DeserializeUnaryScalarExpression(cosmosObject);
                case QLScalarExpressionKind.UserDefinedFunctionCall:
                    return DeserializeUserDefinedFunctionCallScalarExpression(cosmosObject);
                case QLScalarExpressionKind.VariableRef:
                    return DeserializeVariableRefScalarExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid QLExpression kind: {scalarExpressionKind}");
            }
        }

        private static QLArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(CosmosObject cosmosObject)
        {
            QLArrayKind arrayKind = GetEnumValue<QLArrayKind>(GetValue<CosmosString>(cosmosObject, Constants.ArrayKind).Value);
            IReadOnlyList<QLScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new QLArrayCreateScalarExpression(arrayKind, items);
        }

        private static QLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(CosmosObject cosmosObject)
        {
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            long index = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new QLArrayIndexerScalarExpression(expression, index);
        }

        private static QLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(CosmosObject cosmosObject)
        {
            QLBinaryScalarOperatorKind operatorKind = GetEnumValue<QLBinaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            QLScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            QLScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new QLBinaryScalarExpression(operatorKind, leftExpression, rightExpression);
        }

        private static QLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(CosmosObject cosmosObject)
        {
            QLIsOperatorKind operatorKind = GetEnumValue<QLIsOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new QLIsOperatorScalarExpression(operatorKind, expression);
        }

        private static QLLetScalarExpression DeserializeLetScalarExpression(CosmosObject cosmosObject)
        {
            QLVariable declaredVariable = DeserializeQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            QLScalarExpression declaredVariableExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariableExpression));
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new QLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        private static QLLiteralScalarExpression DeserializeLiteralScalarExpression(CosmosObject cosmosObject)
        {
            CosmosObject literalObject = GetValue<CosmosObject>(cosmosObject, Constants.Literal);
            return new QLLiteralScalarExpression(DeserializeQLLiteral(literalObject));
        }

        private static QLMuxScalarExpression DeserializeMuxScalarExpression(CosmosObject cosmosObject)
        {
            QLScalarExpression conditionExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.ConditionExpression));
            QLScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            QLScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new QLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        private static QLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(CosmosObject cosmosObject)
        {
            QLObjectKind objectKind = GetEnumValue<QLObjectKind>(GetValue<CosmosString>(cosmosObject, Constants.ObjectKind).Value);
            IReadOnlyList<QLObjectProperty> properties = DeserializeObjectProperties(GetValue<CosmosArray>(cosmosObject, Constants.Properties));
            return new QLObjectCreateScalarExpression(properties, objectKind);
        }

        private static QLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(CosmosObject cosmosObject)
        {
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            string propertyName = GetValue<CosmosString>(cosmosObject, Constants.PropertyName).Value;
            return new QLPropertyRefScalarExpression(expression, propertyName);
        }

        private static QLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            QLBuiltinScalarFunctionKind functionKind = GetEnumValue<QLBuiltinScalarFunctionKind>(GetValue<CosmosString>(cosmosObject, Constants.FunctionKind).Value);
            IReadOnlyList<QLScalarExpression> arguments = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Arguments));
            return new QLSystemFunctionCallScalarExpression(functionKind, arguments);
        }

        private static QLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(CosmosObject cosmosObject)
        {
            IReadOnlyList<QLScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new QLTupleCreateScalarExpression(items);
        }

        private static QLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(CosmosObject cosmosObject)
        {
            QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            long index = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new QLTupleItemRefScalarExpression(expression, index);
        }

        private static QLUnaryScalarExpression DeserializeUnaryScalarExpression(CosmosObject cosmosObject)
        {
            QLUnaryScalarOperatorKind operatorKind = GetEnumValue<QLUnaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            QLScalarExpression scalarExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new QLUnaryScalarExpression(operatorKind, scalarExpression);
        }

        private static QLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            string identifierString = GetValue<CosmosString>(cosmosObject, Constants.Identifier).Value;
            QLFunctionIdentifier functionIdentifier = new QLFunctionIdentifier(identifierString);
            IReadOnlyList<QLScalarExpression> arguments = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Arguments));
            bool builtin = GetValue<CosmosBoolean>(cosmosObject, Constants.Builtin).Value;
            return new QLUserDefinedFunctionCallScalarExpression(functionIdentifier, arguments, builtin);
        }

        private static QLVariableRefScalarExpression DeserializeVariableRefScalarExpression(CosmosObject cosmosObject)
        {
            QLVariable variable = DeserializeQLVariable(GetValue<CosmosObject>(cosmosObject, Constants.Variable));
            return new QLVariableRefScalarExpression(variable);
        }

        #endregion
        #region Aggregate Expressions

        private static IReadOnlyList<QLAggregate> DeserializeAggregateArray(CosmosArray cosmosArray)
        {
            List<QLAggregate> aggregates = new List<QLAggregate>(cosmosArray.Count);
            foreach (CosmosElement aggregateElement in cosmosArray)
            {
                CosmosObject aggregateObject = CastToCosmosObject(aggregateElement);
                aggregates.Add(DeserializeAggregate(aggregateObject));
            }

            return aggregates;
        }

        private static QLAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            QLAggregateKind aggregateKind = GetEnumValue<QLAggregateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (aggregateKind)
            {
                case QLAggregateKind.Builtin:
                    return DeserializeBuiltInAggregateExpression(cosmosObject);
                case QLAggregateKind.Tuple:
                    return DeserializeTupleAggregateExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid QLExpression kind: {aggregateKind}");
            }
        }

        private static QLBuiltinAggregate DeserializeBuiltInAggregateExpression(CosmosObject cosmosObject)
        {
            QLAggregateOperatorKind aggregateOperatorKind = GetEnumValue<QLAggregateOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            return new QLBuiltinAggregate(aggregateOperatorKind);
        }

        private static QLTupleAggregate DeserializeTupleAggregateExpression(CosmosObject cosmosObject)
        {
            CosmosArray tupleArray = GetValue<CosmosArray>(cosmosObject, Constants.Items);
            List<QLAggregate> aggregates = new List<QLAggregate>(tupleArray.Count);

            foreach (CosmosElement tupleElement in tupleArray)
            {
                CosmosObject tupleObject = CastToCosmosObject(tupleElement);
                QLAggregate aggregate = DeserializeAggregate(tupleObject);
                aggregates.Add(aggregate);
            }

            return new QLTupleAggregate(aggregates);
        }

        #endregion
        #region Literal Expressions

        private static QLLiteral DeserializeQLLiteral(CosmosObject cosmosObject)
        {
            QLLiteralKind literalKind = GetEnumValue<QLLiteralKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (literalKind)
            {
                case QLLiteralKind.Array:
                    return new QLArrayLiteral(DeserializeLiteralArray(GetValue<CosmosArray>(cosmosObject, Constants.Items)));
                case QLLiteralKind.Binary:
                    return new QLBinaryLiteral(DeserializeBinaryLiteral(GetValue<CosmosArray>(cosmosObject, Constants.Value)));
                case QLLiteralKind.Boolean:
                    return new QLBooleanLiteral(GetValue<CosmosBoolean>(cosmosObject, Constants.Value).Value);
                case QLLiteralKind.CGuid:
                    return new QLCGuidLiteral(GetValue<CosmosGuid>(cosmosObject, Constants.Value).Value);
                case QLLiteralKind.CNumber:
                    return new QLCNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case QLLiteralKind.MDateTime:
                    return new QLMDateTimeLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case QLLiteralKind.MJavaScript:
                    return new QLMJavaScriptLiteral(GetValue<CosmosString>(cosmosObject, Constants.Name).Value);
                case QLLiteralKind.MNumber:
                    return new QLMNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case QLLiteralKind.MRegex:
                    string pattern = GetValue<CosmosString>(cosmosObject, Constants.Pattern).Value;
                    string options = GetValue<CosmosString>(cosmosObject, Constants.Options).Value;
                    return new QLMRegexLiteral(pattern, options);
                case QLLiteralKind.MSingleton:
                    return new QLMSingletonLiteral(GetEnumValue<QLMSingletonLiteral.Kind>(GetValue<CosmosString>(cosmosObject, Constants.SingletonKind).Value));
                case QLLiteralKind.MSymbol:
                    return new QLMSymbolLiteral(GetValue<CosmosString>(cosmosObject, Constants.Value).Value);
                case QLLiteralKind.Null:
                    return QLNullLiteral.Singleton;
                case QLLiteralKind.Number:
                    return new QLNumberLiteral(Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value));
                case QLLiteralKind.Object:
                    return new QLObjectLiteral(DeserializeObjectLiteralArray(GetValue<CosmosArray>(cosmosObject, Constants.Properties)));
                case QLLiteralKind.String:
                    return new QLStringLiteral(GetValue<CosmosString>(cosmosObject, Constants.Value).Value);
                case QLLiteralKind.Undefined:
                    return QLUndefinedLiteral.Singleton;
                default:
                    throw new NotSupportedException($"Invalid QLExpression kind: {literalKind}");
            }
        }

        private static IReadOnlyList<QLLiteral> DeserializeLiteralArray(CosmosArray cosmosArray)
        {
            List<QLLiteral> literals = new List<QLLiteral>(cosmosArray.Count);
            foreach (CosmosElement literalElement in cosmosArray)
            {
                CosmosObject literalObject = CastToCosmosObject(literalElement);
                literals.Add(DeserializeQLLiteral(literalObject));
            }
        
            return literals;
        }

        private static byte[] DeserializeBinaryLiteral(CosmosArray cosmosArray)
        {
            List<ReadOnlyMemory<byte>> binaryLiterals = new List<ReadOnlyMemory<byte>>(cosmosArray.Count);
            int memoryLength = 0;
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = CastToCosmosObject(propertyElement);
                ReadOnlyMemory<byte> bytes = GetValue<CosmosBinary>(propertyObject, Constants.Kind).Value;
                memoryLength += bytes.Length;
                binaryLiterals.Add(bytes);
            }

            byte[] byteCollection = new byte[memoryLength];
            int currentIndex = 0;
            foreach (ReadOnlyMemory<byte> bytes in binaryLiterals)
            {
                bytes.CopyTo(byteCollection.AsMemory(currentIndex));
                currentIndex += bytes.Length;
            }

            return byteCollection;
        }

        private static IReadOnlyList<QLObjectLiteralProperty> DeserializeObjectLiteralArray(CosmosArray cosmosArray)
        {
            List<QLObjectLiteralProperty> objectLiterals = new List<QLObjectLiteralProperty>(cosmosArray.Count);
            foreach (CosmosElement objectLiteralElement in cosmosArray)
            {
                CosmosObject propertyObject = CastToCosmosObject(objectLiteralElement);
                string name = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                QLLiteral literal = DeserializeQLLiteral(propertyObject);
                QLObjectLiteralProperty objectLiteralProperty = new QLObjectLiteralProperty(name, literal);
                objectLiterals.Add(objectLiteralProperty);
            }

            return objectLiterals;
        }

        #endregion
        #region Helper Functions

        private static QLVariable DeserializeQLVariable(CosmosObject cosmosObject)
        {
            string name = GetValue<CosmosString>(cosmosObject, Constants.Name).Value;
            long uniqueId = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.UniqueId).Value);

            return new QLVariable(name, uniqueId);
        }

        private static IReadOnlyList<QLObjectProperty> DeserializeObjectProperties(CosmosArray cosmosArray)
        {
            List<QLObjectProperty> properties = new List<QLObjectProperty>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = CastToCosmosObject(propertyElement);
                string objectPropertyName = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                QLScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(propertyObject, Constants.Expression));
                properties.Add(new QLObjectProperty(objectPropertyName, expression));
            }

            return properties;
        }

        private static IReadOnlyList<QLScalarExpression> DeserializeScalarExpressionArray(CosmosArray cosmosArray)
        {
            List<QLScalarExpression> expressions = new List<QLScalarExpression>(cosmosArray.Count);
            foreach (CosmosElement itemElement in cosmosArray)
            {
                CosmosObject itemObject = CastToCosmosObject(itemElement);
                QLScalarExpression expression = DeserializeScalarExpression(itemObject);
                expressions.Add(expression);
            }

            return expressions;
        }

        private static IReadOnlyList<QLOrderByItem> DeserializeOrderByItemArray(CosmosArray cosmosArray)
        {
            List<QLOrderByItem> expressions = new List<QLOrderByItem>(cosmosArray.Count);
            foreach (CosmosElement itemElement in cosmosArray)
            {
                CosmosObject itemObject = CastToCosmosObject(itemElement);
                QLSortOrder sortOrder = GetEnumValue<QLSortOrder>(GetValue<CosmosString>(itemObject, Constants.SortOrder).Value);
                QLScalarExpression scalarExpression = DeserializeScalarExpression(itemObject);
                expressions.Add(new QLOrderByItem(scalarExpression, sortOrder));
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
            bool success = Enum.TryParse(propertyName, ignoreCase: true, out TEnum result);
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

        private static CosmosObject CastToCosmosObject(CosmosElement cosmosElement)
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