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
                CreateInput(
                    description: @"Input Expression",
                    clientPlanJson: "{\"clientDistributionPlan\": {\"Cql\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}"),

                CreateInput(
                    description: @"Aggregate and ObjectCreate Expressions",
                    clientPlanJson: "{\"clientDistributionPlan\": {\"Cql\": {\"Kind\": \"Select\",\"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 6},\"Expression\": {\"Kind\": \"ObjectCreate\",\"ObjectKind\": \"Object\",\"Properties\": [{\"Name\": \"count_a\",\"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": {\"Name\": \"v0\",\"UniqueId\": 6}}}]},\"SourceExpression\": {\"Kind\": \"Aggregate\",\"Aggregate\": {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\"},\"SourceExpression\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}}}"),

                CreateInput(
                    description: @"Select, Aggregate and BinaryOperator Expressions",
                    clientPlanJson: "{\"clientDistributionPlan\": {\"Cql\": { \"Kind\": \"Select\", \"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 10 }, \"Expression\": {\"Kind\": \"ObjectCreate\",\"ObjectKind\": \"Object\",\"Properties\": [ {\"Name\": \"F1\",\"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [{ \"Name\": \"FieldA\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 0 }},{ \"Name\": \"FieldSum\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 1 }},{ \"Name\": \"FieldAvg\", \"Expression\": {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Equal\", \"LeftExpression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 10} }, \"Index\": 2},\"Index\": 1 }, \"RightExpression\": {\"Kind\": \"Literal\",\"Literal\": { \"Kind\": \"Number\", \"Value\": 0} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Undefined\" }},\"RightExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"Divide\", \"LeftExpression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 10} }, \"Index\": 2},\"Index\": 0 }, \"RightExpression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 10} }, \"Index\": 2},\"Index\": 1 }}}}]}}, {\"Name\": \"F2\",\"Expression\": { \"Kind\": \"ObjectCreate\", \"ObjectKind\": \"Object\", \"Properties\": [{ \"Name\": \"OtherFieldA\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 0 }},{ \"Name\": \"OtherFieldMax\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 10 }},\"Index\": 3 }} ]} }] }, \"SourceExpression\": {\"Kind\": \"GroupBy\",\"KeyCount\": 1,\"Aggregates\": [ {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\" }, {\"Kind\": \"Tuple\",\"Items\": [ {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\" }, {\"Kind\": \"Builtin\",\"OperatorKind\": \"Sum\" }] }, {\"Kind\": \"Builtin\",\"OperatorKind\": \"Max\" }],\"SourceExpression\": { \"Kind\": \"Select\", \"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 16 }, \"Expression\": {\"Kind\": \"TupleCreate\",\"Items\": [ {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 0 }, {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"NotEqual\",\"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 1 }, \"Index\": 1},\"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Number\",\"Value\": 0 }} }, \"RightExpression\": {\"Kind\": \"UnaryOperator\",\"OperatorKind\": \"Not\",\"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [{ \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 1 }, \"Index\": 0} ]} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Array\",\"Items\": [] }},\"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 1 }, \"Index\": 0} }, {\"Kind\": \"TupleCreate\",\"Items\": [ {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"NotEqual\",\"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16} }, \"Index\": 2},\"Index\": 0 }, \"Index\": 1},\"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Number\",\"Value\": 0 }} }, \"RightExpression\": {\"Kind\": \"UnaryOperator\",\"OperatorKind\": \"Not\",\"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [{ \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16} }, \"Index\": 2},\"Index\": 0 }, \"Index\": 0} ]} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Array\",\"Items\": [] }},\"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 2 }, \"Index\": 0} }, {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"VariableRef\",\"Variable\": { \"Name\": \"v0\", \"UniqueId\": 16} }, \"Index\": 2},\"Index\": 1 }] }, {\"Kind\": \"Mux\",\"ConditionExpression\": { \"Kind\": \"BinaryOperator\", \"OperatorKind\": \"And\", \"LeftExpression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"NotEqual\",\"LeftExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 3 }, \"Index\": 1},\"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Number\",\"Value\": 0 }} }, \"RightExpression\": {\"Kind\": \"UnaryOperator\",\"OperatorKind\": \"Not\",\"Expression\": { \"Kind\": \"SystemFunctionCall\", \"FunctionKind\": \"Is_Defined\", \"Arguments\": [{ \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16 }},\"Index\": 3 }, \"Index\": 0} ]} }},\"LeftExpression\": { \"Kind\": \"Literal\", \"Literal\": {\"Kind\": \"Array\",\"Items\": [] }},\"RightExpression\": { \"Kind\": \"TupleItemRef\", \"Expression\": {\"Kind\": \"TupleItemRef\",\"Expression\": { \"Kind\": \"VariableRef\", \"Variable\": {\"Name\": \"v0\",\"UniqueId\": 16}},\"Index\": 3}, \"Index\": 0}}]}, \"SourceExpression\": {\"Kind\": \"Input\",\"Name\": \"root\"}}}}}}"),

                CreateInput(
                    description: @"Select, Sum and VariableRef Expressions",
                    clientPlanJson: "{\"clientDistributionPlan\": {\"Cql\": {\"Kind\": \"Select\",\"DeclaredVariable\": {\"Name\": \"v0\",\"UniqueId\": 6},\"Expression\": {\"Kind\": \"ObjectCreate\",\"ObjectKind\": \"Object\",\"Properties\": [{\"Name\": \"count_a_plus_five\",\"Expression\": {\"Kind\": \"BinaryOperator\",\"OperatorKind\": \"Add\",\"LeftExpression\": {\"Kind\": \"VariableRef\",\"Variable\": {\"Name\": \"v0\",\"UniqueId\": 6 }}, \"RightExpression\": { \"Kind\": \"Literal\", \"Literal\": { \"Kind\": \"Number\", \"Value\": 5 }}}}]}, \"SourceExpression\": { \"Kind\": \"Aggregate\", \"Aggregate\": { \"Kind\": \"Builtin\", \"OperatorKind\": \"Sum\" }, \"SourceExpression\": { \"Kind\": \"Input\", \"Name\": \"root\" }}}}}"),
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

        public string SerializedOutput => this.output.ToString();

        void ICqlVisitor.Visit(CqlAggregate cqlAggregate)
        {
            CqlAggregateKind aggregateKind = cqlAggregate.Kind;
            string aggregate = "";

            switch (aggregateKind)
            {
                case CqlAggregateKind.Builtin:
                    aggregate = "Builtin";
                    break;
                case CqlAggregateKind.Tuple:
                    aggregate = "Tuple";
                    break;
            }

            this.output.Append($"Kind: {aggregate},");
        }

        void ICqlVisitor.Visit(CqlAggregateEnumerableExpression cqlAggregateEnumerableExpression)
        {
            this.output.Append("{Kind: Aggregate, ");
            this.output.Append("Aggregate: [{");
            cqlAggregateEnumerableExpression.SourceExpression.Accept(this);
            cqlAggregateEnumerableExpression.Aggregate.Accept(this);
        }

        void ICqlVisitor.Visit(CqlAggregateKind cqlAggregateKind)
        {
            throw new NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlAggregateOperatorKind cqlAggregateOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlArrayCreateScalarExpression cqlArrayCreateScalarExpression)
        {
            this.output.Append("{Kind: ArrayCreate, ");
            string arrayKind = cqlArrayCreateScalarExpression.ArrayKind;
            IReadOnlyList<CqlScalarExpression> items = cqlArrayCreateScalarExpression.Items;

            foreach (CqlScalarExpression item in items)
            { 
                item.Accept(this);
            }
        }

        void ICqlVisitor.Visit(CqlArrayIndexerScalarExpression cqlArrayIndexerScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlArrayLiteral cqlArrayLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlBinaryScalarExpression cqlBinaryScalarExpression)
        {
            throw new System.NotImplementedException();
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
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlBuiltinScalarFunctionKind cqlBuiltinScalarFunctionKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlDistinctEnumerableExpression cqlDistinctEnumerableExpression)
        {
            this.output.Append("Kind: Distinct, ");
            cqlDistinctEnumerableExpression.SourceExpression.Accept(this);
            string name = cqlDistinctEnumerableExpression.DeclaredVariable.Name;
            long uniqueId = cqlDistinctEnumerableExpression.DeclaredVariable.UniqueId;
            this.output.Append($"DeclaredVariable: {{ Name: {name}, UniqueId: {uniqueId} }}, ");
            IReadOnlyList<CqlScalarExpression> scalarExpressions = cqlDistinctEnumerableExpression.Expression;

            foreach (CqlScalarExpression scalarExpression in scalarExpressions)
            {
                CqlScalarExpressionKind scalarExpressionKind = scalarExpression.Kind;
                string scalarExpressionKindString = scalarExpressionKind switch
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
                    _ => throw new NotSupportedException($"Invalid CqlExpression kind: {scalarExpressionKind}"),
                };

                this.output.Append($"{{Kind:{scalarExpressionKindString}, ");
            }
        }

        void ICqlVisitor.Visit(CqlEnumerableExpression cqlEnumerableExpression)
        {
            CqlEnumerableExpressionKind kind = cqlEnumerableExpression.Kind;
            string kindString = kind switch
            {
                CqlEnumerableExpressionKind.Aggregate => "Aggregate",
                CqlEnumerableExpressionKind.Distinct => "Distinct",
                CqlEnumerableExpressionKind.GroupBy => "GroupBy",
                CqlEnumerableExpressionKind.Input => "Input",
                CqlEnumerableExpressionKind.OrderBy => "OrderBy",
                CqlEnumerableExpressionKind.ScalarAsEnumerable => "ScalarAsEnumerable",
                CqlEnumerableExpressionKind.Select => "Select",
                CqlEnumerableExpressionKind.SelectMany => "SelectMany",
                CqlEnumerableExpressionKind.Take => "Take",
                CqlEnumerableExpressionKind.Where => "Where",
                _ => throw new NotSupportedException($"Invalid CqlExpression kind: {kind}"),
            };

            this.output.Append($"Kind: {kindString}, ");
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
            cqlGroupByEnumerableExpression.SourceExpression.Accept(this);
            ulong keyCount = cqlGroupByEnumerableExpression.KeyCount;
            this.output.Append($"KeyCount: {keyCount},Aggregates: [ {{");

            IReadOnlyList<CqlAggregate> aggregates = cqlGroupByEnumerableExpression.Aggregates;

            foreach (CqlAggregate aggregate in aggregates)
            { 
                aggregate.Accept(this);
            }
        }

        void ICqlVisitor.Visit(CqlInputEnumerableExpression cqlInputEnumerableExpression)
        {
            string name = cqlInputEnumerableExpression.Name;
            this.output.Append($"SourceExpressions: {{Kind: Input, Name: {name}}}");
        }

        void ICqlVisitor.Visit(CqlIsOperatorKind cqlIsOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlIsOperatorScalarExpression cqlIsOperatorScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlLetScalarExpression cqlLetScalarExpression)
        {
            throw new System.NotImplementedException();
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
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlMuxScalarExpression cqlMuxScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlNullLiteral cqlNullLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlNumberLiteral cqlNumberLiteral)
        {
            //stringBuilfer.Append(this.value.ToString());
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlObjectCreateScalarExpression cqlObjectCreateScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlObjectLiteral cqlObjectLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlObjectLiteralProperty cqlObjectLiteralProperty)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlObjectProperty cqlObjectProperty)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlOrderByEnumerableExpression cqlOrderByEnumerableExpression)
        {
            //throw new System.NotImplementedException();
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
            //throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlScalarExpression cqlScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlScalarExpressionKind cqlScalarExpressionKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlSelectEnumerableExpression cqlSelectEnumerableExpression)
        {
            //throw new System.NotImplementedException();
            string start = "{Select: "; 
            cqlSelectEnumerableExpression.SourceExpression.Accept(this);
            Console.WriteLine("Select");
        }

        void ICqlVisitor.Visit(CqlSelectManyEnumerableExpression cqlSelectManyEnumerableExpression)
        {
            //throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlSortOrder cqlSortOrder)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlStringLiteral cqlStringLiteral)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlSystemFunctionCallScalarExpression cqlSystemFunctionCallScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlTakeEnumerableExpression cqlTakeEnumerableExpression)
        {
            //throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlTupleAggregate cqlTupleAggregate)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlTupleCreateScalarExpression cqlTupleCreateScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlTupleItemRefScalarExpression cqlTupleItemRefScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlUnaryScalarExpression cqlUnaryScalarExpression)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlUnaryScalarOperatorKind cqlUnaryScalarOperatorKind)
        {
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlUndefinedLiteral cqlUndefinedLiteral)
        {
            throw new System.NotImplementedException();
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
            throw new System.NotImplementedException();
        }

        void ICqlVisitor.Visit(CqlWhereEnumerableExpression cqlWhereEnumerableExpression)
        {
            throw new System.NotImplementedException();
        }
    }
}
