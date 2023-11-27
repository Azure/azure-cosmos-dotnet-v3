//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Cql;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal static class ClientDistributionPlanDeserializer
    {
        private static class Constants
        {
            public const string Arguments = "Arguments";
            public const string ArrayKind = "ArrayKind";
            public const string Aggregate = "Aggregate";
            public const string Aggregates = "Aggregates";
            public const string Builtin = "Builtin";
            public const string Cql = "Cql";
            public const string ConditionExpression = "ConditionExpression";
            public const string ClientDistributionPlan = "clientDistributionPlan";
            public const string DeclaredVariable = "DeclaredVariable";
            public const string DeclaredVariableExpression = "DeclaredVariableExpression";
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

        public static ClientDistributionPlan DeserializeClientDistributionPlan(string jsonString)
        {
            CosmosObject cosmosObject = CosmosObject.Parse(jsonString);
            CosmosObject clientDistributionPlanElement = GetValue<CosmosObject>(cosmosObject, Constants.ClientDistributionPlan);
            CosmosObject cqlElement = GetValue<CosmosObject>(clientDistributionPlanElement, Constants.Cql);
            CqlEnumerableExpression expression = DeserializeCqlEnumerableExpression(cqlElement);

            return new ClientDistributionPlan(expression);
        }

        #region Enumerable Expressions

        private static CqlEnumerableExpression DeserializeCqlEnumerableExpression(CosmosObject cosmosObject)
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
                    throw new NotSupportedException($"Invalid CqlExpression kind: {kindProperty.Value}");
            }
        }
        
        private static CqlAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            CqlAggregate aggregate = DeserializeAggregate(GetValue<CosmosObject>(cosmosObject, Constants.Aggregate));
            return new CqlAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        private static CqlDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            CqlVariable declaredVariable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<CqlScalarExpression> expressions = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Expression));
            return new CqlDistinctEnumerableExpression(sourceExpression, declaredVariable, expressions);
        }

        private static CqlGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            long keyCount = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.KeyCount).Value);
            IReadOnlyList<CqlAggregate> aggregates = DeserializeAggregateArray(GetValue<CosmosArray>(cosmosObject, Constants.Aggregates));
            return new CqlGroupByEnumerableExpression(sourceExpression, Convert.ToUInt64(keyCount), aggregates);
        }

        private static CqlInputEnumerableExpression DeserializeInputEnumerableExpression(CosmosObject cosmosObject)
        {
            return new CqlInputEnumerableExpression(GetValue<CosmosString>(cosmosObject, Constants.Name).Value);
        }

        private static CqlOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            CqlVariable declaredVariable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<CqlOrderByItem> orderByItems = DeserializeOrderByItemArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new CqlOrderByEnumerableExpression(sourceExpression, declaredVariable, orderByItems);
        }

        private static CqlScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            CqlEnumerationKind enumerationKind = GetEnumValue<CqlEnumerationKind>(GetValue<CosmosString>(cosmosObject, Constants.EnumerationKind).Value);
            return new CqlScalarAsEnumerableExpression(expression, enumerationKind);
        }

        private static CqlSelectEnumerableExpression DeserializeSelectEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            CqlVariable declaredVariable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new CqlSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        private static CqlSelectManyEnumerableExpression DeserializeSelectManyEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            CqlVariable declaredVariable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            CqlEnumerableExpression selectorExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SelectorExpression));
            return new CqlSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        private static CqlTakeEnumerableExpression DeserializeTakeEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            long skipValue = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.SkipValue).Value);
            long takeExpression = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.TakeValue).Value);
            return new CqlTakeEnumerableExpression(sourceExpression, Convert.ToUInt64(skipValue), Convert.ToUInt64(takeExpression));
        }

        private static CqlWhereEnumerableExpression DeserializeWhereEnumerableExpression(CosmosObject cosmosObject)
        {
            CqlEnumerableExpression sourceExpression = DeserializeCqlEnumerableExpression(GetValue<CosmosObject>(cosmosObject, Constants.SourceExpression));
            CqlVariable declaredVariable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new CqlWhereEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        #endregion

        #region Scalar Expressions

        private static CqlScalarExpression DeserializeScalarExpression(CosmosObject cosmosObject)
        {
            CqlScalarExpressionKind scalarExpressionKind = GetEnumValue<CqlScalarExpressionKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (scalarExpressionKind)
            {
                case CqlScalarExpressionKind.ArrayCreate:
                    return DeserializeArrayCreateScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.ArrayIndexer:
                    return DeserializeArrayIndexerScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.BinaryOperator:
                    return DeserializeBinaryOperatorScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.IsOperator:
                    return DeserializeIsOperatorScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.Let:
                    return DeserializeLetScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.Literal:
                    return DeserializeLiteralScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.Mux:
                    return DeserializeMuxScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.ObjectCreate:
                    return DeserializeObjectCreateScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.PropertyRef:
                    return DeserializePropertyRefScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.SystemFunctionCall:
                    return DeserializeSystemFunctionCallScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.TupleCreate:
                    return DeserializeTupleCreateScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.TupleItemRef:
                    return DeserializeTupleItemRefScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.UnaryOperator:
                    return DeserializeUnaryScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.UserDefinedFunctionCall:
                    return DeserializeUserDefinedFunctionCallScalarExpression(cosmosObject);
                case CqlScalarExpressionKind.VariableRef:
                    return DeserializeVariableRefScalarExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid CqlExpression kind: {scalarExpressionKind}");
            }
        }

        private static CqlArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(CosmosObject cosmosObject)
        {
            IReadOnlyList<CqlScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new CqlArrayCreateScalarExpression(items);
        }

        private static CqlArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(CosmosObject cosmosObject)
        {
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            long index = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new CqlArrayIndexerScalarExpression(expression, Convert.ToUInt64(index));
        }

        private static CqlBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(CosmosObject cosmosObject)
        {
            CqlBinaryScalarOperatorKind operatorKind = GetEnumValue<CqlBinaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            CqlScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            CqlScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new CqlBinaryScalarExpression(operatorKind, leftExpression, rightExpression);
        }

        private static CqlIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(CosmosObject cosmosObject)
        {
            CqlIsOperatorKind operatorKind = GetEnumValue<CqlIsOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new CqlIsOperatorScalarExpression(operatorKind, expression);
        }

        private static CqlLetScalarExpression DeserializeLetScalarExpression(CosmosObject cosmosObject)
        {
            CqlVariable declaredVariable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            CqlScalarExpression declaredVariableExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.DeclaredVariableExpression));
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new CqlLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        private static CqlLiteralScalarExpression DeserializeLiteralScalarExpression(CosmosObject cosmosObject)
        {
            CosmosObject literalObject = GetValue<CosmosObject>(cosmosObject, Constants.Literal);
            return new CqlLiteralScalarExpression(DeserializeLiteral(literalObject));
        }

        private static CqlMuxScalarExpression DeserializeMuxScalarExpression(CosmosObject cosmosObject)
        {
            CqlScalarExpression conditionExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.ConditionExpression));
            CqlScalarExpression leftExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.LeftExpression));
            CqlScalarExpression rightExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new CqlMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        private static CqlObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(CosmosObject cosmosObject)
        {
            IReadOnlyList<CqlObjectProperty> properties = DeserializeObjectProperties(GetValue<CosmosArray>(cosmosObject, Constants.Properties));
            return new CqlObjectCreateScalarExpression(properties);
        }

        private static CqlPropertyRefScalarExpression DeserializePropertyRefScalarExpression(CosmosObject cosmosObject)
        {
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            string propertyName = GetValue<CosmosString>(cosmosObject, Constants.PropertyName).Value;
            return new CqlPropertyRefScalarExpression(expression, propertyName);
        }

        private static CqlSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            CqlBuiltinScalarFunctionKind functionKind = GetEnumValue<CqlBuiltinScalarFunctionKind>(GetValue<CosmosString>(cosmosObject, Constants.FunctionKind).Value);
            IReadOnlyList<CqlScalarExpression> arguments = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Arguments));
            return new CqlSystemFunctionCallScalarExpression(functionKind, arguments);
        }

        private static CqlTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(CosmosObject cosmosObject)
        {
            IReadOnlyList<CqlScalarExpression> items = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Items));
            return new CqlTupleCreateScalarExpression(items);
        }

        private static CqlTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(CosmosObject cosmosObject)
        {
            CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            long index = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.Index).Value);
            return new CqlTupleItemRefScalarExpression(expression, Convert.ToUInt64(index));
        }

        private static CqlUnaryScalarExpression DeserializeUnaryScalarExpression(CosmosObject cosmosObject)
        {
            CqlUnaryScalarOperatorKind operatorKind = GetEnumValue<CqlUnaryScalarOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            CqlScalarExpression scalarExpression = DeserializeScalarExpression(GetValue<CosmosObject>(cosmosObject, Constants.Expression));
            return new CqlUnaryScalarExpression(operatorKind, scalarExpression);
        }

        private static CqlUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            string identifierString = GetValue<CosmosString>(cosmosObject, Constants.Identifier).Value;
            CqlFunctionIdentifier functionIdentifier = new CqlFunctionIdentifier(identifierString);
            IReadOnlyList<CqlScalarExpression> arguments = DeserializeScalarExpressionArray(GetValue<CosmosArray>(cosmosObject, Constants.Arguments));
            bool builtin = GetValue<CosmosBoolean>(cosmosObject, Constants.Builtin).Value;
            return new CqlUserDefinedFunctionCallScalarExpression(functionIdentifier, arguments, builtin);
        }

        private static CqlVariableRefScalarExpression DeserializeVariableRefScalarExpression(CosmosObject cosmosObject)
        {
            CqlVariable variable = DeserializeCqlVariable(GetValue<CosmosObject>(cosmosObject, Constants.Variable));
            return new CqlVariableRefScalarExpression(variable);
        }

        #endregion

        #region Aggregate

        private static IReadOnlyList<CqlAggregate> DeserializeAggregateArray(CosmosArray cosmosArray)
        {
            List<CqlAggregate> aggregates = new List<CqlAggregate>(cosmosArray.Count);
            foreach (CosmosElement aggregateElement in cosmosArray)
            {
                CosmosObject aggregateObject = CastToCosmosObject(aggregateElement);
                aggregates.Add(DeserializeAggregate(aggregateObject));
            }

            return aggregates;
        }

        private static CqlAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            CqlAggregateKind aggregateKind = GetEnumValue<CqlAggregateKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (aggregateKind)
            {
                case CqlAggregateKind.Builtin:
                    return DeserializeBuiltInAggregateExpression(cosmosObject);
                case CqlAggregateKind.Tuple:
                    return DeserializeTupleAggregateExpression(cosmosObject);
                default:
                    throw new NotSupportedException($"Invalid CqlExpression kind: {aggregateKind}");
            }
        }

        private static CqlBuiltinAggregate DeserializeBuiltInAggregateExpression(CosmosObject cosmosObject)
        {
            CqlAggregateOperatorKind aggregateOperatorKind = GetEnumValue<CqlAggregateOperatorKind>(GetValue<CosmosString>(cosmosObject, Constants.OperatorKind).Value);
            return new CqlBuiltinAggregate(aggregateOperatorKind);
        }

        private static CqlTupleAggregate DeserializeTupleAggregateExpression(CosmosObject cosmosObject)
        {
            CosmosArray tupleArray = GetValue<CosmosArray>(cosmosObject, Constants.Items);
            List<CqlAggregate> aggregates = new List<CqlAggregate>(tupleArray.Count);

            foreach (CosmosElement tupleElement in tupleArray)
            {
                CosmosObject tupleObject = CastToCosmosObject(tupleElement);
                CqlAggregate aggregate = DeserializeAggregate(tupleObject);
                aggregates.Add(aggregate);
            }

            return new CqlTupleAggregate(aggregates);
        }

        #endregion

        #region Literal

        private static CqlLiteral DeserializeLiteral(CosmosObject cosmosObject)
        {
            CqlLiteralKind literalKind = GetEnumValue<CqlLiteralKind>(GetValue<CosmosString>(cosmosObject, Constants.Kind).Value);
            switch (literalKind)
            {
                case CqlLiteralKind.Array:
                    return new CqlArrayLiteral(DeserializeLiteralArray(GetValue<CosmosArray>(cosmosObject, Constants.Items)));
                case CqlLiteralKind.Boolean:
                    return new CqlBooleanLiteral(GetValue<CosmosBoolean>(cosmosObject, Constants.Value).Value);
                case CqlLiteralKind.Null:
                    return CqlNullLiteral.Singleton;
                case CqlLiteralKind.Number:
                    return new CqlNumberLiteral(GetValue<CosmosNumber>(cosmosObject, Constants.Value).Value);
                case CqlLiteralKind.Object:
                    return new CqlObjectLiteral(DeserializeObjectLiteralArray(GetValue<CosmosArray>(cosmosObject, Constants.Properties)));
                case CqlLiteralKind.String:
                    return new CqlStringLiteral(GetValue<CosmosString>(cosmosObject, Constants.Value).Value);
                case CqlLiteralKind.Undefined:
                    return CqlUndefinedLiteral.Singleton;
                default:
                    throw new NotSupportedException($"Invalid CqlExpression kind: {literalKind}");
            }
        }

        private static IReadOnlyList<CqlLiteral> DeserializeLiteralArray(CosmosArray cosmosArray)
        {
            List<CqlLiteral> literals = new List<CqlLiteral>(cosmosArray.Count);
            foreach (CosmosElement literalElement in cosmosArray)
            {
                CosmosObject literalObject = CastToCosmosObject(literalElement);
                literals.Add(DeserializeLiteral(literalObject));
            }
        
            return literals;
        }

        private static IReadOnlyList<CqlObjectLiteralProperty> DeserializeObjectLiteralArray(CosmosArray cosmosArray)
        {
            List<CqlObjectLiteralProperty> objectLiterals = new List<CqlObjectLiteralProperty>(cosmosArray.Count);
            foreach (CosmosElement objectLiteralElement in cosmosArray)
            {
                CosmosObject propertyObject = CastToCosmosObject(objectLiteralElement);
                string name = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                CqlLiteral literal = DeserializeLiteral(propertyObject);
                CqlObjectLiteralProperty objectLiteralProperty = new CqlObjectLiteralProperty(name, literal);
                objectLiterals.Add(objectLiteralProperty);
            }

            return objectLiterals;
        }

        #endregion

        #region Helper Functions

        private static CqlVariable DeserializeCqlVariable(CosmosObject cosmosObject)
        {
            string name = GetValue<CosmosString>(cosmosObject, Constants.Name).Value;
            long uniqueId = Number64.ToLong(GetValue<CosmosNumber>(cosmosObject, Constants.UniqueId).Value);

            return new CqlVariable(name, uniqueId);
        }

        private static IReadOnlyList<CqlObjectProperty> DeserializeObjectProperties(CosmosArray cosmosArray)
        {
            List<CqlObjectProperty> properties = new List<CqlObjectProperty>(cosmosArray.Count);
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = CastToCosmosObject(propertyElement);
                string objectPropertyName = GetValue<CosmosString>(propertyObject, Constants.Name).Value;
                CqlScalarExpression expression = DeserializeScalarExpression(GetValue<CosmosObject>(propertyObject, Constants.Expression));
                properties.Add(new CqlObjectProperty(objectPropertyName, expression));
            }

            return properties;
        }

        private static IReadOnlyList<CqlScalarExpression> DeserializeScalarExpressionArray(CosmosArray cosmosArray)
        {
            List<CqlScalarExpression> expressions = new List<CqlScalarExpression>(cosmosArray.Count);
            foreach (CosmosElement itemElement in cosmosArray)
            {
                CosmosObject itemObject = CastToCosmosObject(itemElement);
                CqlScalarExpression expression = DeserializeScalarExpression(itemObject);
                expressions.Add(expression);
            }

            return expressions;
        }

        private static IReadOnlyList<CqlOrderByItem> DeserializeOrderByItemArray(CosmosArray cosmosArray)
        {
            List<CqlOrderByItem> expressions = new List<CqlOrderByItem>(cosmosArray.Count);
            foreach (CosmosElement itemElement in cosmosArray)
            {
                CosmosObject itemObject = CastToCosmosObject(itemElement);
                CqlSortOrder sortOrder = GetEnumValue<CqlSortOrder>(GetValue<CosmosString>(itemObject, Constants.SortOrder).Value);
                CqlScalarExpression scalarExpression = DeserializeScalarExpression(itemObject);
                expressions.Add(new CqlOrderByItem(scalarExpression, sortOrder));
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