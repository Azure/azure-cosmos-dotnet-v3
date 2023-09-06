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
            CosmosObject cosmosObject = CosmosObject.Parse(jsonString);

            if (cosmosObject.TryGetValue(Constants.CoordinatorDistributionPlan, out CosmosElement coordinatorDistributionPlanElement) &&
                coordinatorDistributionPlanElement is CosmosObject coordinatorDistributionPlanObject &&
                coordinatorDistributionPlanObject.TryGetValue(Constants.ClientQL, out CosmosElement clientQLElement))
            {
                ClientQLEnumerableExpression expression = DeserializeClientQLEnumerableExpression((CosmosObject)clientQLElement);
                return new CoordinatorDistributionPlan(expression);
            }
            else
            {
                return null;
            }
        }

        private static ClientQLEnumerableExpression DeserializeClientQLEnumerableExpression(CosmosObject cosmosObject)
        {
            cosmosObject.TryGetValue(Constants.Kind, out CosmosElement token);
            switch (RemoveQuotesIfPresent(token.ToString()))
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
                    throw new ArgumentNullException("blah");
            }
        }
        
        private static ClientQLAggregateEnumerableExpression DeserializeAggregateEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLAggregate aggregate = DeserializeAggregate(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Aggregate));
            return new ClientQLAggregateEnumerableExpression(sourceExpression, aggregate);
        }

        private static ClientQLDistinctEnumerableExpression DeserializeDistinctEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetCosmosElement<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<ClientQLScalarExpression> vecExpressions = DeserializeScalarExpressionArray(GetCosmosElement<CosmosArray>(cosmosObject, Constants.VecExpression));
            return new ClientQLDistinctEnumerableExpression(sourceExpression, declaredVariable, vecExpressions);
        }

        private static ClientQLGroupByEnumerableExpression DeserializeGroupByEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            IReadOnlyList<ClientQLGroupByKey> vecKeys = TryGetValue<IReadOnlyList<ClientQLGroupByKey>>(cosmosObject, Constants.VecKeys);
            IReadOnlyList<ClientQLAggregate> vecAggregates = DeserializeAggregateArray(GetCosmosElement<CosmosArray>(cosmosObject, Constants.Aggregates));
            return new ClientQLGroupByEnumerableExpression(sourceExpression, vecKeys, vecAggregates);
        }

        private static ClientQLFlattenEnumerableExpression DeserializeFlattenEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            return new ClientQLFlattenEnumerableExpression(sourceExpression);
        }

        private static ClientQLInputEnumerableExpression DeserializeInputEnumerableExpression(CosmosObject cosmosObject)
        {
            return new ClientQLInputEnumerableExpression(RemoveQuotesIfPresent(GetValue<string>(cosmosObject, Constants.Name)));
        }

        private static ClientQLOrderByEnumerableExpression DeserializeOrderByEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetCosmosElement<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            IReadOnlyList<ClientQLOrderByItem> orderByItems = GetValue<IReadOnlyList<ClientQLOrderByItem>>(cosmosObject, Constants.VecItems);
            return new ClientQLOrderByEnumerableExpression(sourceExpression, declaredVariable, orderByItems);
        }

        private static ClientQLScalarAsEnumerableExpression DeserializeScalarAsEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.EnumerationKind), out ClientQLEnumerationKind enumerationKind);
            return new ClientQLScalarAsEnumerableExpression(expression, enumerationKind);
        }

        private static ClientQLSelectEnumerableExpression DeserializeSelectEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetCosmosElement<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
        }

        private static ClientQLSelectManyEnumerableExpression DeserializeSelectManyExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetCosmosElement<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            ClientQLEnumerableExpression selectorExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SelectorExpression));
            return new ClientQLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
        }

        private static ClientQLTakeEnumerableExpression DeserializeTakeEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            int skipValue = GetValue<int>(cosmosObject, Constants.SkipValue);
            int takeExpression = GetValue<int>(cosmosObject, Constants.TakeValue);
            return new ClientQLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
        }

        private static ClientQLWhereEnumerableExpression DeserializeWhereEnumerableExpression(CosmosObject cosmosObject)
        {
            ClientQLEnumerableExpression sourceExpression = DeserializeClientQLEnumerableExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.SourceExpression));
            ClientQLDelegate clientDelegate = DeserializeDelegateExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Delegate));
            return new ClientQLWhereEnumerableExpression(sourceExpression, clientDelegate);
        }

        private static ClientQLScalarExpression DeserializeScalarExpression(CosmosObject cosmosObject)
        {
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.Kind), out ClientQLScalarExpressionKind scalarExpressionKind);
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
                    throw new ArgumentException("blah");
            }
        }

        private static ClientQLArrayCreateScalarExpression DeserializeArrayCreateScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLArrayKind arrayKind = GetValue<ClientQLArrayKind>(cosmosObject, Constants.ArrayKind);
            IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray(GetCosmosElement<CosmosArray>(cosmosObject, Constants.VecItems));
            return new ClientQLArrayCreateScalarExpression(arrayKind, vecItems);
        }

        private static ClientQLArrayIndexerScalarExpression DeserializeArrayIndexerScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            int index = GetValue<int>(cosmosObject, Constants.Index);
            return new ClientQLArrayIndexerScalarExpression(expression, index);
        }

        private static ClientQLBinaryScalarExpression DeserializeBinaryOperatorScalarExpression(CosmosObject cosmosObject)
        {
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.OperatorKind), out ClientQLBinaryScalarOperatorKind operatorKind);
            int maxDepth = TryGetValue<int>(cosmosObject, Constants.MaxDepth);
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.LeftExpression));
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
        }

        private static ClientQLIsOperatorScalarExpression DeserializeIsOperatorScalarExpression(CosmosObject cosmosObject)
        {
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.OperatorKind), out ClientQLIsOperatorKind operatorKind);
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLIsOperatorScalarExpression(operatorKind, expression);
        }

        private static ClientQLLetScalarExpression DeserializeLetScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLVariable declaredVariable = DeserializeClientQLVariable(GetCosmosElement<CosmosObject>(cosmosObject, Constants.DeclaredVariable));
            ClientQLScalarExpression declaredVariableExpression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.DeclaredVariableExpression));
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
        }

        private static ClientQLLiteralScalarExpression DeserializeLiteralScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLLiteral literal = new ClientQLLiteral((ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal);
            return new ClientQLLiteralScalarExpression(literal);
        }

        private static ClientQLMuxScalarExpression DeserializeMuxScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression conditionExpression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.ConditionExpression));
            ClientQLScalarExpression leftExpression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.LeftExpression));
            ClientQLScalarExpression rightExpression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.RightExpression));
            return new ClientQLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
        }

        private static ClientQLObjectCreateScalarExpression DeserializeObjectCreateScalarExpression(CosmosObject cosmosObject)
        {
            string objectKindString = GetValue<string>(cosmosObject, Constants.ObjectKind);
            Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind);
            IReadOnlyList<ClientQLObjectProperty> properties = DeserializeObjectProperties((CosmosArray)cosmosObject[Constants.Properties]);
            return new ClientQLObjectCreateScalarExpression(properties, objectKind);
        }

        private static ClientQLPropertyRefScalarExpression DeserializePropertyRefScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            string propertyName = GetValue<string>(cosmosObject, Constants.PropertyName);
            return new ClientQLPropertyRefScalarExpression(expression, propertyName);
        }

        private static ClientQLSystemFunctionCallScalarExpression DeserializeSystemFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.FunctionKind), out ClientQLBuiltinScalarFunctionKind functionKind);
            IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray(GetCosmosElement<CosmosArray>(cosmosObject, Constants.Arguments));
            return new ClientQLSystemFunctionCallScalarExpression(functionKind, vecArguments);
        }

        private static ClientQLTupleCreateScalarExpression DeserializeTupleCreateScalarExpression(CosmosObject cosmosObject)
        {
            IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray(GetCosmosElement<CosmosArray>(cosmosObject, Constants.Items));
            return new ClientQLTupleCreateScalarExpression(vecItems);
        }

        private static ClientQLTupleItemRefScalarExpression DeserializeTupleItemRefScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            int index = GetValue<int>(cosmosObject, Constants.Index);
            return new ClientQLTupleItemRefScalarExpression(expression, index);
        }

        private static ClientQLUnaryScalarExpression DeserializeUnaryScalarExpression(CosmosObject cosmosObject)
        {
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.OperatorKind), out ClientQLUnaryScalarOperatorKind operatorKind);
            ClientQLScalarExpression expression = DeserializeScalarExpression(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Expression));
            return new ClientQLUnaryScalarExpression(operatorKind, expression);
        }

        private static ClientQLUserDefinedFunctionCallScalarExpression DeserializeUserDefinedFunctionCallScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLFunctionIdentifier identifier = GetValue<ClientQLFunctionIdentifier>(cosmosObject, Constants.Identifier);
            IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray(GetCosmosElement<CosmosArray>(cosmosObject, Constants.VecArguments));
            bool builtin = GetValue<bool>(cosmosObject, Constants.Builtin);
            return new ClientQLUserDefinedFunctionCallScalarExpression(identifier, vecArguments, builtin);
        }

        private static ClientQLVariableRefScalarExpression DeserializeVariableRefScalarExpression(CosmosObject cosmosObject)
        {
            ClientQLVariable variable = DeserializeClientQLVariable(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Variable));
            return new ClientQLVariableRefScalarExpression(variable);
        }

        private static ClientQLDelegate DeserializeDelegateExpression(CosmosObject cosmosObject)
        {
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.Kind), out ClientQLDelegateKind kind);
            ClientQLType type = DeserializeType(GetCosmosElement<CosmosObject>(cosmosObject, Constants.Type));
            return new ClientQLDelegate(kind, type);
        }

        private static ClientQLType DeserializeType(CosmosObject cosmosObject)
        {
            Enum.TryParse(cosmosObject[Constants.Kind].ToString(), out ClientQLTypeKind kind);
            return new ClientQLType(kind);
        }

        private static ClientQLAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            string aggregateKind = RemoveQuotesIfPresent(cosmosObject[Constants.Kind].ToString());
            Enum.TryParse(aggregateKind, out ClientQLAggregateKind kind);
            string operatorKind = null;
            if (cosmosObject[Constants.OperatorKind] != null)
            {
                operatorKind = RemoveQuotesIfPresent(cosmosObject[Constants.OperatorKind].ToString());
            }

            return new ClientQLAggregate(kind, operatorKind);
        }

        private static ClientQLVariable DeserializeClientQLVariable(CosmosObject cosmosObject)
        {
            string name = string.Empty;
            int uniqueId = default;

            if (cosmosObject.TryGetValue(Constants.Name, out CosmosElement nameToken))
            {
                name = RemoveQuotesIfPresent(nameToken.ToString());
            }

            if (cosmosObject.TryGetValue(Constants.UniqueId, out CosmosElement uniqueIdToken))
            {
                if (uniqueIdToken is CosmosNumber)
                {
                    if (int.TryParse(uniqueIdToken.ToString(), out int uniqueIdValue))
                    {
                        uniqueId = uniqueIdValue;
                    }
                }
            }

            return new ClientQLVariable(name, uniqueId);
        }

        private static List<ClientQLObjectProperty> DeserializeObjectProperties(CosmosArray cosmosArray)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            foreach (CosmosElement propertyElement in cosmosArray)
            {
                CosmosObject propertyObject = propertyElement as CosmosObject;
                string objectPropertyName = RemoveQuotesIfPresent(propertyObject[Constants.Name].ToString());
                ClientQLScalarExpression expression = DeserializeScalarExpression(propertyObject[Constants.Expression] as CosmosObject);
                properties.Add(new ClientQLObjectProperty(objectPropertyName, expression));
            }

            return properties;
        }

        private static ClientQLTupleAggregate DeserializeTupleAggregateExpression(CosmosObject cosmosObject)
        {
            string tupleAggregateKind = RemoveQuotesIfPresent(cosmosObject["Kind"].ToString());
            List<ClientQLAggregate> expression = new List<ClientQLAggregate>();
            foreach (CosmosElement propertyElement in cosmosObject[Constants.Items] as CosmosArray)
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
                    ClientQLScalarExpression expression = DeserializeScalarExpression(propertyElement as CosmosObject);
                    expressions.Add(expression);
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
                    string kindString = RemoveQuotesIfPresent(propertyObject[Constants.Kind].ToString());

                    if (kindString.Equals(Constants.Builtin))
                    {
                        aggregateExpression = DeserializeAggregate((CosmosObject)propertyElement);
                    }
                    else if (kindString.Equals(Constants.Tuple))
                    {
                        aggregateExpression = DeserializeTupleAggregateExpression((CosmosObject)propertyElement);
                    }
                    
                    expressions.Add(aggregateExpression);
                }
            }

            return expressions;
        }

        private static T GetValue<T>(CosmosObject token, string expression)
        {
            try
            {
                if (token.TryGetValue(expression, out CosmosElement value))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<T>(value.ToString());
                }
                else
                {
                    throw new ArgumentException("The specified property does not exist in the CosmosObject.", nameof(expression));
                }
            }
            catch (Exception ex)
            {
                string errorMessage = GetExceptionMessageFormat();
                errorMessage += ex.InnerException;

                throw new ArgumentNullException(errorMessage);
            }
        }

        private static T TryGetValue<T>(CosmosObject token, string expression)
        {
            try
            {
                if (token.TryGetValue(expression, out CosmosElement value))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<T>(value.ToString());
                }
                else
                {
                    return default;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = GetExceptionMessageFormat();
                errorMessage += ex.InnerException;

                throw new ArgumentNullException(errorMessage);
            }
        }

        private static T GetCosmosElement<T>(CosmosObject token, string expression)
            where T : CosmosElement
        {
            try
            {
                if (token.TryGetValue(expression, out CosmosElement value))
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                    else
                    {
                        throw new ArgumentException($"The specified property is not of type {typeof(T).Name}.", nameof(expression));
                    }
                }
                else
                {
                    throw new ArgumentNullException("The specified property does not exist in the CosmosObject.", nameof(expression));
                }
            }
            catch (Exception ex)
            {
                string errorMessage = GetExceptionMessageFormat();
                errorMessage += ex.InnerException;

                throw new ArgumentNullException(errorMessage);
            }
        }

        private static string GetExceptionMessageFormat()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            string clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
            
            return $"Exception occurred while deserializing query plan. Version : '{clientSDKVersion}', Exception/Reason : '{1}'.";
        }

        private static string RemoveQuotesIfPresent(string token)
        {
            if (token.StartsWith("\"") && token.EndsWith("\""))
            {
                return token.Substring(1, token.Length - 2);
            }
            else
            {
                return token;
            }
        }

    }
}