//-----------------------------------------------------------------------
// <copyright file="GremlinScenarioTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for CosmosDB Gremlin use case scenarios of CosmosElement and JsonNavigator interfaces.
    /// </summary>
    [TestClass]
    public abstract class GremlinScenarioTests
    {
        private const string PartitionKeyPropertyName = "myPartitionKey";

        /// <summary>
        /// Gremlin keywords for use in creating scenario Json document structure.
        /// </summary>
        private static class GremlinKeywords
        {
            internal const string KW_DOC_ID = "id";

            internal const string KW_EDGE_LABEL = "label";
            internal const string KW_EDGE_SINKV = "_sink";
            internal const string KW_EDGE_SINKV_LABEL = "_sinkLabel";
            internal const string KW_EDGE_SINKV_PARTITION = "_sinkPartition";

            internal const string KW_EDGEDOC_IDENTIFIER = "_isEdge";
            internal const string KW_EDGEDOC_ISPKPROPERTY = "_isPkEdgeProperty";
            internal const string KW_EDGEDOC_VERTEXID = "_vertexId";
            internal const string KW_EDGEDOC_VERTEXLABEL = "_vertexLabel";

            internal const string KW_PROPERTY_ID = "id";
            internal const string KW_PROPERTY_VALUE = "_value";
            internal const string KW_PROPERTY_META = "_meta";

            internal const string KW_VERTEX_LABEL = "label";
        }

        internal abstract JsonSerializationFormat SerializationFormat { get; }

        /// <summary>
        /// Test read path for Gremlin edge documents by:
        /// - Creating a serialized edge document using eager <see cref="CosmosElement"/>s.
        /// - Navigating and verifying the structure and values of the serialized edge document using lazy <see cref="CosmosElement"/>s.
        /// </summary>
        [TestMethod]
        public virtual void SerializeAndDeserializeGremlinEdgeDocument()
        {
            this.SerializeAndDeserializeEdgeDocumentTest(this.SerializationFormat);
        }

        /// <summary>
        /// Test read path for Gremlin vertex documents by:
        /// - Creating a serialized vertex document using eager <see cref="CosmosElement"/>s.
        /// - Navigating and verifying structure and values of the serialized vertex document using lazy <see cref="CosmosElement"/>s.
        /// </summary>
        [TestMethod]
        public virtual void SerializeAndDeserializeGremlinVertexDocument()
        {
            this.SerializeAndDeserializeVertexDocumentTest(this.SerializationFormat);
        }

        /// <summary>
        /// Test write path for Gremlin vertex documents by:
        /// - Creating a serialized vertex document using eager <see cref="CosmosElement"/>s.
        /// - Navigating the serialized vertex document using lazy <see cref="CosmosElement"/>s.
        /// - Assembling a modified vertex document structure using a mix of lazy and eager <see cref="CosmosElement"/>s.
        /// - Serializing and verifying the contents of the modified vertex document.
        /// </summary>
        [TestMethod]
        public virtual void DeserializeModifyAndSerializeVertexDocument()
        {
            this.DeserializeModifyAndSerializeVertexDocumentTest(this.SerializationFormat);
        }

        /// <summary>
        /// Test getting the internal <see cref="CosmosElement"/>s directly from a <see cref="QueryResponse{T}"/> without re-serializing them.
        /// </summary>
        [TestMethod]
        public virtual void GetCosmosElementsFromQueryResponse()
        {
            this.GetCosmosElementsFromQueryResponseTest(this.SerializationFormat);
        }

        /// <summary>
        /// Test getting other re-serialized objects besides CosmosElement from a <see cref="QueryResponse{T}"/>.
        /// </summary>
        [TestMethod]
        public virtual void GetDeserializedObjectsFromQueryResponse()
        {
            this.GetDeserializedObjectsFromQueryResponseTest(this.SerializationFormat);
        }

        internal void SerializeAndDeserializeEdgeDocumentTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string idName = "id";
            const string idValue = "e_0";
            const string pkValue = "pk_0";
            const string labelName = "label";
            const string labelValue = "l_0";
            const string vertexIdValue = "v_0";
            const string vertexLabelValue = "l_1";
            const string sinkIdValue = "v_1";
            const string sinkLabelValue = "l_2";
            const string sinkPartitionValue = "pk_1";
            const bool isEdgeValue = true;
            const bool isPkEdgePropertyValue = true;
            const string boolName = "myBool";
            const bool boolValue = true;
            const string intName = "myInteger";
            const int intValue = 12345;
            const string longName = "myLong";
            const long longValue = 67890L;
            const string floatName = "myFloatingPoint";
            const float floatValue = 123.4f;
            const string doubleName = "myDouble";
            const double doubleValue = 56.78;
            const string stringName = "myString";
            const string stringValue = "str_0";

            Dictionary<string, CosmosElement> edgeDocumentProperties = new Dictionary<string, CosmosElement>()
            {
                { idName, CosmosString.Create(idValue) },
                { GremlinScenarioTests.PartitionKeyPropertyName, CosmosString.Create(pkValue) },
                { labelName, CosmosString.Create(labelValue) },
                { GremlinKeywords.KW_EDGEDOC_VERTEXID, CosmosString.Create(vertexIdValue) },
                { GremlinKeywords.KW_EDGEDOC_VERTEXLABEL, CosmosString.Create(vertexLabelValue) },
                { GremlinKeywords.KW_EDGE_SINKV, CosmosString.Create(sinkIdValue) },
                { GremlinKeywords.KW_EDGE_SINKV_LABEL, CosmosString.Create(sinkLabelValue) },
                { GremlinKeywords.KW_EDGE_SINKV_PARTITION, CosmosString.Create(sinkPartitionValue) },
                { GremlinKeywords.KW_EDGEDOC_IDENTIFIER, CosmosBoolean.Create(isEdgeValue) },
                { GremlinKeywords.KW_EDGEDOC_ISPKPROPERTY, CosmosBoolean.Create(isPkEdgePropertyValue) },
                { boolName, CosmosBoolean.Create(boolValue) },
                { intName, CosmosNumber64.Create(intValue) },
                { longName, CosmosNumber64.Create(longValue) },
                { floatName, CosmosNumber64.Create(floatValue) },
                { doubleName, CosmosNumber64.Create(doubleValue) },
                { stringName, CosmosString.Create(stringValue) },
            };

            CosmosObject edgeEagerObject = CosmosObject.Create(edgeDocumentProperties);

            // Serialize the edge object into a document using the specified serialization format
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            edgeEagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> jsonResult = jsonWriter.GetResult();
            Assert.IsTrue(jsonResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized edge document using lazy CosmosElements
            CosmosElement rootLazyElement = CosmosElement.CreateFromBuffer(jsonResult);

            // Validate the expected edge document structure/values

            // Root edge document object
            CosmosObject edgeLazyObject = rootLazyElement as CosmosObject;
            Assert.IsNotNull(edgeLazyObject, $"Edge document root is not {nameof(CosmosObject)}.");
            Assert.AreEqual(edgeDocumentProperties.Count, edgeLazyObject.Count);

            // Edge system document properties
            CosmosString idLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, idName);
            Assert.AreEqual(idValue, idLazyString.Value.ToString());

            CosmosString pkLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinScenarioTests.PartitionKeyPropertyName);
            Assert.AreEqual(pkValue, pkLazyString.Value.ToString());

            CosmosString labelLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, labelName);
            Assert.AreEqual(labelValue, labelLazyString.Value.ToString());

            CosmosString vertexIdLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_VERTEXID);
            Assert.AreEqual(vertexIdValue, vertexIdLazyString.Value.ToString());

            CosmosString vertexLabelLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_VERTEXLABEL);
            Assert.AreEqual(vertexLabelValue, vertexLabelLazyString.Value.ToString());

            CosmosString sinkIdLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGE_SINKV);
            Assert.AreEqual(sinkIdValue, sinkIdLazyString.Value.ToString());

            CosmosString sinkLabelLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGE_SINKV_LABEL);
            Assert.AreEqual(sinkLabelValue, sinkLabelLazyString.Value.ToString());

            CosmosString sinkPartitionLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGE_SINKV_PARTITION);
            Assert.AreEqual(sinkPartitionValue, sinkPartitionLazyString.Value.ToString());

            CosmosBoolean isEdgeLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_IDENTIFIER);
            Assert.AreEqual(isEdgeValue, isEdgeLazyBool.Value);

            CosmosBoolean isPkEdgePropertyLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_ISPKPROPERTY);
            Assert.AreEqual(isPkEdgePropertyValue, isPkEdgePropertyLazyBool.Value);

            // Edge user properties

            CosmosBoolean boolValueLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(edgeLazyObject, boolName);
            Assert.AreEqual(boolValue, boolValueLazyBool.Value);

            CosmosNumber intValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, intName);
            Assert.IsTrue(intValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(intValueLazyNumber.Value.IsInteger);
            Assert.AreEqual((long)intValue, intValueLazyNumber.Value);

            CosmosNumber longValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, longName);
            Assert.IsTrue(intValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(intValueLazyNumber.Value.IsInteger);
            Assert.AreEqual(longValue, longValueLazyNumber.Value);

            CosmosNumber floatValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, floatName);
            Assert.IsTrue(intValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(floatValueLazyNumber.Value.IsDouble);
            Assert.AreEqual((double)floatValue, floatValueLazyNumber.Value);

            CosmosNumber doubleValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, doubleName);
            Assert.IsTrue(intValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(doubleValueLazyNumber.Value.IsDouble);
            Assert.AreEqual((double)doubleValue, doubleValueLazyNumber.Value);

            CosmosString stringValueLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, stringName);
            Assert.AreEqual(stringValue, stringValueLazyString.Value.ToString());
        }

        internal void SerializeAndDeserializeVertexDocumentTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string idName = "id";
            const string idValue = "v_0";
            const string pkValue = "pk_0";
            const string labelName = "label";
            const string labelValue = "l_0";
            const string boolName = "myBool";
            const string boolId = "3648bdcc-5113-43f8-86dd-c19fe793a2f8";
            const bool boolValue = true;
            const string intName = "myInteger";
            const string intId = "7546f541-a003-4e69-a25c-608372ed1321";
            const int intValue = 12345;
            const string longId = "b119c62a-82a2-48b2-b293-9963fa99fbe2";
            const long longValue = 67890L;
            const string floatName = "myFloatingPoint";
            const string floatId = "98d27280-70ee-4edd-8461-7633a328539a";
            const float floatValue = 123.4f;
            const string doubleId = "f9bfcc22-221a-4c92-b5b9-be53cdedb092";
            const double doubleValue = 56.78;
            const string stringName = "myString";
            const string stringId = "6bb8ae5b-19ca-450e-b369-922a34c02729";
            const string stringValue = "str_0";
            const string metaProperty0Name = "myMetaProperty0";
            const string metaProperty0Value = "m_0";
            const string metaProperty1Name = "myMetaProperty1";
            const int metaProperty1Value = 123;

            // Compose the vertex document using eager CosmosElements
            Dictionary<string, CosmosElement> vertexDocumentProperties = new Dictionary<string, CosmosElement>()
            {
                { idName, CosmosString.Create(idValue) },
                { GremlinScenarioTests.PartitionKeyPropertyName, CosmosString.Create(pkValue) },
                { labelName, CosmosString.Create(labelValue) },
                {
                    boolName,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(boolId), CosmosBoolean.Create(boolValue)),
                        }
                    )
                },
                {
                    intName,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(intId), CosmosNumber64.Create(intValue)),
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(longId), CosmosNumber64.Create(longValue)),
                        }
                    )
                },
                {
                    floatName,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(floatId), CosmosNumber64.Create(floatValue)),
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(doubleId), CosmosNumber64.Create(doubleValue)),
                        }
                    )
                },
                {
                    stringName,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(
                                CosmosString.Create(stringId),
                                CosmosString.Create(stringValue),
                                Tuple.Create<string, CosmosElement>(metaProperty0Name, CosmosString.Create(metaProperty0Value)),
                                Tuple.Create<string, CosmosElement>(metaProperty1Name, CosmosNumber64.Create(metaProperty1Value))),
                        }
                    )
                },
            };

            CosmosObject vertexEagerObject = CosmosObject.Create(vertexDocumentProperties);

            // Serialize the vertex object into a document using the specified serialization format
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            vertexEagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> jsonResult = jsonWriter.GetResult();
            Assert.IsTrue(jsonResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized vertex document using lazy CosmosElements
            CosmosElement rootLazyElement = CosmosElement.CreateFromBuffer(jsonResult);

            // Validate the expected vertex document structure/values

            // Root vertex document object
            CosmosObject vertexLazyObject = rootLazyElement as CosmosObject;
            Assert.IsNotNull(vertexLazyObject, $"Vertex document root is not {nameof(CosmosObject)}.");
            Assert.AreEqual(vertexDocumentProperties.Count, vertexLazyObject.Count);

            // Vertex system document properties
            CosmosString idLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, idName);
            Assert.AreEqual(idValue, idLazyString.Value.ToString());

            CosmosString pkLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, GremlinScenarioTests.PartitionKeyPropertyName);
            Assert.AreEqual(pkValue, pkLazyString.Value.ToString());

            CosmosString labelLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, labelName);
            Assert.AreEqual(labelValue, labelLazyString.Value.ToString());

            // Vertex user properties
            CosmosArray boolLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, boolName);
            Assert.AreEqual(1, boolLazyArray.Count);

            // Bool value(s)
            CosmosObject boolValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(boolLazyArray, 0);
            CosmosString boolValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(boolValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(boolId, boolValue0IdLazyString.Value.ToString());
            CosmosBoolean boolValue0ValueLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(boolValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(boolValue, boolValue0ValueLazyBool.Value);

            CosmosArray intLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, intName);
            Assert.AreEqual(2, intLazyArray.Count);

            // Integer value(s)
            CosmosObject intValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(intLazyArray, 0);
            CosmosString intValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(intValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(intId, intValue0IdLazyString.Value.ToString());
            CosmosNumber intValue0ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(intValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.IsTrue(intValue0ValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(intValue0ValueLazyNumber.Value.IsInteger);
            Assert.AreEqual((long)intValue, intValue0ValueLazyNumber.Value);

            CosmosObject intValue1LazyObject = this.GetAndAssertArrayValue<CosmosObject>(intLazyArray, 1);
            CosmosString intValue1IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(intValue1LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(longId, intValue1IdLazyString.Value.ToString());
            CosmosNumber intValue1ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(intValue1LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.IsTrue(intValue1ValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(intValue1ValueLazyNumber.Value.IsInteger);
            Assert.AreEqual(longValue, intValue1ValueLazyNumber.Value);

            // Floating point value(s)
            CosmosArray floatLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, floatName);
            Assert.AreEqual(2, floatLazyArray.Count);

            CosmosObject floatValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(floatLazyArray, 0);
            CosmosString floatValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(floatValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(floatId, floatValue0IdLazyString.Value.ToString());
            CosmosNumber floatValue0ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(floatValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.IsTrue(floatValue0ValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(floatValue0ValueLazyNumber.Value.IsDouble);
            Assert.AreEqual((double)floatValue, floatValue0ValueLazyNumber.Value);

            CosmosObject floatValue1LazyObject = this.GetAndAssertArrayValue<CosmosObject>(floatLazyArray, 1);
            CosmosString floatValue1IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(floatValue1LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(doubleId, floatValue1IdLazyString.Value.ToString());
            CosmosNumber floatValue1ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(floatValue1LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.IsTrue(floatValue1ValueLazyNumber is CosmosNumber64);
            Assert.IsTrue(floatValue1ValueLazyNumber.Value.IsDouble);
            Assert.AreEqual(doubleValue, floatValue1ValueLazyNumber.Value);

            // String value(s)
            CosmosArray stringLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, stringName);
            Assert.AreEqual(1, stringLazyArray.Count);

            CosmosObject stringValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(stringLazyArray, 0);
            CosmosString stringValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(stringValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(stringId, stringValue0IdLazyString.Value.ToString());
            CosmosString stringValue0ValueLazyString = this.GetAndAssertObjectProperty<CosmosString>(stringValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(stringValue, stringValue0ValueLazyString.Value.ToString());

            // String value meta-properties
            CosmosObject stringValue0MetaLazyObject = this.GetAndAssertObjectProperty<CosmosObject>(stringValue0LazyObject, GremlinKeywords.KW_PROPERTY_META);
            Assert.AreEqual(2, stringValue0MetaLazyObject.Count);

            CosmosString stringValue0MetaValue0LazyString = this.GetAndAssertObjectProperty<CosmosString>(stringValue0MetaLazyObject, metaProperty0Name);
            Assert.AreEqual(metaProperty0Value, stringValue0MetaValue0LazyString.Value.ToString());

            CosmosNumber stringValue0MetaValue1LazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(stringValue0MetaLazyObject, metaProperty1Name);
            Assert.IsTrue(stringValue0MetaValue1LazyNumber is CosmosNumber64);
            Assert.IsTrue(stringValue0MetaValue1LazyNumber.Value.IsInteger);
            Assert.AreEqual((long)metaProperty1Value, stringValue0MetaValue1LazyNumber.Value);
        }

        internal void DeserializeModifyAndSerializeVertexDocumentTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string idName = "id";
            const string idValue = "v_0";
            const string pkValue = "pk_0";
            const string labelName = "label";
            const string labelValue = "l_0";
            const string property1Name = "p_0";
            const string property1Value1Id = "3648bdcc-5113-43f8-86dd-c19fe793a2f8";
            const string property1Value1 = "p_0_v_0";
            const string property1Value2Id = "7546f541-a003-4e69-a25c-608372ed1321";
            const long property1Value2 = 1234;
            const string property2Name = "p_1";
            const string property2Value1Id = "b119c62a-82a2-48b2-b293-9963fa99fbe2";
            const double property2Value1 = 34.56;
            const string property3Name = "p_2";
            const string property3Value1Id = "98d27280-70ee-4edd-8461-7633a328539a";
            const bool property3Value1 = true;
            const string property4Name = "p_3";
            const string property4Value1Id = "f9bfcc22-221a-4c92-b5b9-be53cdedb092";
            const string property4Value1 = "p_3_v_0";

            // Compose the initial vertex document using eager CosmosElements
            Dictionary<string, CosmosElement> initialVertexDocumentProperties = new Dictionary<string, CosmosElement>()
            {
                { idName, CosmosString.Create(idValue) },
                { GremlinScenarioTests.PartitionKeyPropertyName, CosmosString.Create(pkValue) },
                { labelName, CosmosString.Create(labelValue) },
                {
                    property1Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property1Value1Id), CosmosString.Create(property1Value1)),
                        }
                    )
                },
                {
                    property2Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property2Value1Id), CosmosNumber64.Create(property2Value1)),
                        }
                    )
                },
                {
                    property3Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property3Value1Id), CosmosBoolean.Create(property3Value1)),
                        }
                    )
                },
            };

            CosmosObject initialVertexEagerObject = CosmosObject.Create(initialVertexDocumentProperties);

            // Serialize the initial vertex object into a document using the specified serialization format
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            initialVertexEagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> initialJsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(initialJsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized vertex document using lazy CosmosElements
            CosmosElement rootLazyElement = CosmosElement.CreateFromBuffer(initialJsonWriterResult);

            // Root vertex document object
            CosmosObject vertexLazyObject = rootLazyElement as CosmosObject;
            Assert.IsNotNull(vertexLazyObject, $"Vertex document root is not {nameof(CosmosObject)}.");
            Assert.AreEqual(initialVertexDocumentProperties.Count, vertexLazyObject.Count);

            CosmosString idLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, idName);
            CosmosString pkLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, GremlinScenarioTests.PartitionKeyPropertyName);
            CosmosString labelLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, labelName);
            CosmosArray property2Array = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, property2Name);

            // Compose a new vertex document using a combination of lazy and eager CosmosElements
            Dictionary<string, CosmosElement> modifiedVertexDocumentProperties = new Dictionary<string, CosmosElement>()
            {
                { idName, idLazyString },
                { GremlinScenarioTests.PartitionKeyPropertyName, pkLazyString },
                { labelName, labelLazyString },

                // Property 1 is modified with a new value
                {
                    property1Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property1Value2Id), CosmosNumber64.Create(property1Value2)),
                        }
                    )
                },

                // Property 2 is unmodified
                { property2Name, property2Array },

                // Property 3 is deleted

                // Property 4 is newly added
                {
                    property4Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property4Value1Id), CosmosString.Create(property4Value1)),
                        }
                    )
                },
            };

            CosmosObject modifiedVertexEagerObject = CosmosObject.Create(modifiedVertexDocumentProperties);

            // Serialize the modified vertex object into a document using the specified serialization format
            jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            modifiedVertexEagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> modifiedJsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(modifiedJsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            // Compose an expected vertex document using eager CosmosElements
            Dictionary<string, CosmosElement> expectedVertexDocumentProperties = new Dictionary<string, CosmosElement>()
            {
                { idName, CosmosString.Create(idValue) },
                { GremlinScenarioTests.PartitionKeyPropertyName, CosmosString.Create(pkValue) },
                { labelName, CosmosString.Create(labelValue) },
                {
                    property1Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property1Value2Id), CosmosNumber64.Create(property1Value2)),
                        }
                    )
                },
                {
                    property2Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property2Value1Id), CosmosNumber64.Create(property2Value1)),
                        }
                    )
                },
                {
                    property4Name,
                    CosmosArray.Create(
                        new CosmosElement[]
                        {
                            this.CreateVertexPropertySingleComplexValue(CosmosString.Create(property4Value1Id), CosmosString.Create(property4Value1)),
                        }
                    )
                },
            };

            CosmosObject expectedVertexEagerObject = CosmosObject.Create(expectedVertexDocumentProperties);

            // Serialize the initial vertex object into a document using the specified serialization format
            jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            expectedVertexEagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> expectedJsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(expectedJsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            // Verify that the modified serialized document matches the expected serialized document
            Assert.IsTrue(modifiedJsonWriterResult.Span.SequenceEqual(expectedJsonWriterResult.Span));
        }

        internal void GetCosmosElementsFromQueryResponseTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string vertex1Id = "v_0";
            const string vertex2Id = "v_1";
            const string vertex1Label = "l_0";
            const string vertex2Label = "l_1";
            const string vertex1PkValue = "pk_0";
            const string vertex2PkValue = "pk_1";
            const string property1Name = "p_0";
            const string vertex1Property1Value = "v_0_p_0_v_0";
            const string vertex2Property1Value = "v_1_p_0_v_0";
            const string property2Name = "p_1";
            const double vertex1Property2Value = 12.34;
            const long vertex2Property2Value = 5678;

            // Compose two initial vertex documents using eager CosmosElements
            CosmosObject initialVertex1EagerObject = this.CreateVertexDocument(
                vertex1Id,
                vertex1Label,
                GremlinScenarioTests.PartitionKeyPropertyName,
                vertex1PkValue,
                new Tuple<string, IEnumerable<object>>[]
                {
                    Tuple.Create<string, IEnumerable<object>>(property1Name, new object[] { vertex1Property1Value }),
                    Tuple.Create<string, IEnumerable<object>>(property2Name, new object[] { vertex1Property2Value }),
                });
            CosmosObject initialVertex2EagerObject = this.CreateVertexDocument(
                vertex2Id,
                vertex2Label,
                GremlinScenarioTests.PartitionKeyPropertyName,
                vertex2PkValue,
                new Tuple<string, IEnumerable<object>>[]
                {
                    Tuple.Create<string, IEnumerable<object>>(property1Name, new object[] { vertex2Property1Value }),
                    Tuple.Create<string, IEnumerable<object>>(property2Name, new object[] { vertex2Property2Value }),
                });

            // Serialize the initial vertex object into a document using the specified serialization format
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            initialVertex1EagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> vertex1JsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(vertex1JsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            initialVertex2EagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> vertex2JsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(vertex2JsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized vertex documents using lazy CosmosElements
            CosmosElement vertex1LazyObject = CosmosElement.CreateFromBuffer(vertex1JsonWriterResult);
            CosmosElement vertex2LazyObject = CosmosElement.CreateFromBuffer(vertex2JsonWriterResult);

            // Create a CosmosElement-typed QueryResponse backed by the vertex document CosmosElements
            CosmosArray vertexArray = CosmosArray.Create(
                new CosmosElement[]
                {
                    vertex1LazyObject,
                    vertex2LazyObject,
                });
            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                vertexArray,
                count: 2,
                serializationOptions: null,
                trace: NoOpTrace.Singleton,
                responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(
                    sourceHeaders: null,
                    resourceType: ResourceType.Document,
                    containerRid: GremlinScenarioTests.CreateRandomString(10)));
            QueryResponse<CosmosElement> cosmosElementQueryResponse =
                QueryResponse<CosmosElement>.CreateResponse<CosmosElement>(
                    queryResponse,
                    MockCosmosUtil.Serializer);

            // Assert that we are directly returned the lazy CosmosElements that we created earlier
            List<CosmosElement> responseCosmosElements = new List<CosmosElement>(cosmosElementQueryResponse.Resource);
            Assert.AreEqual(vertexArray.Count, responseCosmosElements.Count);
            Assert.AreSame(vertex1LazyObject, responseCosmosElements[0]);
            Assert.AreSame(vertex2LazyObject, responseCosmosElements[1]);
        }

        internal void GetDeserializedObjectsFromQueryResponseTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string vertex1Id = "v_0";
            const string vertex2Id = "v_1";
            const string vertex1Label = "l_0";
            const string vertex2Label = "l_1";
            const string vertex1PkValue = "pk_0";
            const string vertex2PkValue = "pk_1";
            const string property1Name = "p_0";
            const string vertex1Property1Value = "v_0_p_0_v_0";
            const string vertex2Property1Value = "v_1_p_0_v_0";
            const string property2Name = "p_1";
            const double vertex1Property2Value = 12.34;
            const long vertex2Property2Value = 5678;

            // Compose two initial vertex documents using eager CosmosElements
            CosmosObject initialVertex1EagerObject = this.CreateVertexDocument(
                vertex1Id,
                vertex1Label,
                GremlinScenarioTests.PartitionKeyPropertyName,
                vertex1PkValue,
                new Tuple<string, IEnumerable<object>>[]
                {
                    Tuple.Create<string, IEnumerable<object>>(property1Name, new object[] { vertex1Property1Value }),
                    Tuple.Create<string, IEnumerable<object>>(property2Name, new object[] { vertex1Property2Value }),
                });
            CosmosObject initialVertex2EagerObject = this.CreateVertexDocument(
                vertex2Id,
                vertex2Label,
                GremlinScenarioTests.PartitionKeyPropertyName,
                vertex2PkValue,
                new Tuple<string, IEnumerable<object>>[]
                {
                    Tuple.Create<string, IEnumerable<object>>(property1Name, new object[] { vertex2Property1Value }),
                    Tuple.Create<string, IEnumerable<object>>(property2Name, new object[] { vertex2Property2Value }),
                });

            // Serialize the initial vertex object into a document using the specified serialization format
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            initialVertex1EagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> vertex1JsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(vertex1JsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            jsonWriter = JsonWriter.Create(jsonSerializationFormat);
            initialVertex2EagerObject.WriteTo(jsonWriter);
            ReadOnlyMemory<byte> vertex2JsonWriterResult = jsonWriter.GetResult();
            Assert.IsTrue(vertex2JsonWriterResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized vertex documents using lazy CosmosElements
            CosmosElement vertex1LazyObject = CosmosElement.CreateFromBuffer(vertex1JsonWriterResult);
            CosmosElement vertex2LazyObject = CosmosElement.CreateFromBuffer(vertex2JsonWriterResult);

            // Create a dynamically-typed QueryResponse backed by the vertex document CosmosElements
            CosmosArray vertexArray = CosmosArray.Create(
                new CosmosElement[]
                {
                    vertex1LazyObject,
                    vertex2LazyObject,
                });
            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                vertexArray,
                count: 2,
                serializationOptions: null,
                trace: NoOpTrace.Singleton,
                responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(
                    sourceHeaders: null,
                    resourceType: ResourceType.Document,
                    containerRid: GremlinScenarioTests.CreateRandomString(10)));
            QueryResponse<dynamic> cosmosElementQueryResponse =
                QueryResponse<dynamic>.CreateResponse<dynamic>(
                    queryResponse,
                    MockCosmosUtil.Serializer);

            // Assert that other objects (anything besides the lazy CosmosElements that we created earlier) are deserialized
            // from the backing CosmosElement contents rather than being directly returned as CosmosElements
            List<dynamic> responseCosmosElements = new List<dynamic>(cosmosElementQueryResponse.Resource);
            Assert.AreEqual(vertexArray.Count, responseCosmosElements.Count);
            Assert.AreNotSame(vertex1LazyObject, responseCosmosElements[0]);
            Assert.AreNotSame(vertex2LazyObject, responseCosmosElements[1]);
        }

        private CosmosObject CreateVertexDocument(string id, string label, string pkName, string pkValue, IEnumerable<Tuple<string, IEnumerable<object>>> userProperties)
        {
            Dictionary<string, CosmosElement> vertexDocumentProperties = new Dictionary<string, CosmosElement>()
            {
                { GremlinKeywords.KW_DOC_ID, CosmosString.Create(id) },
                { GremlinKeywords.KW_VERTEX_LABEL, CosmosString.Create(label) },
            };

            if (!string.IsNullOrEmpty(pkName) && !string.IsNullOrEmpty(pkValue))
            {
                vertexDocumentProperties.Add(pkName, CosmosString.Create(pkValue));
            }

            foreach (Tuple<string, IEnumerable<object>> userProperty in userProperties)
            {
                List<CosmosElement> singleValues = new List<CosmosElement>();
                foreach (object userPropertyValue in userProperty.Item2)
                {
                    string propertyId = Guid.NewGuid().ToString();
                    singleValues.Add(
                        this.CreateVertexPropertySingleComplexValue(
                            CosmosString.Create(propertyId),
                            this.CreateVertexPropertyPrimitiveValueElement(userPropertyValue)));
                }
            }

            return CosmosObject.Create(vertexDocumentProperties);
        }

        private CosmosElement CreateVertexPropertySingleComplexValue(
            CosmosString propertyIdElement,
            CosmosElement propertyValueElement,
            params Tuple<string, CosmosElement>[] metaPropertyPairs)
        {
            // Vertex complex property value
            Dictionary<string, CosmosElement> propertyValueMembers = new Dictionary<string, CosmosElement>()
            {
                { GremlinKeywords.KW_PROPERTY_ID, propertyIdElement },
                { GremlinKeywords.KW_PROPERTY_VALUE, propertyValueElement },
            };

            // (Optional) Meta-property object for the property value
            if (metaPropertyPairs.Length > 0)
            {
                CosmosObject metaElement = CosmosObject.Create(
                    metaPropertyPairs.ToDictionary(pair => pair.Item1, pair => pair.Item2));
                propertyValueMembers.Add(GremlinKeywords.KW_PROPERTY_META, metaElement);
            }

            return CosmosObject.Create(propertyValueMembers);
        }

        private CosmosElement CreateVertexPropertyPrimitiveValueElement(object value)
        {
            return value switch
            {
                bool boolValue => CosmosBoolean.Create(boolValue),
                double doubleValue => CosmosNumber64.Create(doubleValue),
                float floatValue => CosmosNumber64.Create(floatValue),
                int intValue => CosmosNumber64.Create(intValue),
                long longValue => CosmosNumber64.Create(longValue),
                string stringValue => CosmosString.Create(stringValue),
                _ => throw new AssertFailedException($"Invalid Gremlin property value object type: {value.GetType().Name}."),
            };
        }

        private static string CreateRandomString(int stringLength)
        {
            Assert.IsTrue(stringLength > 0, $"Random string length ({stringLength}) must be a positive value");

            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789=";

            Random rand = new Random();
            StringBuilder sb = new StringBuilder(stringLength);
            for (int i = 0; i < stringLength; i++)
            {
                sb.Append(validChars[rand.Next(validChars.Length)]);
            }

            return sb.ToString();
        }

        private T GetAndAssertObjectProperty<T>(CosmosObject cosmosObject, string propertyName)
            where T : CosmosElement
        {
            Assert.IsTrue(cosmosObject.TryGetValue(propertyName, out CosmosElement lazyElement), $"Object does not contain the {propertyName} property.");
            T lazyPropertyValue = lazyElement as T;
            Assert.IsNotNull(lazyPropertyValue, $"Object property {propertyName} is not {typeof(T).Name}.");
            return lazyPropertyValue;
        }

        private T GetAndAssertArrayValue<T>(CosmosArray cosmosArray, int index)
            where T : CosmosElement
        {
            T boolValue1LazyValue = cosmosArray[index] as T;
            Assert.IsNotNull(boolValue1LazyValue, $"Array value at position {index} is not {typeof(T).Name}.");
            return boolValue1LazyValue;
        }
    }
}