//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class EncryptionProcessor
    {
        // todo: get this from usual HttpHeaders constants once new .Direct package is published
        internal const string ClientEncryptionKeyHeader = "x-ms-cosmos-client-encryption-key";
        internal const string ClientDecryptedHeader = "x-ms-cosmos-client-decrypted";

        private static readonly CosmosSerializer baseSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());

        public async Task<(Stream, string)> EncryptAsync(
            Stream input,
            EncryptionOptions encryptionOptions,
            DatabaseCore database,
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(encryptionOptions != null);
            Debug.Assert(database != null);
            Debug.Assert(diagnosticsContext != null);

            string dataEncryptionKeyRid = null;

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
            }

            if (encryptionOptions.PathsToEncrypt.Count == 0)
            {
                return (input, dataEncryptionKeyRid);
            }

            if (encryptionOptions.DataEncryptionKey == null)
            {
                throw new ArgumentException("Invalid encryption options", nameof(encryptionOptions.DataEncryptionKey));
            }

            if (encryptionKeyWrapProvider == null)
            {
                throw new ArgumentException(ClientResources.EncryptionKeyWrapProviderNotConfigured);
            }

            foreach (string path in encryptionOptions.PathsToEncrypt)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/')
                {
                    throw new ArgumentException($"Invalid path provided: {path ?? string.Empty}", nameof(encryptionOptions.PathsToEncrypt));
                }
            }

            List<string> pathsToEncrypt = encryptionOptions.PathsToEncrypt.OrderBy(p => p).ToList();

            for (int index = 1; index < encryptionOptions.PathsToEncrypt.Count; index++)
            {
                // If path (eg. /foo) is a prefix of another path (eg. /foo/bar), /foo/bar is redundant.
                if (pathsToEncrypt[index].StartsWith(pathsToEncrypt[index - 1]) && pathsToEncrypt[index][pathsToEncrypt[index - 1].Length] == '/')
                {
                    throw new ArgumentException($"Redundant path provided: {pathsToEncrypt[index]}", nameof(encryptionOptions.PathsToEncrypt));
                }
            }

            DataEncryptionKey dek = database.GetDataEncryptionKey(encryptionOptions.DataEncryptionKey.Id);

            DataEncryptionKeyCore dekCore = (DataEncryptionKeyInlineCore)dek;
            (DataEncryptionKeyProperties dekProperties, InMemoryRawDek inMemoryRawDek) = await dekCore.FetchUnwrappedAsync(
                diagnosticsContext,
                cancellationToken);

            JObject itemJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(input);
            JObject toEncryptJObj = new JObject();

            if (EncryptionProcessor.SplitItemJObject(itemJObj, toEncryptJObj, pathsToEncrypt))
            {
                Stream stream = EncryptionProcessor.baseSerializer.ToStream<JObject>(toEncryptJObj);
                Debug.Assert(stream != null);

                MemoryStream memoryStream = stream as MemoryStream;
                Debug.Assert(memoryStream != null);

                if (memoryStream == null)
                {
                    memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                }

                byte[] plainText = memoryStream.ToArray();
                EncryptionProperties encryptionProperties = new EncryptionProperties(
                    dataEncryptionKeyRid: dekProperties.ResourceId,
                    encryptionFormatVersion: 1,
                    encryptedData: inMemoryRawDek.AlgorithmUsingRawDek.EncryptData(plainText));

                itemJObj.Add(Constants.Properties.EncryptedInfo, JObject.FromObject(encryptionProperties));
                dataEncryptionKeyRid = dekProperties.ResourceId;
            }

            return (EncryptionProcessor.baseSerializer.ToStream(itemJObj), dataEncryptionKeyRid);
        }

        private static bool SplitItemJObject(JObject itemJObj, JObject toEncryptJObj, List<string> pathsToEncrypt)
        {
            bool isAnyPathFound = false;
            foreach (string pathToEncrypt in pathsToEncrypt)
            {
                string[] segmentsOfPath = pathToEncrypt.Split('/'); // The first segment is always empty since the path starts with / 
                bool isPathFound = false;
                JObject readObj = itemJObj;
                for (int index = 1; index < segmentsOfPath.Length; index++)
                {
                    JProperty readProp = readObj.Property(segmentsOfPath[index]);
                    if (readProp == null)
                    {
                        // path not found
                        break;
                    }

                    if (index == segmentsOfPath.Length - 1)
                    {
                        isPathFound = true;
                        isAnyPathFound = true;
                    }
                    else
                    {
                        if (readProp.Value.Type != JTokenType.Object)
                        {
                            // the Value (RHS) of the property for all except the last path segment needs to be an Object so we can go deeper
                            break;
                        }
                        else
                        {
                            readObj = (JObject)readProp.Value;
                        }
                    }
                }

                if (!isPathFound)
                {
                    // Didn't find current path in the item to encrypt; try the next one.
                    continue;
                }

                JObject writeObj = toEncryptJObj;
                for (int index = 1; index < segmentsOfPath.Length - 1; index++)
                {
                    JProperty writeProp = writeObj.Property(segmentsOfPath[index]);
                    if (writeProp == null)
                    {
                        writeObj.Add(new JProperty(segmentsOfPath[index], new JObject()));
                        writeProp = writeObj.Property(segmentsOfPath[index]);
                    }

                    writeObj = (JObject)writeProp.Value;
                }

                string lastSegment = segmentsOfPath[segmentsOfPath.Length - 1];
                writeObj.Add(lastSegment, readObj.Property(lastSegment).Value);
                readObj.Remove(lastSegment);
            }

            return isAnyPathFound;
        }

        public async Task<(Stream, bool)> DecryptAsync(
            Stream input,
            DatabaseCore database,
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(database != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(diagnosticsContext != null);

            bool wasDecryptionPerformed = false;

            if (encryptionKeyWrapProvider == null)
            {
                return (input, wasDecryptionPerformed);
            }

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
                }
            }

            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.Properties.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return (input, wasDecryptionPerformed);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
            if (encryptionProperties.EncryptionFormatVersion != 1)
            {
                throw new CosmosException(
                    HttpStatusCode.InternalServerError,
                    $"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            DataEncryptionKeyCore tempDek = (DataEncryptionKeyInlineCore)database.GetDataEncryptionKey(id: "unknown");
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await tempDek.FetchUnwrappedByRidAsync(
                encryptionProperties.DataEncryptionKeyRid,
                diagnosticsContext,
                cancellationToken);

            byte[] plainText = inMemoryRawDek.AlgorithmUsingRawDek.DecryptData(encryptionProperties.EncryptedData);

            JObject plainTextJObj = null;
            using (MemoryStream plainTextStream = new MemoryStream(plainText))
            {
                plainTextJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(plainTextStream);
            }

            itemJObj.Merge(plainTextJObj);
            itemJObj.Remove(Constants.Properties.EncryptedInfo);
            wasDecryptionPerformed = true;
            input.Dispose();

            return (EncryptionProcessor.baseSerializer.ToStream(itemJObj), wasDecryptionPerformed);
        }
    }
}
