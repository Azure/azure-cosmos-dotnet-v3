using System;
using Microsoft.Azure.Cosmos;

class TestPatchPathHelper
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing PatchPathHelper...");
        
        // Test 1: Short numeric string (should not be changed)
        string shortPath = "/strings/123456789";
        string result1 = PatchPathHelper.ProcessPath(shortPath);
        Console.WriteLine($"Short numeric: '{shortPath}' -> '{result1}'");
        
        // Test 2: Long numeric string (should be escaped)
        string longPath = "/strings/12345678901234567890";
        string result2 = PatchPathHelper.ProcessPath(longPath);
        Console.WriteLine($"Long numeric: '{longPath}' -> '{result2}'");
        
        // Test 3: Mixed alphanumeric (should not be changed)
        string mixedPath = "/strings/abc123456789012345678901234567890def";
        string result3 = PatchPathHelper.ProcessPath(mixedPath);
        Console.WriteLine($"Mixed alphanumeric: '{mixedPath}' -> '{result3}'");
        
        // Test 4: Multiple segments with long numeric
        string multiPath = "/strings/12345678901234567890/nested/123456789";
        string result4 = PatchPathHelper.ProcessPath(multiPath);
        Console.WriteLine($"Multiple segments: '{multiPath}' -> '{result4}'");
        
        // Test 5: Create actual patch operations
        Console.WriteLine("\nTesting patch operations:");
        
        var operation1 = PatchOperation.Add("/strings/123456789", "short");
        Console.WriteLine($"Short numeric operation path: '{operation1.Path}'");
        
        var operation2 = PatchOperation.Add("/strings/12345678901234567890", "long");
        Console.WriteLine($"Long numeric operation path: '{operation2.Path}'");
        
        Console.WriteLine("Test completed successfully!");
    }
}