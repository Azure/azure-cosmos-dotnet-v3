using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
        // Create a patch operation with a long numeric-looking string
        var longNumericString = "12345678901234567890";
        var patchOperation = PatchOperation.Add($"/strings/{longNumericString}", "value");
        
        Console.WriteLine($"Original Path: {patchOperation.Path}");
        
        // Create a PatchSpec to simulate what gets serialized
        var patchSpec = new PatchSpec(
            new List<PatchOperation> { patchOperation },
            new PatchItemRequestOptions()
        );
        
        // Use the same serializer as the SDK
        var serializer = new CosmosJsonDotNetSerializer();
        var converter = new PatchOperationsJsonConverter(serializer);
        
        // Serialize to see what JSON is generated
        var jsonSerializer = new JsonSerializer();
        jsonSerializer.Converters.Add(converter);
        
        var stringWriter = new StringWriter();
        var jsonWriter = new JsonTextWriter(stringWriter);
        
        converter.WriteJson(jsonWriter, patchSpec, jsonSerializer);
        
        var json = stringWriter.ToString();
        Console.WriteLine($"Generated JSON: {json}");
        
        // Test with other path formats
        var quotedPath = PatchOperation.Add($"/strings/\"{longNumericString}\"", "value");
        Console.WriteLine($"Quoted Path: {quotedPath.Path}");
        
        var escapedPath = PatchOperation.Add($"/strings/~{longNumericString}", "value"); 
        Console.WriteLine($"Escaped Path: {escapedPath.Path}");
    }
}