namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class AggregateQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestQueryCrossPartitionAggregateFunctionsAsync()
        {
            AggregateTestArgs args = new AggregateTestArgs()
            {
                NumberOfDocsWithSamePartitionKey = 37,
                NumberOfDocumentsDifferentPartitionKey = 43,
                PartitionKey = "key",
                UniquePartitionKey = "uniquePartitionKey",
                Field = "field",
                Values = new object[] { false, true, "abc", "cdfg", "opqrs", "ttttttt", "xyz" },
            };

            List<string> documents = new List<string>(args.NumberOfDocumentsDifferentPartitionKey + args.NumberOfDocsWithSamePartitionKey);
            foreach (object val in args.Values)
            {
                Document doc;
                doc = new Document();
                doc.SetPropertyValue(args.PartitionKey, val);
                doc.SetPropertyValue("id", Guid.NewGuid().ToString());

                documents.Add(doc.ToString());
            }

            for (int i = 0; i < args.NumberOfDocsWithSamePartitionKey; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(args.PartitionKey, args.UniquePartitionKey);
                documents.Add(doc.ToString());
            }

            Random random = new Random();
            for (int i = 0; i < args.NumberOfDocumentsDifferentPartitionKey; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(args.PartitionKey, random.NextDouble());
                documents.Add(doc.ToString());
            }

            await this.CreateIngestQueryDelete<AggregateTestArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                ImplementationAsync,
                args,
                "/" + args.PartitionKey);

            async Task ImplementationAsync(
                Container container,
                IEnumerable<Document> inputDocuments,
                AggregateTestArgs aggregateTestArgs)
            {
                IEnumerable<Document> documentsWherePkIsANumber = inputDocuments
                    .Where(doc =>
                    {
                        return double.TryParse(
                            JObject.Parse(doc.ToString())[aggregateTestArgs.PartitionKey].ToString(),
                            out double result);
                    });
                double numberSum = documentsWherePkIsANumber
                    .Sum(doc =>
                    {
                        return double.Parse(JObject.Parse(doc.ToString())[aggregateTestArgs.PartitionKey].ToString());
                    });
                double count = documentsWherePkIsANumber.Count();
                AggregateQueryArguments[] aggregateQueryArgumentsList = new AggregateQueryArguments[]
                {
                new AggregateQueryArguments()
                {
                    AggregateOperator = "AVG",
                    ExpectedValue = CosmosNumber64.Create(numberSum / count),
                    Predicate = $"IS_NUMBER(r.{aggregateTestArgs.PartitionKey})",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "AVG",
                    ExpectedValue = null,
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "COUNT",
                    ExpectedValue = CosmosNumber64.Create(documents.Count()),
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "MAX",
                    ExpectedValue = CosmosString.Create("xyz"),
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "MIN",
                    ExpectedValue = CosmosBoolean.Create(false),
                    Predicate = "true",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "SUM",
                    ExpectedValue = CosmosNumber64.Create(numberSum),
                    Predicate = $"IS_NUMBER(r.{aggregateTestArgs.PartitionKey})",
                },
                new AggregateQueryArguments()
                {
                    AggregateOperator = "SUM",
                    ExpectedValue = null,
                    Predicate = $"true",
                },
                };

                foreach (int maxDoP in new[] { 0, 10 })
                {
                    foreach (AggregateQueryArguments argument in aggregateQueryArgumentsList)
                    {
                        string[] queryFormats = new[]
                        {
                        "SELECT VALUE {0}(r.{1}) FROM r WHERE {2}",
                        "SELECT VALUE {0}(r.{1}) FROM r WHERE {2} ORDER BY r.{1}"
                    };

                        foreach (string queryFormat in queryFormats)
                        {
                            string query = string.Format(
                                CultureInfo.InvariantCulture,
                                queryFormat,
                                argument.AggregateOperator,
                                aggregateTestArgs.PartitionKey,
                                argument.Predicate);
                            string message = string.Format(
                                CultureInfo.InvariantCulture,
                                "query: {0}, data: {1}",
                                query,
                                JsonConvert.SerializeObject(argument));

                            List<CosmosElement> items = await QueryTestsBase.RunQueryAsync<CosmosElement>(
                                container,
                                query,
                                new QueryRequestOptions()
                                {
                                    MaxConcurrency = maxDoP,
                                });

                            if (argument.ExpectedValue == null)
                            {
                                Assert.AreEqual(0, items.Count, message);
                            }
                            else
                            {
                                Assert.AreEqual(1, items.Count, message);
                                CosmosElement expected = argument.ExpectedValue;
                                CosmosElement actual = items.Single();

                                if ((expected is CosmosNumber expectedNumber) && (actual is CosmosNumber actualNumber))
                                {
                                    Assert.AreEqual(Number64.ToDouble(expectedNumber.Value), Number64.ToDouble(actualNumber.Value), .01);
                                }
                                else
                                {
                                    Assert.AreEqual(expected, actual, message);
                                }
                            }
                        }
                    }
                }
            }
        }

        private struct AggregateTestArgs
        {
            public int NumberOfDocumentsDifferentPartitionKey;
            public int NumberOfDocsWithSamePartitionKey;
            public string PartitionKey;
            public string UniquePartitionKey;
            public string Field;
            public object[] Values;
        }

        private struct AggregateQueryArguments
        {
            public string AggregateOperator;
            public CosmosElement ExpectedValue;
            public string Predicate;
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionAggregateFunctionsEmptyPartitionsAsync()
        {
            AggregateQueryEmptyPartitionsArgs testArgs = new AggregateQueryEmptyPartitionsArgs()
            {
                NumDocuments = 100,
                PartitionKey = "key",
                UniqueField = "UniqueField",
            };

            List<string> inputDocuments = new List<string>(testArgs.NumDocuments);
            for (int i = 0; i < testArgs.NumDocuments; ++i)
            {
                Document doc = new Document();
                doc.SetPropertyValue(testArgs.PartitionKey, Guid.NewGuid());
                doc.SetPropertyValue(testArgs.UniqueField, i);
                inputDocuments.Add(doc.ToString());
            }

            await this.CreateIngestQueryDelete<AggregateQueryEmptyPartitionsArgs>(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync,
                testArgs,
                "/" + testArgs.PartitionKey);

            async Task ImplementationAsync(
                Container container,
                IEnumerable<Document> documents,
                AggregateQueryEmptyPartitionsArgs args)
            {
                int numDocuments = args.NumDocuments;
                string partitionKey = args.PartitionKey;
                string uniqueField = args.UniqueField;

                // Perform full fanouts but only match a single value that isn't the partition key.
                // This leads to all other partitions returning { "<aggregate>" = UNDEFINDED, "count" = 0 }
                // which should be ignored from the aggregation.
                int valueOfInterest = args.NumDocuments / 2;
                string[] queries = new string[]
                {
                    $"SELECT VALUE AVG(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                    $"SELECT VALUE MIN(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                    $"SELECT VALUE MAX(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                    $"SELECT VALUE SUM(c.{uniqueField}) FROM c WHERE c.{uniqueField} = {valueOfInterest}",
                };

                foreach (string query in queries)
                {
                    try
                    {
                        List<dynamic> items = await QueryTestsBase.RunQueryAsync<dynamic>(
                            container,
                            query,
                            new QueryRequestOptions()
                            {
                                MaxConcurrency = 10,
                            });

                        Assert.AreEqual(valueOfInterest, items.Single());
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Something went wrong with query: {query}, ex: {ex}");
                    }
                }
            }
        }

        private struct AggregateQueryEmptyPartitionsArgs
        {
            public int NumDocuments;
            public string PartitionKey;
            public string UniqueField;
        }

        [TestMethod]
        public async Task TestQueryCrossPartitionAggregateFunctionsWithMixedTypes()
        {
            AggregateQueryMixedTypes args = new AggregateQueryMixedTypes()
            {
                PartitionKey = "key",
                Field = "field",
                DoubleOnlyKey = "doubleOnly",
                StringOnlyKey = "stringOnly",
                BoolOnlyKey = "boolOnly",
                NullOnlyKey = "nullOnly",
                ObjectOnlyKey = "objectOnlyKey",
                ArrayOnlyKey = "arrayOnlyKey",
                OneObjectKey = "oneObjectKey",
                OneArrayKey = "oneArrayKey",
                UndefinedKey = "undefinedKey",
            };

            List<string> documents = new List<string>();
            Random random = new Random(1234);
            for (int i = 0; i < 20; ++i)
            {
                Document doubleDoc = new Document();
                doubleDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                doubleDoc.SetPropertyValue(args.Field, random.Next(1, 100000));
                documents.Add(doubleDoc.ToString());
                doubleDoc.SetPropertyValue(args.PartitionKey, args.DoubleOnlyKey);
                documents.Add(doubleDoc.ToString());

                Document stringDoc = new Document();
                stringDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                stringDoc.SetPropertyValue(args.Field, random.NextDouble().ToString());
                documents.Add(stringDoc.ToString());
                stringDoc.SetPropertyValue(args.PartitionKey, args.StringOnlyKey);
                documents.Add(stringDoc.ToString());

                Document boolDoc = new Document();
                boolDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                boolDoc.SetPropertyValue(args.Field, random.Next() % 2 == 0);
                documents.Add(boolDoc.ToString());
                boolDoc.SetPropertyValue(args.PartitionKey, args.BoolOnlyKey);
                documents.Add(boolDoc.ToString());

                Document nullDoc = new Document();
                nullDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                nullDoc.propertyBag.Add(args.Field, null);
                documents.Add(nullDoc.ToString());
                nullDoc.SetPropertyValue(args.PartitionKey, args.NullOnlyKey);
                documents.Add(nullDoc.ToString());

                Document objectDoc = new Document();
                objectDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                objectDoc.SetPropertyValue(args.Field, new object { });
                documents.Add(objectDoc.ToString());
                objectDoc.SetPropertyValue(args.PartitionKey, args.ObjectOnlyKey);
                documents.Add(objectDoc.ToString());

                Document arrayDoc = new Document();
                arrayDoc.SetPropertyValue(args.PartitionKey, Guid.NewGuid());
                arrayDoc.SetPropertyValue(args.Field, new object[] { });
                documents.Add(arrayDoc.ToString());
                arrayDoc.SetPropertyValue(args.PartitionKey, args.ArrayOnlyKey);
                documents.Add(arrayDoc.ToString());
            }

            Document oneObjectDoc = new Document();
            oneObjectDoc.SetPropertyValue(args.PartitionKey, args.OneObjectKey);
            oneObjectDoc.SetPropertyValue(args.Field, new object { });
            documents.Add(oneObjectDoc.ToString());

            Document oneArrayDoc = new Document();
            oneArrayDoc.SetPropertyValue(args.PartitionKey, args.OneArrayKey);
            oneArrayDoc.SetPropertyValue(args.Field, new object[] { });
            documents.Add(oneArrayDoc.ToString());

            Document undefinedDoc = new Document();
            undefinedDoc.SetPropertyValue(args.PartitionKey, args.UndefinedKey);
            // This doc does not have the field key set
            documents.Add(undefinedDoc.ToString());

            await this.CreateIngestQueryDelete<AggregateQueryMixedTypes>(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                documents,
                this.TestQueryCrossPartitionAggregateFunctionsWithMixedTypesHelper,
                args,
                "/" + args.PartitionKey);
        }

        private struct AggregateQueryMixedTypes
        {
            public string PartitionKey;
            public string Field;
            public string DoubleOnlyKey;
            public string StringOnlyKey;
            public string BoolOnlyKey;
            public string NullOnlyKey;
            public string ObjectOnlyKey;
            public string ArrayOnlyKey;
            public string OneObjectKey;
            public string OneArrayKey;
            public string UndefinedKey;
        }

        private async Task TestQueryCrossPartitionAggregateFunctionsWithMixedTypesHelper(
            Container container,
            IEnumerable<Document> documents,
            AggregateQueryMixedTypes args)
        {
            await QueryTestsBase.NoOp();
            string partitionKey = args.PartitionKey;
            string field = args.Field;
            string[] typeOnlyPartitionKeys = new string[]
            {
                args.DoubleOnlyKey,
                args.StringOnlyKey,
                args.BoolOnlyKey,
                args.NullOnlyKey,
                args.ObjectOnlyKey,
                args.ArrayOnlyKey,
                args.OneArrayKey,
                args.OneObjectKey,
                args.UndefinedKey
            };

            string[] aggregateOperators = new string[] { "AVG", "MIN", "MAX", "SUM", "COUNT" };
            string[] typeCheckFunctions = new string[] { "IS_ARRAY", "IS_BOOL", "IS_NULL", "IS_NUMBER", "IS_OBJECT", "IS_STRING", "IS_DEFINED", "IS_PRIMITIVE" };
            List<string> queries = new List<string>();
            foreach (string aggregateOperator in aggregateOperators)
            {
                foreach (string typeCheckFunction in typeCheckFunctions)
                {
                    queries.Add(
                    $@"
                        SELECT VALUE {aggregateOperator} (c.{field}) 
                        FROM c 
                        WHERE {typeCheckFunction}(c.{field})
                    ");
                }

                foreach (string typeOnlyPartitionKey in typeOnlyPartitionKeys)
                {
                    queries.Add(
                    $@"
                        SELECT VALUE {aggregateOperator} (c.{field}) 
                        FROM c 
                        WHERE c.{partitionKey} = ""{typeOnlyPartitionKey}""
                    ");
                }
            };

            // mixing primitive and non primitives
            foreach (string minmaxop in new string[] { "MIN", "MAX" })
            {
                foreach (string key in new string[] { args.OneObjectKey, args.OneArrayKey })
                {
                    queries.Add(
                    $@"
                        SELECT VALUE {minmaxop} (c.{field}) 
                        FROM c 
                        WHERE c.{partitionKey} IN (""{key}"", ""{args.DoubleOnlyKey}"")
                    ");
                }
            }

            string filename = $"CrossPartitionQueryTests.AggregateMixedTypes";
            string baselinePath = $"{filename}_baseline.xml";

            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Indent = true,
                NewLineOnAttributes = true,
            };

            StringBuilder builder = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(builder, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Results");
                foreach (string query in queries)
                {
                    string formattedQuery = string.Join(
                        Environment.NewLine,
                        query.Trim().Split(
                            new[] { Environment.NewLine },
                            StringSplitOptions.None)
                            .Select(x => x.Trim()));

                    List<dynamic> items = await QueryTestsBase.RunQueryAsync<dynamic>(
                        container,
                        query,
                        new QueryRequestOptions()
                        {
                            MaxItemCount = 10,
                        });

                    writer.WriteStartElement("Result");
                    writer.WriteStartElement("Query");
                    writer.WriteCData(formattedQuery);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Aggregation");
                    if (items.Count > 0)
                    {
                        writer.WriteCData(JsonConvert.SerializeObject(items.Single()));
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            Regex r = new Regex(">\\s+");
            string normalizedBaseline = r.Replace(File.ReadAllText(baselinePath), ">");
            string normalizedOutput = r.Replace(builder.ToString(), ">");

            Assert.AreEqual(normalizedBaseline, normalizedOutput);
        }

        [TestMethod]
        [Owner("brchon")]
        public async Task TestNonValueAggregates()
        {
            string[] documents = new string[]
            {
                @"{""first"":""Good"",""last"":""Trevino"",""age"":23,""height"":61,""income"":59848}",
                @"{""first"":""Charles"",""last"":""Decker"",""age"":31,""height"":64,""income"":55970}",
                @"{""first"":""Holden"",""last"":""Cotton"",""age"":30,""height"":66,""income"":57075}",
                @"{""first"":""Carlene"",""last"":""Cabrera"",""age"":26,""height"":72,""income"":98018}",
                @"{""first"":""Gates"",""last"":""Spence"",""age"":38,""height"":53,""income"":12338}",
                @"{""first"":""Camacho"",""last"":""Singleton"",""age"":40,""height"":52,""income"":76973}",
                @"{""first"":""Rachel"",""last"":""Tucker"",""age"":27,""height"":68,""income"":28116}",
                @"{""first"":""Kristi"",""last"":""Robertson"",""age"":32,""height"":53,""income"":61687}",
                @"{""first"":""Poole"",""last"":""Petty"",""age"":22,""height"":75,""income"":53381}",
                @"{""first"":""Lacey"",""last"":""Carlson"",""age"":38,""height"":78,""income"":63989}",
                @"{""first"":""Rosario"",""last"":""Mendez"",""age"":21,""height"":64,""income"":20300}",
                @"{""first"":""Estrada"",""last"":""Collins"",""age"":28,""height"":74,""income"":6926}",
                @"{""first"":""Ursula"",""last"":""Burton"",""age"":26,""height"":66,""income"":32870}",
                @"{""first"":""Rochelle"",""last"":""Sanders"",""age"":24,""height"":56,""income"":47564}",
                @"{""first"":""Darcy"",""last"":""Herring"",""age"":27,""height"":52,""income"":67436}",
                @"{""first"":""Carole"",""last"":""Booth"",""age"":34,""height"":60,""income"":50177}",
                @"{""first"":""Cruz"",""last"":""Russell"",""age"":25,""height"":52,""income"":95072}",
                @"{""first"":""Wilma"",""last"":""Robbins"",""age"":36,""height"":50,""income"":53008}",
                @"{""first"":""Mcdaniel"",""last"":""Barlow"",""age"":21,""height"":78,""income"":85441}",
                @"{""first"":""Leann"",""last"":""Blackwell"",""age"":40,""height"":79,""income"":900}",
                @"{""first"":""Hoffman"",""last"":""Hoffman"",""age"":31,""height"":76,""income"":1208}",
                @"{""first"":""Pittman"",""last"":""Shepherd"",""age"":35,""height"":61,""income"":26887}",
                @"{""first"":""Wright"",""last"":""Rojas"",""age"":35,""height"":73,""income"":76487}",
                @"{""first"":""Lynne"",""last"":""Waters"",""age"":27,""height"":60,""income"":22926}",
                @"{""first"":""Corina"",""last"":""Shelton"",""age"":29,""height"":78,""income"":67379}",
                @"{""first"":""Alvarez"",""last"":""Barr"",""age"":29,""height"":59,""income"":34698}",
                @"{""first"":""Melinda"",""last"":""Mccoy"",""age"":24,""height"":63,""income"":69811}",
                @"{""first"":""Chelsea"",""last"":""Bolton"",""age"":20,""height"":63,""income"":47698}",
                @"{""first"":""English"",""last"":""Ingram"",""age"":28,""height"":50,""income"":94977}",
                @"{""first"":""Vance"",""last"":""Thomas"",""age"":30,""height"":49,""income"":67638}",
                @"{""first"":""Howell"",""last"":""Joyner"",""age"":34,""height"":78,""income"":65547}",
                @"{""first"":""Ofelia"",""last"":""Chapman"",""age"":23,""height"":82,""income"":85049}",
                @"{""first"":""Downs"",""last"":""Adams"",""age"":28,""height"":76,""income"":19373}",
                @"{""first"":""Terrie"",""last"":""Bryant"",""age"":32,""height"":55,""income"":79024}",
                @"{""first"":""Jeanie"",""last"":""Carson"",""age"":26,""height"":52,""income"":68293}",
                @"{""first"":""Hazel"",""last"":""Bean"",""age"":40,""height"":70,""income"":46028}",
                @"{""first"":""Dominique"",""last"":""Norman"",""age"":25,""height"":50,""income"":59445}",
                @"{""first"":""Lyons"",""last"":""Patterson"",""age"":36,""height"":64,""income"":71748}",
                @"{""first"":""Catalina"",""last"":""Cantrell"",""age"":30,""height"":78,""income"":16999}",
                @"{""first"":""Craft"",""last"":""Head"",""age"":30,""height"":49,""income"":10542}",
                @"{""first"":""Suzanne"",""last"":""Gilliam"",""age"":36,""height"":77,""income"":7511}",
                @"{""first"":""Pamela"",""last"":""Merritt"",""age"":30,""height"":81,""income"":80653}",
                @"{""first"":""Haynes"",""last"":""Ayala"",""age"":38,""height"":65,""income"":85832}",
                @"{""first"":""Teri"",""last"":""Martin"",""age"":40,""height"":83,""income"":27839}",
                @"{""first"":""Susanne"",""last"":""Short"",""age"":25,""height"":57,""income"":48957}",
                @"{""first"":""Rosalie"",""last"":""Camacho"",""age"":24,""height"":83,""income"":30313}",
                @"{""first"":""Walls"",""last"":""Bray"",""age"":28,""height"":74,""income"":21616}",
                @"{""first"":""Norris"",""last"":""Bates"",""age"":23,""height"":59,""income"":13631}",
                @"{""first"":""Wendy"",""last"":""King"",""age"":38,""height"":48,""income"":19845}",
                @"{""first"":""Deena"",""last"":""Ramsey"",""age"":20,""height"":66,""income"":49665}",
                @"{""first"":""Richmond"",""last"":""Meadows"",""age"":36,""height"":59,""income"":43244}",
                @"{""first"":""Burks"",""last"":""Whitley"",""age"":25,""height"":55,""income"":39974}",
                @"{""first"":""Gilliam"",""last"":""George"",""age"":37,""height"":82,""income"":47114}",
                @"{""first"":""Marcy"",""last"":""Harding"",""age"":33,""height"":80,""income"":20316}",
                @"{""first"":""Curtis"",""last"":""Gomez"",""age"":31,""height"":50,""income"":69085}",
                @"{""first"":""Lopez"",""last"":""Burt"",""age"":34,""height"":79,""income"":37577}",
                @"{""first"":""Nell"",""last"":""Nixon"",""age"":37,""height"":58,""income"":67999}",
                @"{""first"":""Sonja"",""last"":""Lamb"",""age"":37,""height"":53,""income"":92553}",
                @"{""first"":""Owens"",""last"":""Fischer"",""age"":40,""height"":48,""income"":75199}",
                @"{""first"":""Ortega"",""last"":""Padilla"",""age"":28,""height"":55,""income"":29126}",
                @"{""first"":""Stacie"",""last"":""Velez"",""age"":20,""height"":56,""income"":45292}",
                @"{""first"":""Brennan"",""last"":""Craig"",""age"":38,""height"":65,""income"":37445}"
            };

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                /*CollectionTypes.SinglePartition |*/ CollectionTypes.MultiPartition,
                documents,
                this.TestNonValueAggregates);
        }

        private async Task TestNonValueAggregates(
            Container container,
            IEnumerable<Document> documents)
        {
            IEnumerable<JToken> documentsAsJTokens = documents.Select(document => JToken.FromObject(document));

            // ------------------------------------------
            // Positive
            // ------------------------------------------

            List<Tuple<string, JToken>> queryAndExpectedAggregation = new List<Tuple<string, JToken>>()
            {
                // ------------------------------------------
                // Simple Aggregates without a value
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT SUM(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Sum(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT COUNT(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Where(document => document["age"] != null).Count()
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MAX(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT AVG(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Average(document => document["age"].Value<double>())
                        }
                    }),
                
                // ------------------------------------------
                // Simple aggregates with alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT SUM(c.age) as sum_age FROM c",
                    new JObject
                    {
                        {
                            "sum_age",
                            documentsAsJTokens.Sum(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT COUNT(c.age) as count_age FROM c",
                    new JObject
                    {
                        {
                            "count_age",
                            documentsAsJTokens.Where(document => document["age"] != null).Count()
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) as min_age FROM c",
                    new JObject
                    {
                        {
                            "min_age",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MAX(c.age) as max_age FROM c",
                    new JObject
                    {
                        {
                            "max_age",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT AVG(c.age) as avg_age FROM c",
                    new JObject
                    {
                        {
                            "avg_age",
                            documentsAsJTokens.Average(document => document["age"].Value<double>())
                        }
                    }),
                
                // ------------------------------------------
                // Multiple Aggregates without alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age), MAX(c.age) FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "$2",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                // ------------------------------------------
                // Multiple Aggregates with alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) as min_age, MAX(c.age) as max_age FROM c",
                    new JObject
                    {
                        {
                            "min_age",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "max_age",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                // ------------------------------------------
                // Multiple Aggregates with and without alias
                // ------------------------------------------

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age), MAX(c.age) as max_age FROM c",
                    new JObject
                    {
                        {
                            "$1",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "max_age",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),

                new Tuple<string, JToken>(
                    "SELECT MIN(c.age) as min_age, MAX(c.age) FROM c",
                    new JObject
                    {
                        {
                            "min_age",
                            documentsAsJTokens.Min(document => document["age"].Value<double>())
                        },
                        {
                            "$1",
                            documentsAsJTokens.Max(document => document["age"].Value<double>())
                        }
                    }),
            };

            // Test query correctness.
            foreach ((string query, JToken expectedAggregation) in queryAndExpectedAggregation)
            {
                foreach (int maxItemCount in new int[] { 1, 5, 10 })
                {
                    List<JToken> actual = await RunQueryAsync<JToken>(
                        container: container,
                        query: query,
                        queryRequestOptions: new QueryRequestOptions()
                        {
                            MaxBufferedItemCount = 100,
                            MaxConcurrency = 100,
                            MaxItemCount = maxItemCount,
                        });

                    Assert.AreEqual(1, actual.Count());

                    Assert.IsTrue(
                       JsonTokenEqualityComparer.Value.Equals(actual.First(), expectedAggregation),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"Actual: {JsonConvert.SerializeObject(actual.First())}" +
                       $"Expected: {JsonConvert.SerializeObject(expectedAggregation)}");
                }
            }

            // ------------------------------------------
            // Negative
            // ------------------------------------------

            List<string> notSupportedQueries = new List<string>()
            {
                "SELECT MIN(c.age) + MAX(c.age) FROM c",
                "SELECT MIN(c.age) / 2 FROM c",
            };

            foreach (string query in notSupportedQueries)
            {
                try
                {
                    List<JToken> actual = await QueryWithoutContinuationTokensAsync<JToken>(
                        container: container,
                        query: query,
                        queryRequestOptions: new QueryRequestOptions()
                        {
                            MaxBufferedItemCount = 100,
                            MaxConcurrency = 100,
                        });

                    Assert.Fail("Expected Query To Fail");
                }
                catch (Exception)
                {
                    // Do Nothing
                }
            }
        }
    }
}
