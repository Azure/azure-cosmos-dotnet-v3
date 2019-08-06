//-----------------------------------------------------------------------
// <copyright file="GremlinScenarioTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos.CosmosElements;
using Microsoft.Azure.Cosmos.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.Scenarios
{
    /// <summary>
    /// Tests for CosmosDB Gremlin use case scenarios of CosmosElement and JsonNavigator interfaces.
    /// </summary>
    [TestClass]
    public class GremlinScenarioTests
    {
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

        [TestMethod]
        public void SerializeAndDeserializeGremlinEdgeDocumentAsText()
        {
            this.SerializeAndDeserializeEdgeDocumentTest(JsonSerializationFormat.Text);
        }

        [TestMethod]
        public void SerializeAndDeserializeGremlinEdgeDocumentAsBinary()
        {
            this.SerializeAndDeserializeEdgeDocumentTest(JsonSerializationFormat.Binary);
        }

        [TestMethod]
        public void SerializeAndDeserializeGremlinEdgeDocumentAsHybridRow()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.SerializeAndDeserializeEdgeDocumentTest(JsonSerializationFormat.HybridRow));
        }

        [TestMethod]
        public void SerializeAndDeserializeGremlinVertexDocumentAsText()
        {
            this.SerializeAndDeserializeVertexDocumentTest(JsonSerializationFormat.Text);
        }

        [TestMethod]
        public void SerializeAndDeserializeGremlinVertexDocumentAsBinary()
        {
            this.SerializeAndDeserializeVertexDocumentTest(JsonSerializationFormat.Binary);
        }

        [TestMethod]
        public void SerializeAndDeserializeGremlinVertexDocumentAsHybridRow()
        {
            Assert.ThrowsException<ArgumentException>(
                () => this.SerializeAndDeserializeVertexDocumentTest(JsonSerializationFormat.HybridRow));
        }

        private void SerializeAndDeserializeEdgeDocumentTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string idName = "id";
            const string idValue = "e_0";
            const string pkName = "myPartitionKey";
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
                { pkName, CosmosString.Create(pkValue) },
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
            byte[] jsonResult = jsonWriter.GetResult();
            Assert.IsTrue(jsonResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized edge document using lazy CosmosElements
            CosmosElement rootLazyElement = CosmosElement.Create(jsonResult);

            // Validate the expected edge document structure/values

            // Root edge document object
            CosmosObject edgeLazyObject = rootLazyElement as CosmosObject;
            Assert.IsNotNull(edgeLazyObject, $"Edge document root is not {nameof(CosmosObject)}.");
            Assert.AreEqual(edgeDocumentProperties.Count, edgeLazyObject.Count);

            // Edge system document properties
            CosmosString idLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, idName);
            Assert.AreEqual(idValue, idLazyString.Value);

            CosmosString pkLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, pkName);
            Assert.AreEqual(pkValue, pkLazyString.Value);

            CosmosString labelLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, labelName);
            Assert.AreEqual(labelValue, labelLazyString.Value);

            CosmosString vertexIdLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_VERTEXID);
            Assert.AreEqual(vertexIdValue, vertexIdLazyString.Value);

            CosmosString vertexLabelLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_VERTEXLABEL);
            Assert.AreEqual(vertexLabelValue, vertexLabelLazyString.Value);

            CosmosString sinkIdLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGE_SINKV);
            Assert.AreEqual(sinkIdValue, sinkIdLazyString.Value);

            CosmosString sinkLabelLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGE_SINKV_LABEL);
            Assert.AreEqual(sinkLabelValue, sinkLabelLazyString.Value);

            CosmosString sinkPartitionLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, GremlinKeywords.KW_EDGE_SINKV_PARTITION);
            Assert.AreEqual(sinkPartitionValue, sinkPartitionLazyString.Value);

            CosmosBoolean isEdgeLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_IDENTIFIER);
            Assert.AreEqual(isEdgeValue, isEdgeLazyBool.Value);

            CosmosBoolean isPkEdgePropertyLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(edgeLazyObject, GremlinKeywords.KW_EDGEDOC_ISPKPROPERTY);
            Assert.AreEqual(isPkEdgePropertyValue, isPkEdgePropertyLazyBool.Value);

            // Edge user properties

            CosmosBoolean boolValueLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(edgeLazyObject, boolName);
            Assert.AreEqual(boolValue, boolValueLazyBool.Value);

            CosmosNumber intValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, intName);
            Assert.AreEqual(CosmosNumberType.Number64, intValueLazyNumber.NumberType);
            Assert.IsTrue(intValueLazyNumber.IsInteger);
            Assert.AreEqual((long)intValue, intValueLazyNumber.AsInteger().Value);

            CosmosNumber longValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, longName);
            Assert.AreEqual(CosmosNumberType.Number64, intValueLazyNumber.NumberType);
            Assert.IsTrue(intValueLazyNumber.IsInteger);
            Assert.AreEqual(longValue, longValueLazyNumber.AsInteger().Value);

            CosmosNumber floatValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, floatName);
            Assert.AreEqual(CosmosNumberType.Number64, floatValueLazyNumber.NumberType);
            Assert.IsTrue(floatValueLazyNumber.IsFloatingPoint);
            Assert.AreEqual((double)floatValue, floatValueLazyNumber.AsFloatingPoint().Value);

            CosmosNumber doubleValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(edgeLazyObject, doubleName);
            Assert.AreEqual(CosmosNumberType.Number64, doubleValueLazyNumber.NumberType);
            Assert.IsTrue(doubleValueLazyNumber.IsFloatingPoint);
            Assert.AreEqual((double)doubleValue, doubleValueLazyNumber.AsFloatingPoint().Value);

            CosmosString stringValueLazyString = this.GetAndAssertObjectProperty<CosmosString>(edgeLazyObject, stringName);
            Assert.AreEqual(stringValue, stringValueLazyString.Value);
        }

        private void SerializeAndDeserializeVertexDocumentTest(JsonSerializationFormat jsonSerializationFormat)
        {
            // Constants to use for vertex document property key/values
            const string idName = "id";
            const string idValue = "v_0";
            const string pkName = "myPartitionKey";
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
                { pkName, CosmosString.Create(pkValue) },
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
            byte[] jsonResult = jsonWriter.GetResult();
            Assert.IsTrue(jsonResult.Length > 0, "IJsonWriter result data is empty.");

            // Navigate into the serialized vertex document using lazy CosmosElements
            CosmosElement rootLazyElement = CosmosElement.Create(jsonResult);

            // Validate the expected vertex document structure/values

            // Root vertex document object
            CosmosObject vertexLazyObject = rootLazyElement as CosmosObject;
            Assert.IsNotNull(vertexLazyObject, $"Vertex document root is not {nameof(CosmosObject)}.");
            Assert.AreEqual(vertexDocumentProperties.Count, vertexLazyObject.Count);

            // Vertex system document properties
            CosmosString idLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, idName);
            Assert.AreEqual(idValue, idLazyString.Value);

            CosmosString pkLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, pkName);
            Assert.AreEqual(pkValue, pkLazyString.Value);

            CosmosString labelLazyString = this.GetAndAssertObjectProperty<CosmosString>(vertexLazyObject, labelName);
            Assert.AreEqual(labelValue, labelLazyString.Value);

            // Vertex user properties
            CosmosArray boolLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, boolName);
            Assert.AreEqual(1, boolLazyArray.Count);

            // Bool value(s)
            CosmosObject boolValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(boolLazyArray, 0);
            CosmosString boolValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(boolValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(boolId, boolValue0IdLazyString.Value);
            CosmosBoolean boolValue0ValueLazyBool = this.GetAndAssertObjectProperty<CosmosBoolean>(boolValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(boolValue, boolValue0ValueLazyBool.Value);

            CosmosArray intLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, intName);
            Assert.AreEqual(2, intLazyArray.Count);

            // Integer value(s)
            CosmosObject intValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(intLazyArray, 0);
            CosmosString intValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(intValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(intId, intValue0IdLazyString.Value);
            CosmosNumber intValue0ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(intValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(CosmosNumberType.Number64, intValue0ValueLazyNumber.NumberType);
            Assert.IsTrue(intValue0ValueLazyNumber.IsInteger);
            Assert.AreEqual((long)intValue, intValue0ValueLazyNumber.AsInteger().Value);

            CosmosObject intValue1LazyObject = this.GetAndAssertArrayValue<CosmosObject>(intLazyArray, 1);
            CosmosString intValue1IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(intValue1LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(longId, intValue1IdLazyString.Value);
            CosmosNumber intValue1ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(intValue1LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(CosmosNumberType.Number64, intValue1ValueLazyNumber.NumberType);
            Assert.IsTrue(intValue1ValueLazyNumber.IsInteger);
            Assert.AreEqual(longValue, intValue1ValueLazyNumber.AsInteger().Value);

            // Floating point value(s)
            CosmosArray floatLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, floatName);
            Assert.AreEqual(2, floatLazyArray.Count);

            CosmosObject floatValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(floatLazyArray, 0);
            CosmosString floatValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(floatValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(floatId, floatValue0IdLazyString.Value);
            CosmosNumber floatValue0ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(floatValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(CosmosNumberType.Number64, floatValue0ValueLazyNumber.NumberType);
            Assert.IsTrue(floatValue0ValueLazyNumber.IsFloatingPoint);
            Assert.AreEqual((double)floatValue, floatValue0ValueLazyNumber.AsFloatingPoint().Value);

            CosmosObject floatValue1LazyObject = this.GetAndAssertArrayValue<CosmosObject>(floatLazyArray, 1);
            CosmosString floatValue1IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(floatValue1LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(doubleId, floatValue1IdLazyString.Value);
            CosmosNumber floatValue1ValueLazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(floatValue1LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(CosmosNumberType.Number64, floatValue1ValueLazyNumber.NumberType);
            Assert.IsTrue(floatValue1ValueLazyNumber.IsFloatingPoint);
            Assert.AreEqual(doubleValue, floatValue1ValueLazyNumber.AsFloatingPoint().Value);

            // String value(s)
            CosmosArray stringLazyArray = this.GetAndAssertObjectProperty<CosmosArray>(vertexLazyObject, stringName);
            Assert.AreEqual(1, stringLazyArray.Count);

            CosmosObject stringValue0LazyObject = this.GetAndAssertArrayValue<CosmosObject>(stringLazyArray, 0);
            CosmosString stringValue0IdLazyString = this.GetAndAssertObjectProperty<CosmosString>(stringValue0LazyObject, GremlinKeywords.KW_PROPERTY_ID);
            Assert.AreEqual(stringId, stringValue0IdLazyString.Value);
            CosmosString stringValue0ValueLazyString = this.GetAndAssertObjectProperty<CosmosString>(stringValue0LazyObject, GremlinKeywords.KW_PROPERTY_VALUE);
            Assert.AreEqual(stringValue, stringValue0ValueLazyString.Value);

            // String value meta-properties
            CosmosObject stringValue0MetaLazyObject = this.GetAndAssertObjectProperty<CosmosObject>(stringValue0LazyObject, GremlinKeywords.KW_PROPERTY_META);
            Assert.AreEqual(2, stringValue0MetaLazyObject.Count);

            CosmosString stringValue0MetaValue0LazyString = this.GetAndAssertObjectProperty<CosmosString>(stringValue0MetaLazyObject, metaProperty0Name);
            Assert.AreEqual(metaProperty0Value, stringValue0MetaValue0LazyString.Value);

            CosmosNumber stringValue0MetaValue1LazyNumber = this.GetAndAssertObjectProperty<CosmosNumber>(stringValue0MetaLazyObject, metaProperty1Name);
            Assert.AreEqual(CosmosNumberType.Number64, stringValue0MetaValue1LazyNumber.NumberType);
            Assert.IsTrue(stringValue0MetaValue1LazyNumber.IsInteger);
            Assert.AreEqual((long)metaProperty1Value, stringValue0MetaValue1LazyNumber.AsInteger().Value);
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
