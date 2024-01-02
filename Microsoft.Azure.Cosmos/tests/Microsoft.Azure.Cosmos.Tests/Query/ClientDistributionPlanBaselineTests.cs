namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan;
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ClientDistributionPlanBaselineTests : BaselineTests<ClientDistributionPlanBaselineTests.ClientDistributionPlanTestInput, ClientDistributionPlanBaselineTests.ClientDistributionPlanTestOutput>
    {
        [TestMethod]
        [Owner("akotalwar")]
        public void TestClientDistributionPlanDeserialization()
        {
            List<ClientDistributionPlanTestInput> testVariations = new List<ClientDistributionPlanTestInput>
            {
                // Enumerable Expression
                CreateInput(
                    description: @"Input Expression",
                    clientPlanJson: "{\"clientDistributionPlan\": {\"Cql\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}"),
                
                CreateInput(
                    description: @"Distinct-Select",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"a\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } }}"),
                
                CreateInput(
                    description: @"GroupBy",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 2 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"a\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 2 } }, \"Index\": 0 } }, { \"Name\": \"$1\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 2 } }, \"Index\": 1 } } ] }, \"SourceExpression\": { \"Kind\": \"GroupBy\", \"KeyCount\": 1, \"Aggregates\": [ { \"Kind\": \"Builtin\", \"OperatorKind\": \"Max\" } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 9 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 9 } }, \"Index\": 0 }, { \"Kind\": \"Mux\", \"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"NotEqual\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 9 } }, \"Index\": 1 }, \"Index\": 1 }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 0 } } }, \"RightExpression\": { \"Kind\": \"UnaryOperator\", \"OperatorKind\": \"Not\", \"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 9 } }, \"Index\": 1 }, \"Index\": 0 } ] } } }, \"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [] } }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 9 } }, \"Index\": 1 }, \"Index\": 0 } } ] }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 8 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 1 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 2 } ] } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"Aggregate",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 2 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"$1\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 2 } } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Max\" }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 8 }, \"Expression\": { \"Kind\": \"Mux\", \"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"NotEqual\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 1 }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 0 } } }, \"RightExpression\": { \"Kind\": \"UnaryOperator\", \"OperatorKind\": \"Not\", \"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 0 } ] } } }, \"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [] } }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 0 } }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 7 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 1 } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"SelectMany - ScalarAsEnumerable - Where",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 16 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"s\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16 } } } ] }, \"SourceExpression\": { \"Kind\": \"SelectMany\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 7 }, \"SelectorExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Count\" }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 9 }, \"Expression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 1 } }, \"SourceExpression\": { \"Kind\": \"Where\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 9 }, \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 9 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 435 } } }, \"SourceExpression\": { \"Kind\": \"ScalarAsEnumerable\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"EnumerationKind\": \"ArrayItems\" } } } }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 7 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 19 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 19 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"Take",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Take\", \"SkipValue\": 2, \"TakeValue\": 5, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 4 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"a\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 4 } } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 4 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 4 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 7 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                // Scalar Expression
                CreateInput(
                    description: @"ArrayCreate - ArrayIndexer - ObjectCreate - TupleCreate - VariableRef",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"arr\", \"Expression\": { \"Kind\": \"ArrayCreate\", \"ArrayKind\": \"Array\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 1 } ] } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 1 } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 1 }, \"Index\": 0 } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } }}"),
                
                CreateInput(
                    description: @"BinaryOperator",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 1 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 1 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"$1\", \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 0 }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 1 } } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 1 } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 1 }, \"Index\": 0 } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"Literal",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 1 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 1 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"EqualsFive\", \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 5 } } } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"SystemFunctionCall",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 1 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 1 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"$1\", \"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Integer\", \"Arguments\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ] } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"Unary",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 1 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 1 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"$1\", \"Expression\": { \"Kind\": \"UnaryOperator\", \"OperatorKind\": \"Not\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"UserDefinedFunction",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 1 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 1 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"aUpper\", \"Expression\": { \"Kind\": \"UserDefinedFunctionCall\", \"Identifier\": { \"Name\": \"toUpper\" }, \"Arguments\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"Builtin\": false } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),

                // Additional Cases
                CreateInput(
                    description: @"Multiple Aggregates",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 2 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"sum_a\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 2 } }, \"Index\": 0 } }, { \"Name\": \"sum_b\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 2 } }, \"Index\": 1 } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Tuple\", \"Items\": [ { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" }, { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" } ] }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 8 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"Mux\", \"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"NotEqual\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 0 }, \"Index\": 1 }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 0 } } }, \"RightExpression\": { \"Kind\": \"UnaryOperator\", \"OperatorKind\": \"Not\", \"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 0 }, \"Index\": 0 } ] } } }, \"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [] } }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 0 }, \"Index\": 0 } }, { \"Kind\": \"Mux\", \"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"NotEqual\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 1 }, \"Index\": 1 }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 0 } } }, \"RightExpression\": { \"Kind\": \"UnaryOperator\", \"OperatorKind\": \"Not\", \"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 1 }, \"Index\": 0 } ] } } }, \"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [] } }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 8 } }, \"Index\": 1 }, \"Index\": 0 } } ] }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 7 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 1 } ] }, { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 2 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 7 } }, \"Index\": 3 } ] } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } }}"),
                
                CreateInput(
                    description: @"Distinct with Where",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"FirstName\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 0 } }, { \"Name\": \"LastName\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 1 } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } }, \"Index\": 1 } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 1 }, \"Index\": 0 } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } }}"),
                
                CreateInput(
                    description: @"Complex Query",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 69 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"count\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 2 } }, { \"Name\": \"a\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 0 } }, { \"Name\": \"b\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 1 } }, { \"Name\": \"d\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 4 } }, { \"Name\": \"avg_c\", \"Expression\": { \"Kind\": \"Mux\", \"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 3 }, \"Index\": 1 }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 0 } } }, \"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Undefined\" } }, \"RightExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Divide\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 3 }, \"Index\": 0 }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 69 } }, \"Index\": 3 }, \"Index\": 1 } } } } ] }, \"SourceExpression\": { \"Kind\": \"SelectMany\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 36 }, \"SelectorExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 67 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 2 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 3 }, { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 67 } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Array\" }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 64 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"count\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 64 } }, \"Index\": 7 } }, { \"Name\": \"min\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 64 } }, \"Index\": 8 } }, { \"Name\": \"max\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 64 } }, \"Index\": 9 } }, { \"Name\": \"a\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 64 } }, \"Index\": 4 } } ] }, \"SourceExpression\": { \"Kind\": \"SelectMany\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 58 }, \"SelectorExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 62 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 2 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 3 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 4 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 5 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 6 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 7 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 8 }, { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 62 } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Max\" }, \"SourceExpression\": { \"Kind\": \"Where\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 60 }, \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Or\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 60 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 435 } } }, \"RightExpression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Array_Contains\", \"Arguments\": [ { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [ { \"Kind\": \"String\", \"Value\": \"First\" }, { \"Kind\": \"String\", \"Value\": \"Second\" } ] } }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 5 } ] } }, \"SourceExpression\": { \"Kind\": \"ScalarAsEnumerable\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 58 } }, \"Index\": 0 }, \"EnumerationKind\": \"ArrayItems\" } } } }, \"SourceExpression\": { \"Kind\": \"SelectMany\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 52 }, \"SelectorExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 56 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 2 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 3 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 4 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 5 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 6 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 7 }, { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 56 } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Min\" }, \"SourceExpression\": { \"Kind\": \"Where\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 54 }, \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Or\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 54 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 435 } } }, \"RightExpression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Array_Contains\", \"Arguments\": [ { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [ { \"Kind\": \"String\", \"Value\": \"First\" }, { \"Kind\": \"String\", \"Value\": \"Second\" } ] } }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 5 } ] } }, \"SourceExpression\": { \"Kind\": \"ScalarAsEnumerable\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 52 } }, \"Index\": 0 }, \"EnumerationKind\": \"ArrayItems\" } } } }, \"SourceExpression\": { \"Kind\": \"SelectMany\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 46 }, \"SelectorExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 50 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 2 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 3 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 4 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 5 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 6 }, { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 50 } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Count\" }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 48 }, \"Expression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 1 } }, \"SourceExpression\": { \"Kind\": \"Where\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 48 }, \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Or\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 48 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 435 } } }, \"RightExpression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Array_Contains\", \"Arguments\": [ { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [ { \"Kind\": \"String\", \"Value\": \"First\" }, { \"Kind\": \"String\", \"Value\": \"Second\" } ] } }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 5 } ] } }, \"SourceExpression\": { \"Kind\": \"ScalarAsEnumerable\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 46 } }, \"Index\": 0 }, \"EnumerationKind\": \"ArrayItems\" } } } } }, \"SourceExpression\": { \"Kind\": \"SelectMany\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 39 }, \"SelectorExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 44 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 2 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 3 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 4 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 5 }, { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 44 } } ] }, \"SourceExpression\": { \"Kind\": \"Where\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 44 }, \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 44 } }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Any\" }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 41 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"a2\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 41 } } } ] }, \"SourceExpression\": { \"Kind\": \"Where\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 41 }, \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 41 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 425 } } }, \"SourceExpression\": { \"Kind\": \"ScalarAsEnumerable\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 39 } }, \"Index\": 0 }, \"EnumerationKind\": \"ArrayItems\" } } } } } }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v1\", \"UniqueId\": 38 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 2 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 3 }, { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v1\", \"UniqueId\": 38 } }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 1 } ] }, \"SourceExpression\": { \"Kind\": \"ScalarAsEnumerable\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 36 } }, \"Index\": 0 }, \"EnumerationKind\": \"ArrayItems\" } } } } } } } } }, \"SourceExpression\": { \"Kind\": \"GroupBy\", \"KeyCount\": 2, \"Aggregates\": [ { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" }, { \"Kind\": \"Tuple\", \"Items\": [ { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" }, { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" } ] } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 76 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 0 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 1 }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 2 }, { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"Mux\", \"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"NotEqual\", \"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 3 }, \"Index\": 0 }, \"Index\": 1 }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 0 } } }, \"RightExpression\": { \"Kind\": \"UnaryOperator\", \"OperatorKind\": \"Not\", \"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [ { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 3 }, \"Index\": 0 }, \"Index\": 0 } ] } } }, \"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Array\", \"Items\": [] } }, \"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 3 }, \"Index\": 0 }, \"Index\": 0 } }, { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 76 } }, \"Index\": 3 }, \"Index\": 1 } ] } ] }, \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 75 }, \"Expression\": { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 75 } }, \"Index\": 0 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 75 } }, \"Index\": 1 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 75 } }, \"Index\": 2 }, { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"TupleCreate\", \"Items\": [ { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 75 } }, \"Index\": 3 }, \"Index\": 0 }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 75 } }, \"Index\": 4 } ] }, { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 75 } }, \"Index\": 5 } ] } ] }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } } } }}"),

                CreateInput(
                    description: @"Count plus five",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 2 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"count_a_plus_five\", \"Expression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Add\", \"LeftExpression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 2 } }, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 5 } } } } ] }, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } }}"),

                CreateInput(
                    description: @"Distinct-Select",
                    clientPlanJson: "{\"clientDistributionPlan\": { \"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [ { \"Name\": \"a\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } } ] }, \"SourceExpression\": { \"Kind\": \"Distinct\", \"DeclaredVariable\": { \"Name\": \"s0\", \"UniqueId\": 3 }, \"Expressions\": [ { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"s0\", \"UniqueId\": 3 } } ], \"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": { \"Name\": \"v0\", \"UniqueId\": 6 }, \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"ArrayIndexer\", \"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": { \"Name\": \"v0\", \"UniqueId\": 6 } }, \"Index\": 0 }, \"Index\": 0 }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" } } } } }}"),
            };

            this.ExecuteTestSuite(testVariations);
        }


        private static ClientDistributionPlanTestInput CreateInput(
            string description,
            string clientPlanJson)
        {
            return new ClientDistributionPlanTestInput(description, clientPlanJson);
        }

        public override ClientDistributionPlanTestOutput ExecuteTest(ClientDistributionPlanTestInput input)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };

            ClientDistributionPlan distributionPlan = ClientDistributionPlanDeserializer.DeserializeClientDistributionPlan(input.ClientPlanJson);
            DistributionPlanWriter visitor = new DistributionPlanWriter();
            distributionPlan.Cql.Accept(visitor);
            string serializedDistributionPlan = JsonConvert.SerializeObject(distributionPlan, settings);
            Console.WriteLine(visitor.SerializedOutput);
            return new ClientDistributionPlanTestOutput(serializedDistributionPlan);
        }

        public sealed class ClientDistributionPlanTestOutput : BaselineTestOutput
        {
            public ClientDistributionPlanTestOutput(string serializedclientPlanJson)
            {
                this.SerializedclientPlanJson = serializedclientPlanJson;
            }

            public string SerializedclientPlanJson { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                JObject jObject = JObject.Parse(this.SerializedclientPlanJson);
                string jsonString = jObject.ToString();
                xmlWriter.WriteStartElement("SerializedClientPlanJson");
                xmlWriter.WriteString(jsonString);
                xmlWriter.WriteEndElement();
            }
        }

        public sealed class ClientDistributionPlanTestInput : BaselineTestInput
        {
            internal string ClientPlanJson { get; set; }

            internal ClientDistributionPlanTestInput(
                string description,
                string clientPlanJson)
                : base(description)
            {
                this.ClientPlanJson = clientPlanJson;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString("Description", this.Description);
                xmlWriter.WriteElementString("ClientDistributionPlanJson", this.ClientPlanJson);
            }
        }
    }

    public class DistributionPlanWriter : ICqlVisitor
    {
        private StringBuilder output = new StringBuilder();

        public string SerializedOutput => "clientDistributionPlan: { Cql: { " + this.output.ToString() + " } }";

        void ICqlVisitor.Visit(CqlAggregate cqlAggregate)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlAggregateEnumerableExpression cqlAggregateEnumerableExpression)
        {
            this.output.Append("Kind: Aggregate, ");
            this.output.Append("Aggregate: ");
            cqlAggregateEnumerableExpression.Aggregate.Accept(this);
            this.output.Append(" SourceExpression: { ");
            cqlAggregateEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append("}");
        }

        void ICqlVisitor.Visit(CqlAggregateKind cqlAggregateKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlAggregateOperatorKind cqlAggregateOperatorKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlArrayCreateScalarExpression cqlArrayCreateScalarExpression)
        {
            this.output.Append("{Kind: ArrayCreate, ");
            string arrayKind = cqlArrayCreateScalarExpression.ArrayKind;
            this.output.Append($"ArrayKind: {arrayKind}, ");
            this.output.Append("Items: [");
            foreach (CqlScalarExpression item in cqlArrayCreateScalarExpression.Items)
            {
                item.Accept(this);
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlArrayIndexerScalarExpression cqlArrayIndexerScalarExpression)
        {
            this.output.Append("Kind: ArrayIndexer, ");
            this.output.Append("Expression: { ");
            cqlArrayIndexerScalarExpression.Expression.Accept(this);
            this.output.Append(" }, ");
            ulong index = cqlArrayIndexerScalarExpression.Index;
            this.output.Append($"Index: {index}");
        }

        void ICqlVisitor.Visit(CqlArrayLiteral cqlArrayLiteral)
        {
            this.output.Append("Items: [");
            foreach (CqlLiteral item in cqlArrayLiteral.Items)
            {
                item.Accept(this);
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlBinaryScalarExpression cqlBinaryScalarExpression)
        {
            this.output.Append("Kind: BinaryOperator, ");
            this.output.Append($"OperatorKind: {cqlBinaryScalarExpression.OperatorKind.ToString()}, ");
            this.output.Append("LeftExpression: { ");
            cqlBinaryScalarExpression.LeftExpression.Accept(this);
            this.output.Append("} ");
            this.output.Append("RightExpression: { ");
            cqlBinaryScalarExpression.RightExpression.Accept(this);
            this.output.Append("} ");
        }

        void ICqlVisitor.Visit(CqlBinaryScalarOperatorKind cqlBinaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlBooleanLiteral cqlBooleanLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlBuiltinAggregate cqlBuiltinAggregate)
        {
            this.output.Append($"{{ Kind: {cqlBuiltinAggregate.Kind.ToString()}, ");
            this.output.Append($"OperatorKind: {cqlBuiltinAggregate.OperatorKind.ToString()}");
            this.output.Append("},");
        }

        void ICqlVisitor.Visit(CqlBuiltinScalarFunctionKind cqlBuiltinScalarFunctionKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlDistinctEnumerableExpression cqlDistinctEnumerableExpression)
        {
            this.output.Append("Kind: Distinct, ");
            string name = cqlDistinctEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlDistinctEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"DeclaredVariable: {{ Name: {name}, UniqueId: {uniqueId} }}, ");
            IReadOnlyList<CqlScalarExpression> scalarExpressions = cqlDistinctEnumerableExpression.Expression;
            this.output.Append("Expressions: [ { ");
            foreach (CqlScalarExpression scalarExpression in scalarExpressions)
            {
                scalarExpression.Accept(this);
            }
            this.output.Append("} ], ");
            this.output.Append("SourceExpression: { ");
            cqlDistinctEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }, ");
        }

        void ICqlVisitor.Visit(CqlEnumerableExpression cqlEnumerableExpression)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlEnumerableExpressionKind cqlEnumerableExpressionKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlEnumerationKind cqlEnumerationKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlFunctionIdentifier cqlFunctionIdentifier)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlGroupByEnumerableExpression cqlGroupByEnumerableExpression)
        {
            this.output.Append("{Kind: GroupBy, ");
            this.output.Append($"KeyCount: {cqlGroupByEnumerableExpression.KeyCount}, Aggregates: [ {{");
            foreach (CqlAggregate aggregate in cqlGroupByEnumerableExpression.Aggregates)
            {
                aggregate.Accept(this);
            }

            this.output.Append(" SourceExpression: { ");
            cqlGroupByEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }, ");
        }

        void ICqlVisitor.Visit(CqlInputEnumerableExpression cqlInputEnumerableExpression)
        {
            string name = cqlInputEnumerableExpression.Name;
            this.output.Append($"Kind: Input, Name: {name}");
        }

        void ICqlVisitor.Visit(CqlIsOperatorKind cqlIsOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlIsOperatorScalarExpression cqlIsOperatorScalarExpression)
        {
            this.output.Append("Kind: IsOperator, ");
            this.output.Append("Expression: { ");
            cqlIsOperatorScalarExpression.Expression.Accept(this);
            this.output.Append(" }, ");
        }

        void ICqlVisitor.Visit(CqlLetScalarExpression cqlLetScalarExpression)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLiteral cqlLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLiteralKind cqlLiteralKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLiteralScalarExpression cqlLiteralScalarExpression)
        {
            CqlLiteralKind literalKind = cqlLiteralScalarExpression.Literal.Kind;
            this.output.Append("Kind: Literal, ");
            this.output.Append("Literal: { ");
            this.output.Append($"Kind: {literalKind}, ");
            cqlLiteralScalarExpression.Literal.Accept(this);
        }

        void ICqlVisitor.Visit(CqlMuxScalarExpression cqlMuxScalarExpression)
        {
            this.output.Append("Kind: Mux, ");
            this.output.Append("ConditionExpression: { ");
            cqlMuxScalarExpression.ConditionExpression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("LeftExpression: ");
            cqlMuxScalarExpression.LeftExpression.Accept(this);
            this.output.Append("}, ");
            this.output.Append("RightExpression: ");
            cqlMuxScalarExpression.RightExpression.Accept(this);
            this.output.Append("}, ");
        }

        void ICqlVisitor.Visit(CqlNullLiteral cqlNullLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlNumberLiteral cqlNumberLiteral)
        {
            this.output.Append($"Value: {cqlNumberLiteral.Value}, ");
        }

        void ICqlVisitor.Visit(CqlObjectCreateScalarExpression cqlObjectCreateScalarExpression)
        {
            this.output.Append("Kind: ObjectCreate, ");
            string objectKind = cqlObjectCreateScalarExpression.ObjectKind;
            this.output.Append($"ObjectKind: {objectKind}, ");
            this.output.Append("Properties: [ ");
            foreach (CqlObjectProperty property in cqlObjectCreateScalarExpression.Properties)
            {
                string name = property.Name;
                this.output.Append($"{{ Name: {name}, ");
                this.output.Append("Expression: { ");
                property.Expression.Accept(this);
                this.output.Append("} } ");
            }

            this.output.Append("]");
        }

        void ICqlVisitor.Visit(CqlObjectLiteral cqlObjectLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlObjectLiteralProperty cqlObjectLiteralProperty)
        {
        }

        void ICqlVisitor.Visit(CqlObjectProperty cqlObjectProperty)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlOrderByEnumerableExpression cqlOrderByEnumerableExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlOrderByItem cqlOrderByItem)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlPropertyRefScalarExpression cqlPropertyRefScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlScalarAsEnumerableExpression cqlScalarAsEnumerableExpression)
        {
            this.output.Append("Kind: ScalarAsEnumerable, ");
            this.output.Append("Expression: { ");
            cqlScalarAsEnumerableExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append($"EnumerationKind: {cqlScalarAsEnumerableExpression.EnumerationKind}");
        }

        void ICqlVisitor.Visit(CqlScalarExpression cqlScalarExpression)
        {
            CqlScalarExpressionKind kind = cqlScalarExpression.Kind;
            string scalarExpressionKindString = kind switch
            {
                CqlScalarExpressionKind.ArrayCreate => "ArrayCreate",
                CqlScalarExpressionKind.ArrayIndexer => "ArrayIndexer",
                CqlScalarExpressionKind.BinaryOperator => "BinaryOperator",
                CqlScalarExpressionKind.IsOperator => "IsOperator",
                CqlScalarExpressionKind.Let => "Let",
                CqlScalarExpressionKind.Literal => "Literal",
                CqlScalarExpressionKind.Mux => "Mux",
                CqlScalarExpressionKind.ObjectCreate => "ObjectCreate",
                CqlScalarExpressionKind.PropertyRef => "PropertyRef",
                CqlScalarExpressionKind.SystemFunctionCall => "SystemFunctionCall",
                CqlScalarExpressionKind.TupleCreate => "TupleCreate",
                CqlScalarExpressionKind.TupleItemRef => "TupleItemRef",
                CqlScalarExpressionKind.UnaryOperator => "UnaryOperator",
                CqlScalarExpressionKind.UserDefinedFunctionCall => "UserDefinedFunctionCall",
                CqlScalarExpressionKind.VariableRef => "VariableRef",
                _ => throw new NotSupportedException($"Invalid CqlExpression kind: {kind}"),
            };

            this.output.Append($"{{Kind:{scalarExpressionKindString}, ");

        }

        void ICqlVisitor.Visit(CqlScalarExpressionKind cqlScalarExpressionKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlSelectEnumerableExpression cqlSelectEnumerableExpression)
        {
            this.output.Append("Kind: Select, ");
            string name = cqlSelectEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlSelectEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"DeclaredVariable: {{ Name: {name}, UniqueId: {uniqueId} }}, ");
            this.output.Append("Expression: {");
            cqlSelectEnumerableExpression.Expression.Accept(this);
            this.output.Append(" }, ");
            this.output.Append("SourceExpression: { ");
            cqlSelectEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }");
        }

        void ICqlVisitor.Visit(CqlSelectManyEnumerableExpression cqlSelectManyEnumerableExpression)
        {
            this.output.Append("Kind: SelectMany, ");
            string name = cqlSelectManyEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlSelectManyEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"DeclaredVariable: {{ Name: {name}, UniqueId: {uniqueId} }}, ");
            this.output.Append("SelectorExpression: { ");
            cqlSelectManyEnumerableExpression.SelectorExpression.Accept(this);
            this.output.Append("},");
            this.output.Append("SourceExpression: { ");
            cqlSelectManyEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append('}');
        }

        void ICqlVisitor.Visit(CqlSortOrder cqlSortOrder)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlStringLiteral cqlStringLiteral)
        {
            this.output.Append("Kind: StringLiteral, ");
            this.output.Append($"Value: {cqlStringLiteral.Value}");
        }

        void ICqlVisitor.Visit(CqlSystemFunctionCallScalarExpression cqlSystemFunctionCallScalarExpression)
        {
            this.output.Append("Kind: SystemFunctionCall, ");
            this.output.Append($"FunctionKind: {cqlSystemFunctionCallScalarExpression.FunctionKind.ToString()}, ");
            IReadOnlyList<CqlScalarExpression> scalarExpressions = cqlSystemFunctionCallScalarExpression.Arguments;
            this.output.Append("Arguments: [ ");
            foreach (CqlScalarExpression scalarExpression in scalarExpressions)
            {
                this.output.Append("{ ");
                scalarExpression.Accept(this);
                this.output.Append("},");
            }
            this.output.Append("],");
        }

        void ICqlVisitor.Visit(CqlTakeEnumerableExpression cqlTakeEnumerableExpression)
        {
            this.output.Append($"Kind: Take, SkipValue: {cqlTakeEnumerableExpression.SkipValue}, TakeValue: {cqlTakeEnumerableExpression.TakeValue}, ");
            this.output.Append("SourceExpression: { ");
            cqlTakeEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" } ");
        }

        void ICqlVisitor.Visit(CqlTupleAggregate cqlTupleAggregate)
        {
            this.output.Append("Kind: Tuple, ");
            this.output.Append("Items: [");
            foreach (CqlAggregate item in cqlTupleAggregate.Items)
            {
                item.Accept(this);
            }

            this.output.Append("],");
        }

        void ICqlVisitor.Visit(CqlTupleCreateScalarExpression cqlTupleCreateScalarExpression)
        {
            this.output.Append(" Kind: TupleCreate, ");
            this.output.Append("Items: [");
            foreach (CqlScalarExpression scalarExpression in cqlTupleCreateScalarExpression.Items)
            {
                this.output.Append("{ ");
                scalarExpression.Accept(this);
                this.output.Append("},");
            }

            this.output.Append("],");
        }

        void ICqlVisitor.Visit(CqlTupleItemRefScalarExpression cqlTupleItemRefScalarExpression)
        {
            this.output.Append("Kind: TupleItemRef, ");
            this.output.Append("Expression: { ");
            cqlTupleItemRefScalarExpression.Expression.Accept(this);
            this.output.Append("}, ");
            this.output.Append($"Index: {cqlTupleItemRefScalarExpression.Index}");
            this.output.Append("}, ");
        }

        void ICqlVisitor.Visit(CqlUnaryScalarExpression cqlUnaryScalarExpression)
        {
            this.output.Append("Kind: UnaryOperator, ");
            this.output.Append($"OperatorKind: {cqlUnaryScalarExpression.OperatorKind.ToString()}, ");
            this.output.Append("Expression: { ");
            cqlUnaryScalarExpression.Expression.Accept (this);
            this.output.Append("} ");
        }

        void ICqlVisitor.Visit(CqlUnaryScalarOperatorKind cqlUnaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlUndefinedLiteral cqlUndefinedLiteral)
        {
            this.output.Append("Kind: Undefined");
        }

        void ICqlVisitor.Visit(CqlUserDefinedFunctionCallScalarExpression cqlUserDefinedFunctionCallScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlVariable cqlVariable)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlVariableRefScalarExpression cqlVariableRefScalarExpression)
        {
            this.output.Append("Kind: VariableRef, ");
            this.output.Append($"Variable: {{ Name: {cqlVariableRefScalarExpression.Variable.Name}, UniqueId: {cqlVariableRefScalarExpression.Variable.UniqueId} }}");
        }

        void ICqlVisitor.Visit(CqlWhereEnumerableExpression cqlWhereEnumerableExpression)
        {
            this.output.Append("Kind: Where, ");
            this.output.Append("DeclaredVariable: {");
            this.output.Append($"Name: {cqlWhereEnumerableExpression.DeclaredVariable.Name}, ");
            this.output.Append($"UniqueId: {cqlWhereEnumerableExpression.DeclaredVariable.UniqueId}, ");
            this.output.Append("Expression: { ");
            cqlWhereEnumerableExpression.Expression.Accept(this);
            this.output.Append("} ");
            this.output.Append("SourceExpression: { ");
            cqlWhereEnumerableExpression.SourceExpression.Accept(this);
            this.output.Append(" }");
        }
    }
}
