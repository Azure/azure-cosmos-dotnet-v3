//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class ResponseRewriter
    {
        public static Task RewriteStreamAsTextAsync(ResponseMessage responseMessage, QueryRequestOptions requestOptions, ITrace trace)
        {
            using (ITrace rewriteTrace = trace.StartChild("Rewrite Stream as Text", TraceComponent.Json, TraceLevel.Info))
            {
                // Rewrite the payload to be in the specified format.
                // If it's already in the correct format, then the following will be a memcpy.
                MemoryStream memoryStream;
                if (responseMessage.Content is MemoryStream responseContentAsMemoryStream)
                {
                    memoryStream = responseContentAsMemoryStream;
                }
                else
                {
                    memoryStream = new MemoryStream();
                    responseMessage.Content.CopyTo(memoryStream);
                }

                ReadOnlyMemory<byte> buffer;
                if (memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
                {
                    buffer = segment.Array.AsMemory().Slice(start: segment.Offset, length: segment.Count);
                }
                else
                {
                    buffer = memoryStream.ToArray();
                }

                IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);
                if (jsonNavigator.SerializationFormat == JsonSerializationFormat.Text)
                {
                    return Task.CompletedTask;
                }

                IJsonWriter jsonWriter;
                if (requestOptions?.CosmosSerializationFormatOptions != null)
                {
                    jsonWriter = requestOptions.CosmosSerializationFormatOptions.CreateCustomWriterCallback();
                }
                else
                {
                    jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                }

                jsonNavigator.WriteNode(jsonNavigator.GetRootNode(), jsonWriter);

                ReadOnlyMemory<byte> result = jsonWriter.GetResult();
                MemoryStream rewrittenMemoryStream;
                if (MemoryMarshal.TryGetArray(result, out ArraySegment<byte> rewrittenSegment))
                {
                    rewrittenMemoryStream = new MemoryStream(rewrittenSegment.Array, index: rewrittenSegment.Offset, count: rewrittenSegment.Count, writable: false, publiclyVisible: true);
                }
                else
                {
                    byte[] toArray = result.ToArray();
                    rewrittenMemoryStream = new MemoryStream(toArray, index: 0, count: toArray.Length, writable: false, publiclyVisible: true);
                }

                responseMessage.Content = rewrittenMemoryStream;
                return Task.CompletedTask;
            }
        }
    }
}
