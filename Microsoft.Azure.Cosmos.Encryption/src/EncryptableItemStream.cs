// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.IO;

    /// <summary>
    /// Stream APIs cannot be used to follow lazy decryption method and to retrieve the decryption information.
    /// Instead, typed APIs can be used with EncryptableItemStream as input type, which takes item input in the form of stream.
    /// </summary>
    /// <example>
    /// This example takes in a item in stream format, encrypts it and writes to Cosmos container.
    /// <code language="c#">
    /// <![CDATA[
    ///     ItemResponse<EncryptableItemStream> createResponse = await encryptionContainer.CreateItemAsync<EncryptableItemStream>(
    ///         new EncryptableItemStream(streamPayload),
    ///         new PartitionKey("streamPartitionKey"),
    ///         EncryptionItemRequestOptions);
    ///
    ///     if (!createResponse.IsSuccessStatusCode)
    ///     {
    ///         //Handle and log exception
    ///         return;
    ///     }
    ///
    ///     (Stream responseStream, DecryptionInfo _) = await item.GetItemAsStreamAsync();
    ///     using (responseStream)
    ///     {
    ///         //Read or do other operations with the stream
    ///         using (StreamReader streamReader = new StreamReader(responseStream))
    ///         {
    ///             string responseContentAsString = await streamReader.ReadToEndAsync();
    ///         }
    ///     }
    /// ]]>
    /// </code>
    /// </example>
    public class EncryptableItemStream : DecryptableItem
    {
        private readonly Stream streamPayload;

        internal override Stream GetInputStreamPayload(
            CosmosSerializer cosmosSerializer)
        {
            return this.streamPayload;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptableItemStream"/> class.
        /// </summary>
        /// <param name="input">Input item stream.</param>
        public EncryptableItemStream(Stream input)
            : base(null, null, null)
        {
            this.streamPayload = input;
        }
    }
}
