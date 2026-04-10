namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan;
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ClientDistributionPlanBaselineTests : BaselineTests<ClientDistributionPlanBaselineTests.ClientDistributionPlanTestInput, ClientDistributionPlanBaselineTests.ClientDistributionPlanTestOutput>
    {
        [TestMethod]
        [Owner("akotalwar")]
        public void TestClientDistributionPlanDeserialization()
        {
            List<ClientDistributionPlanTestInput> testVariations = new List<ClientDistributionPlanTestInput>();
            string textPath = "../../../Query/DistributionPlans/Text";
            string[] filePaths = Directory.GetFiles(textPath);

            foreach (string filePath in filePaths)
            {
                string testResponse = File.ReadAllText(filePath);
                JsonDocument jsonDocument = JsonDocument.Parse(testResponse);

                JsonElement root = jsonDocument.RootElement;
                JsonElement distributionPlanElement = root.GetProperty("_distributionPlan");
                string expectedDistributionPlan = distributionPlanElement.ToString();

                string fileName = Path.GetFileName(filePath);

                testVariations.Add(
                    CreateInput(
                        description: fileName,
                        distributionPlanJson: expectedDistributionPlan));
            }

            this.ExecuteTestSuite(testVariations);
        }

        private static ClientDistributionPlanTestInput CreateInput(
            string description,
            string distributionPlanJson)
        {
            return new ClientDistributionPlanTestInput(description, distributionPlanJson);
        }

        public override ClientDistributionPlanTestOutput ExecuteTest(ClientDistributionPlanTestInput input)
        {
            ClientDistributionPlan clientDistributionPlan = ClientDistributionPlanDeserializer.DeserializeClientDistributionPlan(input.DistributionPlanJson);
            DistributionPlanWriter visitor = new DistributionPlanWriter();
            clientDistributionPlan.Cql.Accept(visitor);
            return new ClientDistributionPlanTestOutput(visitor.SerializedOutput);
        }

        public sealed class ClientDistributionPlanTestOutput : BaselineTestOutput
        {
            public ClientDistributionPlanTestOutput(string serializedclientPlanJson)
            {
                this.SerializedClientPlanJson = serializedclientPlanJson;
            }

            public string SerializedClientPlanJson { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                JsonDocument clientPlan = JsonDocument.Parse(this.SerializedClientPlanJson);
                JsonElement clientPlanElement = clientPlan.RootElement;
                using (StringWriter stringWriter = new StringWriter())
                {
                    JsonSerializerOptions jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    string formattedJson = JsonSerializer.Serialize(clientPlanElement.GetProperty("clientDistributionPlan"), jsonOptions);

                    xmlWriter.WriteStartElement("SerializedClientDistributionPlan");
                    xmlWriter.WriteString(formattedJson);
                    xmlWriter.WriteEndElement();
                }
            }
        }

        public sealed class ClientDistributionPlanTestInput : BaselineTestInput
        {
            internal string DistributionPlanJson { get; set; }

            internal ClientDistributionPlanTestInput(
                string description,
                string distributionPlanJson)
                : base(description)
            {
                this.DistributionPlanJson = distributionPlanJson;
            }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                JsonDocument distributionPlan = JsonDocument.Parse(this.DistributionPlanJson);
                JsonElement distributionPlanElement = distributionPlan.RootElement;

                xmlWriter.WriteElementString("Description", this.Description);
                xmlWriter.WriteElementString("ClientDistributionPlanJson", distributionPlanElement.GetProperty("clientDistributionPlan").ToString());
            }
        }
    }
}