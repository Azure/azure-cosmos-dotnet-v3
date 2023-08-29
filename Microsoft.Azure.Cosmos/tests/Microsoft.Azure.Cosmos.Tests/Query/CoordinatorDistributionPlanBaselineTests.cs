namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System.Collections.Generic;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan;
    using Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CoordinatorDistributionPlanBaselineTests : BaselineTests<CoordinatorDistributionPlanBaselineTests.CoordinatorDistributionPlanTestInput, CoordinatorDistributionPlanBaselineTests.CoordinatorDistributionPlanTestOutput>
    {
        [TestMethod]
        [Owner("akotalwar")]
        public void TestCoordinatorDistributionPlanDeserialization()
        {
            List<CoordinatorDistributionPlanTestInput> testVariations = new List<CoordinatorDistributionPlanTestInput>
            {
                CreateInput(
                    description: @"Input Expression",
                    coordinatorPlanJson: "{\"coordinatorDistributionPlan\": {\"clientQL\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}"),

                CreateInput(
                    description: @"Aggregate and ObjectCreate Expressions",
                    coordinatorPlanJson: "{\"coordinatorDistributionPlan\": {\"clientQL\": {\"Kind\": \"Select\",\"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 6},\"Expression\": {\"Kind\": \"ObjectCreate\",\"ObjectKind\": \"Object\",\"Properties\": [{\"Name\": \"count_a\",\"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": {\"Name\": \"v0\",\"UniqueId\": 6}}}]},\"SourceExpression\": {\"Kind\": \"Aggregate\",\"Aggregate\": {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\"},\"SourceExpression\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}}}"),

                CreateInput(
                    description: @"Select, Aggregate and BinaryOperator Expressions",
                    coordinatorPlanJson: "{\"coordinatorDistributionPlan\": {\"clientQL\": { \"Kind\": \"Select\", \"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 10 }, \"Expression\": {\"Kind\": \"ObjectCreate\",\"ObjectKind\": \"Object\",\"Properties\": [ {\"Name\": \"F1\",\"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [{ \"Name\": \"FieldA\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 0 }},{ \"Name\": \"FieldSum\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 1 }},{ \"Name\": \"FieldAvg\", \"Expression\": {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 10} }, \"Index\": 2},\"Index\": 1 }, \"RightExpression\": {\"Kind\": \"Literal\",\"Literal\": { \"Kind\": \"Number\", \"Value\": 0} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Undefined\" }},\"RightExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Divide\", \"LeftExpression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 10} }, \"Index\": 2},\"Index\": 0 }, \"RightExpression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 10} }, \"Index\": 2},\"Index\": 1 }}}}]}}, {\"Name\": \"F2\",\"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [{ \"Name\": \"OtherFieldA\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 0 }},{ \"Name\": \"OtherFieldMax\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 3 }} ]} }] }, \"SourceExpression\": {\"Kind\": \"GroupBy\",\"KeyCount\": 1,\"Aggregates\": [ {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\" }, {\"Kind\": \"Tuple\",\"Items\": [ {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\" }, {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\" }] }, {\"Kind\": \"Builtin\",\"OperatorKind\": \"Max\" }],\"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 16 }, \"Expression\": {\"Kind\": \"TupleCreate\",\"Items\": [ {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 0 }, {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"NotEqual\",\"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 1 }, \"Index\": 1},\"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Number\",\"Value\": 0 }} }, \"RightExpression\": {\"Kind\": \"UnaryOperator\",\"OperatorKind\": \"Not\",\"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [{ \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 1 }, \"Index\": 0} ]} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Array\",\"Items\": [] }},\"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 1 }, \"Index\": 0} }, {\"Kind\": \"TupleCreate\",\"Items\": [ {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"NotEqual\",\"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16} }, \"Index\": 2},\"Index\": 0 }, \"Index\": 1},\"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Number\",\"Value\": 0 }} }, \"RightExpression\": {\"Kind\": \"UnaryOperator\",\"OperatorKind\": \"Not\",\"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [{ \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16} }, \"Index\": 2},\"Index\": 0 }, \"Index\": 0} ]} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Array\",\"Items\": [] }},\"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 2 }, \"Index\": 0} }, {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16} }, \"Index\": 2},\"Index\": 1 }] }, {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"NotEqual\",\"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 3 }, \"Index\": 1},\"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Number\",\"Value\": 0 }} }, \"RightExpression\": {\"Kind\": \"UnaryOperator\",\"OperatorKind\": \"Not\",\"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [{ \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 3 }, \"Index\": 0} ]} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Array\",\"Items\": [] }},\"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16}},\"Index\": 3}, \"Index\": 0}}]}, \"SourceExpression\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}}}}"),

                CreateInput(
                    description: @"Select, Sum and VariableRef Expressions",
                    coordinatorPlanJson: "{\"coordinatorDistributionPlan\": {\"clientQL\": {\"Kind\": \"Select\",\"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 6},\"Expression\": {\"Kind\": \"ObjectCreate\",\"ObjectKind\": \"Object\",\"Properties\": [{\"Name\": \"count_a_plus_five\",\"Expression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"Add\",\"LeftExpression\": {\"Kind\": \"VariableRef\",\"Variable\": {\"Name\": \"v0\",\"UniqueId\": 6 }}, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 5 }}}}]}, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" }}}}}"),
            };

            this.ExecuteTestSuite(testVariations);
        }


        private static CoordinatorDistributionPlanTestInput CreateInput(
            string description,
            string coordinatorPlanJson)
        {
            return new CoordinatorDistributionPlanTestInput(description, coordinatorPlanJson);
        }

        public override CoordinatorDistributionPlanTestOutput ExecuteTest(CoordinatorDistributionPlanTestInput input)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented
            };
            if (!CoordinatorDistributionPlanDeserializer.TryDeserializeCoordinatorDistributionPlan(input.CoordinatorPlanJson, out CoordinatorDistributionPlan distributionPlan).IsSuccess)
            {
                CoordinatorDistributionPlanDeserializer.Result result = CoordinatorDistributionPlanDeserializer.TryDeserializeCoordinatorDistributionPlan(input.CoordinatorPlanJson, out _);
                throw new NotFoundException(result.ErrorMessage);
            }

            string serializedDistributionPlan = JsonConvert.SerializeObject(distributionPlan, settings);

            return new CoordinatorDistributionPlanTestOutput(serializedDistributionPlan);
        }

        public sealed class CoordinatorDistributionPlanTestOutput : BaselineTestOutput
        {
            public CoordinatorDistributionPlanTestOutput(string serializedCoordinatorPlanJson)
            {
                this.SerializedCoordinatorPlanJson = serializedCoordinatorPlanJson;
            }

            public string SerializedCoordinatorPlanJson { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                JObject jObject = JObject.Parse(this.SerializedCoordinatorPlanJson);
                string jsonString = jObject.ToString();
                xmlWriter.WriteStartElement("SerializedCoordinatorPlanJson");
                xmlWriter.WriteString(jsonString);
                xmlWriter.WriteEndElement();
            }
        }

        public sealed class CoordinatorDistributionPlanTestInput : BaselineTestInput
        {
            internal string CoordinatorPlanJson { get; set; }

            internal CoordinatorDistributionPlanTestInput(
                string description,
                string coordinatorPlanJson)
                : base(description)
            {
                this.CoordinatorPlanJson = coordinatorPlanJson;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString("Description", this.Description);
                xmlWriter.WriteElementString("CoordinatorDistributionPlanJson", this.CoordinatorPlanJson);
            }
        }
    }
}
