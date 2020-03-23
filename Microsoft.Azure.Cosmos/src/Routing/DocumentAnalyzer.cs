//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json.Linq;

    internal sealed class DocumentAnalyzer
    {
        /// <summary>
        /// Extracts effective <see cref="PartitionKeyInternal"/> from deserialized document.
        /// </summary>
        /// <remarks>
        /// This code doesn't do any validation, as it assumes that IndexingPolicy is valid, as it is coming from the backend.
        /// Expected format is "/prop1/prop2/?". No array expressions are expected.
        /// </remarks>
        /// <param name="document">Deserialized document to extract partition key value from.</param>
        /// <param name="partitionKeyDefinition">Information about partition key.</param>
        /// <returns>Instance of <see cref="PartitionKeyInternal"/>.</returns>
        public static PartitionKeyInternal ExtractPartitionKeyValue(Document document, PartitionKeyDefinition partitionKeyDefinition)
        {
            if (partitionKeyDefinition == null || partitionKeyDefinition.Paths.Count == 0)
            {
                return PartitionKeyInternal.Empty;
            }

            if (document.GetType().IsSubclassOf(typeof(Document)))
            {
                return DocumentAnalyzer.ExtractPartitionKeyValue(document, partitionKeyDefinition, (doc) => JToken.FromObject(doc));
            }
            else
            {
                return PartitionKeyInternal.FromObjectArray(
                    partitionKeyDefinition.Paths.Select(path =>
                    {
                        string[] parts = PathParser.GetPathParts(path);
                        Debug.Assert(parts.Length >= 1, "Partition key component definition path is invalid.");

                        return document.GetValueByPath<object>(parts, Undefined.Value);
                    }).ToArray(),
                    false);
            }
        }

        /// <summary>
        /// Extracts effective <see cref="PartitionKeyInternal"/> from serialized document.
        /// </summary>
        /// <remarks>
        /// This code doesn't do any validation, as it assumes that IndexingPolicy is valid, as it is coming from the backend.
        /// Expected format is "/prop1/prop2/?". No array expressions are expected.
        /// </remarks>
        /// <param name="documentString">Serialized document to extract partition key value from.</param>
        /// <param name="partitionKeyDefinition">Information about partition key.</param>
        /// <returns>Instance of <see cref="PartitionKeyInternal"/>.</returns>
        public static PartitionKeyInternal ExtractPartitionKeyValue(string documentString, PartitionKeyDefinition partitionKeyDefinition)
        {
            if (partitionKeyDefinition == null || partitionKeyDefinition.Paths.Count == 0)
            {
                return PartitionKeyInternal.Empty;
            }

            return DocumentAnalyzer.ExtractPartitionKeyValue(documentString, partitionKeyDefinition, (docString) => JToken.Parse(docString));
        }

        internal static PartitionKeyInternal ExtractPartitionKeyValue<T>(T data, PartitionKeyDefinition partitionKeyDefinition, Func<T, JToken> convertToJToken)
        {
            return PartitionKeyInternal.FromObjectArray(
                partitionKeyDefinition.Paths.Select(path =>
                {
                    string[] parts = PathParser.GetPathParts(path);
                    Debug.Assert(parts.Length >= 1, "Partition key component definition path is invalid.");

                    JToken token = convertToJToken(data);

                    foreach (string part in parts)
                    {
                        if (token == null)
                        {
                            break;
                        }

                        token = token[part];
                    }

                    return token != null ? token.ToObject<object>() : Undefined.Value;
                }).ToArray(),
                false);
        }
    }
}