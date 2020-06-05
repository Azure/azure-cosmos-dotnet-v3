# Exception handling

## Error handling types
The SDK was designed with the following two scenarios.

### Typed APIs
This is for most users that want a simple interface that follows the normal Dot NET guidelines. Typed APIs is any method that returns the deserialized body, and can be identified as any method that does not return a ResponseMessage.

1. Will throw the following exceptions types:
  1. CosmosException is used for all service related issues like 404 Not found
  2. ArgumentException
  3. OperationCancelledException
2. CosmosException.ToString() includes the message, stack trace, and diagnostics. All of these properties are needed to troubleshoot issues

### ResponseMessage from Stream APIs
These APIs are designed for more advance scenario where perfomance is critical or it's being used in a middle tier service where a stream can just be passed through. Stream APIs return the content of the response as a stream, and can be identified as any method that returns a ResponseMessage. 

1. Returns a ResponseMessage instead of throwing a CosmosException for service related errors like a 404 Not Found. Throwing an exception causes unncessary overhead
2. User still needs to handle the following exceptions.
    1. ArgumentException: This is thrown because it represent a bug in the user code, and should not be hit in production.
    2. OperationCancelledException: Not throwing an exception would violate the CancellationToken contract. 
3. ResponseMessage.IsSuccessStatusCode can be used to validate if the operation was successful
4. ResponseMessage.EnsureSuccessStatusCode() will throw a CosmosException if the operation failed
5. ResponseMessage.ErrorMessage contains the error message. It does not include the diagnostics.
6. ResponseMessage.Diagnostics.ToString() contains information required to troubleshoot most issues.


## Common error status codes and retry logic

| Status Code | Description | Retry logic |
|----------|-------------|------|
| 401 | [Not authorized](CosmosMacSignature.md) | SDK does not retry. User's application should have retry logic for some corner scenarios, but most likely require user to manually fix | 
| 404 | [Resource is not found](CosmosNotFound.md) | SDK does not retry. User's application should handle this scenario. |
| 408 | [Request timed out](CosmosRequestTimeout.md)| SDK does not retry. User's application should have retry logic. There are many transient scenarios that can cause this. The SDK does not rety because it can lead to conflicts since there is no way to tell if the original request completed. Different user scenarios require different logic for conflicts which would be broken if the SDK did retry.  |
| 409 | Conflict (Only for Create/Replace/Upsert) | User's application should handle the conflict |
| 410 | Gone exceptions | SDK handles the retries. If the retry logic is exceeded it will get converted to a 503 error. This can be caused by many scenarios like partition was moved to a larger machine because of a scaling operation. This is an expected exception and will not impact the Cosmos DB SLA. |
| 429 | [To many requests](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/TroubleshootingGuides/CosmosRequestRateTooLarge.md) | The SDK has built in logic, and it is user configurable for most SDKs |
| 500 | Azure Cosmos DB failure | User's application should have retry logic. |
| 503 | Was not able to reach Azure Cosmos DB | User's application should have retry logic. |