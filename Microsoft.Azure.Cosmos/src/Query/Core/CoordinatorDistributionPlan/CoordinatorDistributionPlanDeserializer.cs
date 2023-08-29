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
            public const string Aggregate = "\"Aggregate\"";
            public const string Distinct = "\"Distinct\"";
            public const string GroupBy = "\"GroupBy\"";
            public const string Flatten = "\"Flatten\"";
            public const string Input = "\"Input\"";
            public const string OrderBy = "\"OrderBy\"";
            public const string ScalarAsEnumerable = "\"ScalarAsEnumerable\"";
            public const string Select = "\"Select\"";
            public const string SelectMany = "\"SelectMany\"";
            public const string Take = "\"Take\"";
            public const string Where = "\"Where\"";
            public const string CoordinatorDistributionPlan = "coordinatorDistributionPlan";
            public const string ClientQL = "clientQL";
            public const string SourceExpression = "SourceExpression";
            public const string VecExpression = "\"VecExpression\"";
            public const string VecKeys = "\"VecKeys\"";
            public const string VecAggregates = "\"VecAggregates\"";
            public const string VecItems = "\"VecItems\"";
            public const string DeclaredVariable = "DeclaredVariable";
            public const string EnumerationKind = "\"EnumerationKind\"";
            public const string SelectorExpression = "\"SelectorExpression\"";
            public const string SkipValue = "\"SkipValue\"";
            public const string TakeValue = "\"TakeValue\"";
            public const string Delegate = "\"Delegate\"";
            public const string Kind = "Kind";
            public const string Index = "\"Index\"";
            public const string ArrayKind = "\"ArrayKind\"";
            public const string Expression = "Expression";
            public const string OperatorKind = "\"OperatorKind\"";
            public const string LeftExpression = "\"LeftExpression\"";
            public const string RightExpression = "\"RightExpression\"";
            public const string MaxDepth = "\"MaxDepth\"";
            public const string DeclaredVariableExpression = "\"DeclaredVariableExpression\"";
            public const string ConditionExpression = "\"ConditionExpression\"";
            public const string Properties = "\"Properties\"";
            public const string PropertyName = "\"PropertyName\"";
            public const string FunctionKind = "\"FunctionKind\"";
            public const string VecArguments = "\"VecArguments\"";
            public const string Identifier = "\"Identifier\"";
            public const string Builtin = "\"Builtin\"";
            public const string Type = "\"Type\"";
            public const string Name = "Name";
            public const string UniqueId = "\"UniqueId\"";
            public const string SortOrder = "\"SortOrder\"";
            public const string Variable = "\"Variable\"";
            public const string ObjectKind = "\"ObjectKind\"";
            public const string Items = "\"Items\"";
        }

        public struct Result
        {
            public bool IsSuccess { get; }
            public string ErrorMessage { get; }

            private Result(bool isSuccess, string errorMessage)
            {
                this.IsSuccess = isSuccess;
                this.ErrorMessage = errorMessage;
            }

            public static Result Success => new Result(true, null);

            public static Result Failure(string errorMessage)
            {
                return new Result(false, errorMessage);
            }
        }

        public static Result TryDeserializeCoordinatorDistributionPlan(string jsonString, out CoordinatorDistributionPlan clientQL)
        {
            CosmosObject cosmosObject = CosmosObject.Parse(jsonString);
            clientQL = null;

            if (cosmosObject.TryGetValue(Constants.CoordinatorDistributionPlan, out CosmosElement coordinatorDistributionPlanElement) &&
                coordinatorDistributionPlanElement is CosmosObject coordinatorDistributionPlanObject &&
                coordinatorDistributionPlanObject.TryGetValue(Constants.ClientQL, out CosmosElement clientQLElement))
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)clientQLElement, out ClientQLEnumerableExpression expression);
                clientQL = new CoordinatorDistributionPlan((ClientQLExpression)expression);
                return result;
            }
            else
            {
                return Result.Failure("Invalid Coordinator Distribution Plan");
            }
        }

        private static Result TryDeserializeClientQLEnumerableExpression(CosmosObject cosmosObject, out ClientQLEnumerableExpression expression)
        {
            expression = null;
            if (!cosmosObject.TryGetValue(Constants.Kind, out CosmosElement token))
            {
                return Result.Failure("Invalid Kind Operation in Coordinator Distribution Plan");
            }
            else
            {
                //string output = Regex.Replace(token.ToString(), "\"", string.Empty);
                if (token.ToString().Equals(Constants.Input))
                {
                    Console.WriteLine("Wow");
                }
                else
                {
                    Console.WriteLine(token.ToString());
                    Console.WriteLine(Constants.Input);
                }
                switch (token.ToString())
                {
                    case Constants.Aggregate:
                        Result result = TryDeserializeAggregateEnumerableExpression(cosmosObject, out ClientQLAggregateEnumerableExpression aggregateEnumerableExpression);
                        expression = aggregateEnumerableExpression;
                        return result;
                    case Constants.Distinct:
                        result = TryDeserializeDistinctEnumerableExpression(cosmosObject, out ClientQLDistinctEnumerableExpression distinctEnumerableExpression);
                        expression = distinctEnumerableExpression;
                        return result;
                    case Constants.GroupBy:
                        result = TryDeserializeGroupByEnumerableExpression(cosmosObject, out ClientQLGroupByEnumerableExpression groupByEnumerableExpression);
                        expression = groupByEnumerableExpression;
                        return result;
                    case Constants.Flatten:
                        result = TryDeserializeFlattenEnumerableExpression(cosmosObject, out ClientQLFlattenEnumerableExpression flattenEnumerableExpression);
                        expression = flattenEnumerableExpression;
                        return result;
                    case Constants.Input:
                        result = TryDeserializeInputEnumerableExpression(cosmosObject, out ClientQLInputEnumerableExpression inputEnumerableExpression);
                        expression = inputEnumerableExpression;
                        return result;
                    case Constants.OrderBy:
                        result = TryDeserializeOrderByEnumerableExpression(cosmosObject, out ClientQLOrderByEnumerableExpression orderByEnumerableExpression);
                        expression = orderByEnumerableExpression;
                        return result;
                    case Constants.ScalarAsEnumerable:
                        result = TryDeserializeScalarAsEnumerableExpression(cosmosObject, out ClientQLScalarAsEnumerableExpression scalarAsEnumerableExpression);
                        expression = scalarAsEnumerableExpression;
                        return result;
                    case Constants.Select:
                        result = TryDeserializeSelectEnumerableExpression(cosmosObject, out ClientQLSelectEnumerableExpression selectEnumerableExpression);
                        expression = selectEnumerableExpression;
                        return result;
                    case Constants.SelectMany:
                        result = TryDeserializeSelectManyExpression(cosmosObject, out ClientQLSelectManyEnumerableExpression selectManyEnumerableExpression);
                        expression = selectManyEnumerableExpression;
                        return result;
                    case Constants.Take:
                        result = TryDeserializeTakeEnumerableExpression(cosmosObject, out ClientQLTakeEnumerableExpression takeEnumerableExpression);
                        expression = takeEnumerableExpression;
                        return result;
                    case Constants.Where:
                        result = TryDeserializeWhereEnumerableExpression(cosmosObject, out ClientQLWhereEnumerableExpression whereEnumerableExpression);
                        expression = whereEnumerableExpression;
                        return result;
                    default:
                        return Result.Failure($"Invalid ClientQLExpression kind: {token}");
                }
            }
        }
        
        private static Result TryDeserializeAggregateEnumerableExpression(CosmosObject cosmosObject, out ClientQLAggregateEnumerableExpression aggregateExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                ClientQLAggregate aggregate = DeserializeAggregate((CosmosObject)cosmosObject[Constants.Aggregate]);
                aggregateExpression = new ClientQLAggregateEnumerableExpression(sourceExpression, aggregate);
                return result;
            }
            catch (Exception ex) 
            {
                aggregateExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeDistinctEnumerableExpression(CosmosObject cosmosObject, out ClientQLDistinctEnumerableExpression distinctExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                ClientQLVariable declaredVariable = DeserializeClientQLVariable((CosmosObject)cosmosObject[Constants.DeclaredVariable]);
                IReadOnlyList<ClientQLScalarExpression> vecExpressions = DeserializeScalarExpressionArray((CosmosObject)cosmosObject[Constants.VecExpression]);
                distinctExpression = new ClientQLDistinctEnumerableExpression(sourceExpression, declaredVariable, vecExpressions);
                return result;
            }
            catch (Exception ex)
            {
                distinctExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeGroupByEnumerableExpression(CosmosObject cosmosObject, out ClientQLGroupByEnumerableExpression groupByExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                IReadOnlyList<ClientQLGroupByKey> vecKeys = GetValue<IReadOnlyList<ClientQLGroupByKey>>(cosmosObject, Constants.VecKeys);
                IReadOnlyList<ClientQLAggregate> vecAggregates = GetValue<IReadOnlyList<ClientQLAggregate>>(cosmosObject, Constants.VecAggregates);
                groupByExpression = new ClientQLGroupByEnumerableExpression(sourceExpression, vecKeys, vecAggregates);
                return result;
            }
            catch (Exception ex) 
            {
                groupByExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeFlattenEnumerableExpression(CosmosObject cosmosObject, out ClientQLFlattenEnumerableExpression flattenExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                flattenExpression = new ClientQLFlattenEnumerableExpression(sourceExpression);
                return result;
            }
            catch (Exception ex)
            {
                flattenExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeInputEnumerableExpression(CosmosObject cosmosObject, out ClientQLInputEnumerableExpression inputEnumerableExpression)
        {
            try
            {
                inputEnumerableExpression = new ClientQLInputEnumerableExpression(GetValue<string>(cosmosObject, Constants.Name));
                return Result.Success;
            }
            catch (Exception ex)
            {
                inputEnumerableExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeOrderByEnumerableExpression(CosmosObject cosmosObject, out ClientQLOrderByEnumerableExpression orderByExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                ClientQLVariable declaredVariable = DeserializeClientQLVariable((CosmosObject)cosmosObject[Constants.DeclaredVariable]);
                IReadOnlyList<ClientQLOrderByItem> orderByItems = GetValue<IReadOnlyList<ClientQLOrderByItem>>(cosmosObject, Constants.VecItems);
                orderByExpression = new ClientQLOrderByEnumerableExpression(sourceExpression, declaredVariable, orderByItems);
                return result;
            }
            catch (Exception ex)
            {
                orderByExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeScalarAsEnumerableExpression(CosmosObject cosmosObject, out ClientQLScalarAsEnumerableExpression scalarAsExpression)
        {
            try
            {
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                if (Enum.TryParse(GetValue<string>(cosmosObject, Constants.EnumerationKind), out ClientQLEnumerationKind enumerationKind))
                {
                    scalarAsExpression = new ClientQLScalarAsEnumerableExpression(expression, enumerationKind);
                    return result;
                }
                else
                {
                    scalarAsExpression = null;
                    return Result.Failure($"Invalid Coordinator Distribution Plan");
                }
            }
            catch (Exception ex)
            {
                scalarAsExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeSelectEnumerableExpression(CosmosObject cosmosObject, out ClientQLSelectEnumerableExpression selectEnumerableExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                ClientQLVariable declaredVariable = DeserializeClientQLVariable((CosmosObject)cosmosObject[Constants.DeclaredVariable]);
                TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                selectEnumerableExpression = new ClientQLSelectEnumerableExpression(sourceExpression, declaredVariable, expression);
                return result;
            }
            catch (Exception ex) 
            {
                selectEnumerableExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeSelectManyExpression(CosmosObject cosmosObject, out ClientQLSelectManyEnumerableExpression selectManyEnumerableExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                ClientQLVariable declaredVariable = DeserializeClientQLVariable((CosmosObject)cosmosObject[Constants.DeclaredVariable]);
                Result secondResult = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SelectorExpression], out ClientQLEnumerableExpression selectorExpression);
                selectManyEnumerableExpression = new ClientQLSelectManyEnumerableExpression(sourceExpression, declaredVariable, selectorExpression);
                if (result.IsSuccess && secondResult.IsSuccess)
                {
                    return Result.Success;
                }
                else
                {
                    return Result.Failure("Invalid Select Many Expression");
                }
            }
            catch (Exception ex) 
            {
                selectManyEnumerableExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeTakeEnumerableExpression(CosmosObject cosmosObject, out ClientQLTakeEnumerableExpression takeEnumerableExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                int skipValue = GetValue<int>(cosmosObject, Constants.SkipValue);
                int takeExpression = GetValue<int>(cosmosObject, Constants.TakeValue);
                takeEnumerableExpression = new ClientQLTakeEnumerableExpression(sourceExpression, skipValue, takeExpression);
                return result;
            }
            catch (Exception ex) 
            {
                takeEnumerableExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeWhereEnumerableExpression(CosmosObject cosmosObject, out ClientQLWhereEnumerableExpression whereEnumerableExpression)
        {
            try
            {
                Result result = TryDeserializeClientQLEnumerableExpression((CosmosObject)cosmosObject[Constants.SourceExpression], out ClientQLEnumerableExpression sourceExpression);
                ClientQLDelegate clientDelegate = TryDeserializeDelegateExpression((CosmosObject)cosmosObject[Constants.Delegate]);
                whereEnumerableExpression = new ClientQLWhereEnumerableExpression(sourceExpression, clientDelegate);
                return result;
            }
            catch (Exception ex) 
            {
                whereEnumerableExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeScalarExpression(CosmosObject cosmosObject, out ClientQLScalarExpression scalarExpression)
        {
            scalarExpression = null;
            Enum.TryParse(GetValue<string>(cosmosObject, Constants.Kind), out ClientQLScalarExpressionKind scalarExpressionKind);

            switch (scalarExpressionKind)
            {
                case ClientQLScalarExpressionKind.ArrayCreate:
                    Result result = TryDeserializeArrayCreateScalarExpression(cosmosObject, out ClientQLArrayCreateScalarExpression arrayCreateScalarExpression);
                    scalarExpression = arrayCreateScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.ArrayIndexer:
                    result = TryDeserializeArrayIndexerScalarExpression(cosmosObject, out ClientQLArrayIndexerScalarExpression arrayIndexerScalarExpression);
                    scalarExpression = arrayIndexerScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.BinaryOperator:
                    result = TryDeserializeBinaryOperatorScalarExpression(cosmosObject, out ClientQLBinaryScalarExpression binaryScalarExpression);
                    scalarExpression = binaryScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.IsOperator:
                    result = TryDeserializeIsOperatorScalarExpression(cosmosObject, out ClientQLIsOperatorScalarExpression isOperatorScalarExpression);
                    scalarExpression = isOperatorScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.Let:
                    result = TryDeserializeLetScalarExpression(cosmosObject, out ClientQLLetScalarExpression letScalarExpression);
                    scalarExpression = letScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.Literal:
                    result = TryDeserializeLiteralScalarExpression(cosmosObject, out ClientQLLiteralScalarExpression literalScalarExpression);
                    scalarExpression = literalScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.Mux:
                    result = TryDeserializeMuxScalarExpression(cosmosObject, out ClientQLMuxScalarExpression muxScalarExpression);
                    scalarExpression = muxScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.ObjectCreate:
                    result = TryDeserializeObjectCreateScalarExpression(cosmosObject, out ClientQLObjectCreateScalarExpression objectCreateScalarExpression);
                    scalarExpression = objectCreateScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.PropertyRef:
                    result = TryDeserializePropertyRefScalarExpression(cosmosObject, out ClientQLPropertyRefScalarExpression propertyRefScalarExpression);
                    scalarExpression = propertyRefScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.SystemFunctionCall:
                    result = TryDeserializeSystemFunctionCallScalarExpression(cosmosObject, out ClientQLSystemFunctionCallScalarExpression systemFunctionCallScalarExpression);
                    scalarExpression = systemFunctionCallScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.TupleCreate:
                    result = TryDeserializeTupleCreateScalarExpression(cosmosObject, out ClientQLTupleCreateScalarExpression tupleCreateScalarExpression);
                    scalarExpression = tupleCreateScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.TupleItemRef:
                    result = TryDeserializeTupleItemRefScalarExpression(cosmosObject, out ClientQLTupleItemRefScalarExpression tupleItemRefScalarExpression);
                    scalarExpression = tupleItemRefScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.UnaryOperator:
                    result = TryDeserializeUnaryScalarExpression(cosmosObject, out ClientQLUnaryScalarExpression unaryScalarExpression);
                    scalarExpression = unaryScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.UserDefinedFunctionCall:
                    result = TryDeserializeUserDefinedFunctionCallScalarExpression(cosmosObject, out ClientQLUserDefinedFunctionCallScalarExpression userDefinedFunctionCallScalarExpression);
                    scalarExpression = userDefinedFunctionCallScalarExpression;
                    return result;
                case ClientQLScalarExpressionKind.VariableRef:
                    result = TryDeserializeVariableRefScalarExpression(cosmosObject, out ClientQLVariableRefScalarExpression variableRefScalarExpression);
                    scalarExpression = variableRefScalarExpression;
                    return result;
                default:
                    return Result.Failure($"Invalid ClientQLScalarExpressionKind: {scalarExpressionKind}");
            }
        }

        private static Result TryDeserializeArrayCreateScalarExpression(CosmosObject cosmosObject, out ClientQLArrayCreateScalarExpression arrayCreateScalarExpression)
        {
            try
            {
                ClientQLArrayKind arrayKind = GetValue<ClientQLArrayKind>(cosmosObject, Constants.ArrayKind);
                IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray((CosmosObject)cosmosObject[Constants.VecItems]);
                arrayCreateScalarExpression = new ClientQLArrayCreateScalarExpression(arrayKind, vecItems);
                return Result.Success;
            }
            catch (Exception ex) 
            {
                arrayCreateScalarExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeArrayIndexerScalarExpression(CosmosObject cosmosObject, out ClientQLArrayIndexerScalarExpression arrayIndexerScalarExpression)
        {
            try
            {
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                int index = GetValue<int>(cosmosObject, Constants.Index);
                arrayIndexerScalarExpression = new ClientQLArrayIndexerScalarExpression(expression, index);
                return result;
            }
            catch (Exception ex)
            { 
                arrayIndexerScalarExpression = null;
                return Result.Failure(ex.ToString());
            }
        }

        private static Result TryDeserializeBinaryOperatorScalarExpression(CosmosObject cosmosObject, out ClientQLBinaryScalarExpression binaryScalarExpression)
        {
            if (Enum.TryParse(GetValue<string>(cosmosObject, Constants.OperatorKind), out ClientQLBinaryScalarOperatorKind operatorKind))
            {
                int maxDepth = GetValue<int>(cosmosObject, Constants.MaxDepth);
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.LeftExpression], out ClientQLScalarExpression leftExpression);
                Result secondResult = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.RightExpression], out ClientQLScalarExpression rightExpression);
                binaryScalarExpression = new ClientQLBinaryScalarExpression(operatorKind, maxDepth, leftExpression, rightExpression);
                if (result.IsSuccess && secondResult.IsSuccess)
                {
                    return Result.Success;
                }
                else
                {
                    return Result.Failure($"Invalid Binary Operator Expression: {operatorKind}");
                }
            }
            else
            {
                binaryScalarExpression = null;
                return Result.Failure($"Invalid Binary Operator Expression");
            }
        }

        private static Result TryDeserializeIsOperatorScalarExpression(CosmosObject cosmosObject, out ClientQLIsOperatorScalarExpression isOperatorScalarExpression)
        {
            if (Enum.TryParse(GetValue<string>(cosmosObject, Constants.OperatorKind), out ClientQLIsOperatorKind operatorKind))
            {
                try 
                {
                    Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                    isOperatorScalarExpression = new ClientQLIsOperatorScalarExpression(operatorKind, expression);
                    return result;
                }
                catch (Exception ex) 
                {
                    isOperatorScalarExpression = null;
                    return Result.Failure(ex.Message);
                }
            }
            else
            {
                isOperatorScalarExpression = null;
                return Result.Failure($"Invalid Operator Scalar Expression: {operatorKind}");
            }
        }

        private static Result TryDeserializeLetScalarExpression(CosmosObject cosmosObject, out ClientQLLetScalarExpression letScalarExpression)
        {
            try
            {
                ClientQLVariable declaredVariable = DeserializeClientQLVariable((CosmosObject)cosmosObject[Constants.DeclaredVariable]);
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.DeclaredVariableExpression], out ClientQLScalarExpression declaredVariableExpression);
                Result secondResult = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                letScalarExpression = new ClientQLLetScalarExpression(declaredVariable, declaredVariableExpression, expression);
                if (result.IsSuccess && secondResult.IsSuccess)
                {
                    return Result.Success;
                }
                else
                {
                    return Result.Failure($"Invalid Let Operator Expression");
                }
            }
            catch (Exception ex)
            { 
                letScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static Result TryDeserializeLiteralScalarExpression(CosmosObject cosmosObject, out ClientQLLiteralScalarExpression literalScalarExpression)
        {
            try
            {
                ClientQLLiteral literal = new ClientQLLiteral((ClientQLLiteralKind)ClientQLScalarExpressionKind.Literal);
                literalScalarExpression = new ClientQLLiteralScalarExpression(literal);
                return Result.Success;
            }
            catch (Exception ex)
            { 
                literalScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static Result TryDeserializeMuxScalarExpression(CosmosObject cosmosObject, out ClientQLMuxScalarExpression muxScalarExpression)
        {
            try
            {
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.ConditionExpression], out ClientQLScalarExpression conditionExpression);
                Result secondResult = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.LeftExpression], out ClientQLScalarExpression leftExpression);
                Result thirdResult = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.RightExpression], out ClientQLScalarExpression rightExpression);
                muxScalarExpression = new ClientQLMuxScalarExpression(conditionExpression, leftExpression, rightExpression);
                if (result.IsSuccess && secondResult.IsSuccess && thirdResult.IsSuccess)
                {
                    return Result.Success;
                }
                else
                {
                    return Result.Failure($"Invalid Mux Operator Expression");
                }
            }
            catch (Exception ex)
            {
                muxScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static Result TryDeserializeObjectCreateScalarExpression(CosmosObject cosmosObject, out ClientQLObjectCreateScalarExpression objectCreateScalarExpression)
        {
            try
            {
                string objectKindString = GetValue<string>(cosmosObject, Constants.ObjectKind);
                if (!Enum.TryParse(objectKindString, out ClientQLObjectKind objectKind))
                {
                    objectCreateScalarExpression = null;
                    return Result.Failure($"Invalid ClientQLObjectKind: {objectKindString}");
                }

                ValidateArrayProperty(cosmosObject, "Properties");
                IReadOnlyList<ClientQLObjectProperty> properties = DeserializeObjectProperties((CosmosObject)cosmosObject[Constants.Properties]);
                objectCreateScalarExpression = new ClientQLObjectCreateScalarExpression(properties, objectKind);
                return Result.Success;
            }
            catch (Exception ex) 
            { 
                objectCreateScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static Result TryDeserializePropertyRefScalarExpression(CosmosObject cosmosObject, out ClientQLPropertyRefScalarExpression propertyRefScalarExpression)
        {
            try
            {
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                string propertyName = GetValue<string>(cosmosObject, Constants.PropertyName);
                propertyRefScalarExpression = new ClientQLPropertyRefScalarExpression(expression, propertyName);
                return result;
            }
            catch (Exception ex)
            {
                propertyRefScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static Result TryDeserializeSystemFunctionCallScalarExpression(CosmosObject cosmosObject, out ClientQLSystemFunctionCallScalarExpression systemFunctionCallScalarExpression)
        {
            if (Enum.TryParse(GetValue<string>(cosmosObject, Constants.FunctionKind), out ClientQLBuiltinScalarFunctionKind functionKind))
            {
                try
                {
                    IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray((CosmosObject)cosmosObject[Constants.VecArguments]);
                    systemFunctionCallScalarExpression = new ClientQLSystemFunctionCallScalarExpression(functionKind, vecArguments);
                    return Result.Success;
                }
                catch (Exception ex)
                {
                    systemFunctionCallScalarExpression = null;
                    return Result.Failure(ex.Message);
                }
            }
            else
            {
                systemFunctionCallScalarExpression = null;
                return Result.Failure($"Invalid System Function Call Expression: {functionKind}");
            }
        }

        private static Result TryDeserializeTupleCreateScalarExpression(CosmosObject cosmosObject, out ClientQLTupleCreateScalarExpression tupleCreateScalarExpression)
        {
            try
            {
                IReadOnlyList<ClientQLScalarExpression> vecItems = DeserializeScalarExpressionArray((CosmosObject)cosmosObject[Constants.Items]);
                tupleCreateScalarExpression = new ClientQLTupleCreateScalarExpression(vecItems);
                return Result.Success;
            }
            catch (Exception ex) 
            {
                tupleCreateScalarExpression = null;
                return Result.Failure(ex.Message);
            }
            
        }

        private static Result TryDeserializeTupleItemRefScalarExpression(CosmosObject cosmosObject, out ClientQLTupleItemRefScalarExpression tupleItemRefScalarExpression)
        {
            try
            {
                Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                int index = GetValue<int>(cosmosObject, Constants.Index);
                tupleItemRefScalarExpression = new ClientQLTupleItemRefScalarExpression(expression, index);
                return result;
            }
            catch (Exception ex) 
            {
                tupleItemRefScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static Result TryDeserializeUnaryScalarExpression(CosmosObject cosmosObject, out ClientQLUnaryScalarExpression unaryScalarExpression)
        {
            if (Enum.TryParse(GetValue<string>(cosmosObject, Constants.OperatorKind), out ClientQLUnaryScalarOperatorKind operatorKind))
            {
                try
                {
                    Result result = TryDeserializeScalarExpression((CosmosObject)cosmosObject[Constants.Expression], out ClientQLScalarExpression expression);
                    unaryScalarExpression = new ClientQLUnaryScalarExpression(operatorKind, expression);
                    return result;
                }
                catch (Exception ex)
                {
                    unaryScalarExpression = null;
                    return Result.Failure(ex.Message);
                }
            }
            else
            {
                unaryScalarExpression = null;
                return Result.Failure("Invalid Unary Scalar Expression");
            }
        }

        private static Result TryDeserializeUserDefinedFunctionCallScalarExpression(CosmosObject cosmosObject, out ClientQLUserDefinedFunctionCallScalarExpression userDefinedFunctionCallScalarExpression)
        {
            ClientQLFunctionIdentifier identifier = GetValue<ClientQLFunctionIdentifier>(cosmosObject, Constants.Identifier);
            ValidateArrayProperty(cosmosObject, Constants.VecArguments);
            IReadOnlyList<ClientQLScalarExpression> vecArguments = DeserializeScalarExpressionArray((CosmosObject)cosmosObject[Constants.VecArguments]);
            bool builtin = GetValue<bool>(cosmosObject, Constants.Builtin);
            userDefinedFunctionCallScalarExpression = new ClientQLUserDefinedFunctionCallScalarExpression(identifier, vecArguments, builtin);
            return Result.Success;
        }

        private static Result TryDeserializeVariableRefScalarExpression(CosmosObject cosmosObject, out ClientQLVariableRefScalarExpression variableRefScalarExpression)
        {
            try
            {
                ClientQLVariable variable = DeserializeClientQLVariable((CosmosObject)cosmosObject[Constants.Variable]);
                variableRefScalarExpression = new ClientQLVariableRefScalarExpression(variable);
                return Result.Success;
            }
            catch (Exception ex)
            {
                variableRefScalarExpression = null;
                return Result.Failure(ex.Message);
            }
        }

        private static ClientQLDelegate TryDeserializeDelegateExpression(CosmosObject cosmosObject)
        {
            if (Enum.TryParse(GetValue<string>(cosmosObject, Constants.Kind), out ClientQLDelegateKind kind))
            {
                ClientQLType type = DeserializeType((CosmosObject)cosmosObject[Constants.Type]);
                return new ClientQLDelegate(kind, type);
            }
            else
            {
                throw new NotSupportedException($"Invalid Delegate Expression: {kind}");
            }
        }

        private static ClientQLType DeserializeType(CosmosObject cosmosObject)
        {
            if (Enum.TryParse(cosmosObject[Constants.Kind].ToString(), out ClientQLTypeKind kind))
            {
                return new ClientQLType(kind);
            }
            else
            {
                throw new NotSupportedException($"Invalid Type Expression: {kind}");
            }
        }

        private static ClientQLAggregate DeserializeAggregate(CosmosObject cosmosObject)
        {
            if (Enum.TryParse(cosmosObject[Constants.Kind].ToString(), out ClientQLAggregateKind kind))
            {
                string operatorKind = null;
                if (cosmosObject[Constants.OperatorKind] != null)
                {
                    operatorKind = cosmosObject[Constants.OperatorKind].ToString();
                }

                return new ClientQLAggregate(kind, operatorKind);
            }
            else
            {
                throw new NotSupportedException($"Invalid Aggregate Expression: {kind}");
            }
        }

        private static ClientQLVariable DeserializeClientQLVariable(CosmosObject cosmosObject)
        {
            string name;
            int uniqueId = default;

            name = cosmosObject.TryGetValue(Constants.Name, out CosmosElement nameToken)
                ? nameToken.ToString()
                : throw new NotSupportedException($"Invalid Aggregate Expression: {nameToken.GetType()}");

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

        private static List<ClientQLObjectProperty> DeserializeObjectProperties(CosmosObject cosmosObject)
        {
            List<ClientQLObjectProperty> properties = new List<ClientQLObjectProperty>();
            foreach (KeyValuePair<string, CosmosElement> propertyElement in cosmosObject)
            {
                CosmosObject propertyObject = propertyElement.Value as CosmosObject;
                string name = propertyObject[Constants.Name].ToString();
                TryDeserializeScalarExpression(propertyObject[Constants.Expression] as CosmosObject, out ClientQLScalarExpression expression);
                properties.Add(new ClientQLObjectProperty(name, expression));
            }

            return properties;
        }

        private static List<ClientQLScalarExpression> DeserializeScalarExpressionArray(CosmosObject cosmosObject)
        {
            List<ClientQLScalarExpression> expressions = new List<ClientQLScalarExpression>();
            if (cosmosObject != null)
            {
                foreach (KeyValuePair<string, CosmosElement> property in cosmosObject)
                {
                    TryDeserializeScalarExpression(property.Value as CosmosObject, out ClientQLScalarExpression expression);
                    expressions.Add(expression);
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

        private static void ValidateArrayProperty(CosmosObject cosmosObject, string property)
        {
            string errorMessage;
            if (cosmosObject[property] == null)
            {
                string nullErrorMessage = $"{property} could not be found in the deserialized plan.";
                errorMessage = GetExceptionMessageFormat();
                throw new ArgumentNullException(errorMessage + nullErrorMessage);
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