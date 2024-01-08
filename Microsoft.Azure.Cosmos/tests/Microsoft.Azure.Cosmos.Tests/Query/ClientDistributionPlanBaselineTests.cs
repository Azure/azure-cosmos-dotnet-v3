namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan;
    using Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

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
                JObject jsonObject = JObject.Parse(testResponse);

                string expectedDistributionPlan = jsonObject["_distributionPlan"].ToString();
                expectedDistributionPlan = RemoveWhitespace(expectedDistributionPlan);

                string fileName = Path.GetFileName(filePath);

                testVariations.Add(
                    CreateInput(
                        description: fileName,
                        distributionPlanJson: expectedDistributionPlan));
            }

            this.ExecuteTestSuite(testVariations);
        }

        private static string RemoveWhitespace(string jsonString)
        {
            return jsonString.Replace(" ", string.Empty).Replace("\t", string.Empty).Replace("\r", string.Empty);
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
                JObject jObject = JObject.Parse(this.SerializedClientPlanJson);
                string jsonString = jObject.ToString();
                xmlWriter.WriteStartElement("SerializedClientPlanJson");
                xmlWriter.WriteString(jsonString);
                xmlWriter.WriteEndElement();
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
                JObject distributionPlan = JObject.Parse(this.DistributionPlanJson);
                xmlWriter.WriteElementString("Description", this.Description);
                xmlWriter.WriteElementString("ClientDistributionPlanJson", distributionPlan["clientDistributionPlan"].ToString());
            }
        }
    }
}
