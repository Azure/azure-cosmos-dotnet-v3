using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class TestCosmosItem
{
    public string id { get; set; }
    public Dictionary<string, string> strings { get; set; }
}

public class TestPatchRepro
{
    public void TestPatchOperationCreation()
    {
        // Test creating patch operations with long numeric-looking strings
        var longNumericString = "12345678901234567890";
        var operation = PatchOperation.Add($"/strings/{longNumericString}", "value");
        
        Console.WriteLine($"Path: {operation.Path}");
        Console.WriteLine($"Operation Type: {operation.OperationType}");
        
        // Test with quoted string
        var operationQuoted = PatchOperation.Add($"/strings/\"{longNumericString}\"", "value");
        Console.WriteLine($"Quoted Path: {operationQuoted.Path}");
        
        // Test with tilde escape
        var operationTilde = PatchOperation.Add($"/strings/~{longNumericString}", "value");
        Console.WriteLine($"Tilde Path: {operationTilde.Path}");
    }
}