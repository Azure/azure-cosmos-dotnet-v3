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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class EncryptionProcessor
    {
        private static readonly CosmosSerializer baseSerializer = new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer());

        public async Task<Stream> EncryptAsync(
            Stream input,
            EncryptionOptions encryptionOptions,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(encryptionOptions != null);
            Debug.Assert(diagnosticsContext != null);

            if (encryptor == null)
            {
                throw new ArgumentException(ClientResources.EncryptorNotConfigured);
            }

            if (string.IsNullOrEmpty(encryptionOptions.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.DataEncryptionKeyId));
            }

            if (string.IsNullOrEmpty(encryptionOptions.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.EncryptionAlgorithm));
            }

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
            }

            if (encryptionOptions.PathsToEncrypt.Count == 0)
            {
                return input;
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

            JObject itemJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(input);

            JObject toEncryptJObj = new JObject();

            if (EncryptionProcessor.SplitItemJObjectForEncryption(itemJObj, toEncryptJObj, pathsToEncrypt))
            {
                MemoryStream memoryStream = EncryptionProcessor.baseSerializer.ToStream<JObject>(toEncryptJObj) as MemoryStream;
                Debug.Assert(memoryStream != null);
                Debug.Assert(memoryStream.TryGetBuffer(out _));

                byte[] plainText = memoryStream.GetBuffer();
                byte[] cipherText = await encryptor.EncryptAsync(
                    plainText,
                    encryptionOptions.DataEncryptionKeyId,
                    encryptionOptions.EncryptionAlgorithm,
                    cancellationToken);

                if (cipherText == null)
                {
                    throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(EncryptAsync)}.");
                }

                EncryptionProperties encryptionProperties = new EncryptionProperties(
                    encryptionFormatVersion: 2,
                    dataEncryptionKeyId: encryptionOptions.DataEncryptionKeyId,
                    encryptionAlgorithm: encryptionOptions.EncryptionAlgorithm,
                    encryptedData: cipherText);

                itemJObj.Add(Constants.Properties.EncryptedInfo, JObject.FromObject(encryptionProperties));
            }

            return EncryptionProcessor.baseSerializer.ToStream(itemJObj);
        }

        public async Task<Stream> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
                }
            }

            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.Properties.EncryptedInfo);
            if (encryptionPropertiesJProp == null
                || encryptionPropertiesJProp.Value == null
                || encryptionPropertiesJProp.Value.Type != JTokenType.Object)
            {
                input.Position = 0;
                return input;
            }

            await this.DecryptAsync(
                itemJObj,
                encryptor,
                diagnosticsContext,
                cancellationToken);

            return EncryptionProcessor.baseSerializer.ToStream(itemJObj);
        }

        public async Task<CosmosObject> DecryptAsync(
            CosmosObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            if (!document.TryGetValue(Constants.Properties.EncryptedInfo, out CosmosElement encryptedInfo)
                || encryptedInfo.Type != CosmosElementType.Object)
            {
                return document;
            }

            Stream stream = EncryptionProcessor.baseSerializer.ToStream<CosmosObject>(document);
            JObject itemJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(stream);

            await this.DecryptAsync(
                itemJObj,
                encryptor,
                diagnosticsContext,
                cancellationToken);

            stream = EncryptionProcessor.baseSerializer.ToStream<JObject>(itemJObj);
            return EncryptionProcessor.baseSerializer.FromStream<CosmosObject>(stream);
        }

        private static bool SplitItemJObjectForEncryption(JObject itemJObj, JObject toEncryptJObj, List<string> pathsToEncrypt)
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

        private async Task DecryptAsync(
            JObject itemJObj,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.Properties.EncryptedInfo);
            Debug.Assert(encryptionPropertiesJProp != null
                && encryptionPropertiesJProp.Value != null
                && encryptionPropertiesJProp.Value.Type == JTokenType.Object);

            EncryptionProperties encryptionProperties = encryptionPropertiesJProp.Value.ToObject<EncryptionProperties>();

            if (encryptionProperties.EncryptionFormatVersion != 2)
            {
                throw new InvalidOperationException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await encryptor.DecryptAsync(
                encryptionProperties.EncryptedData,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            }

            JObject plainTextJObj;
            using (MemoryStream plainTextStream = new MemoryStream(plainText))
            {
                plainTextJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(plainTextStream);
            }

            itemJObj.Merge(plainTextJObj);
            itemJObj.Remove(Constants.Properties.EncryptedInfo);
        }
    }
}