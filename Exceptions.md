# Exception handling

## Error handling types
The SDK was designed with the following two scenarios.

### Typed APIs<a id="typed-api"></a>
This is for most users that want a simple interface that follows the normal Dot NET guidelines. Typed APIs is any method that returns the deserialized body, and can be identified as any method that does not return a ResponseMessage.

1. Will throw the following exceptions types:
   1. CosmosException is used for all service related issues like 404 Not found
   2. ArgumentException
   3. OperationCancelledException
2. CosmosException.ToString() includes the message, stack trace, and diagnostics. All of these properties are needed to troubleshoot issues

### ResponseMessage from Stream APIs <a id="stream-api"></a>
These APIs are designed for more advance scenario where perfomance is critical or it's being used in a middle tier service where a stream can just be passed through. Stream APIs return the content of the response as a stream, and can be identified as any method that returns a ResponseMessage. 

1. Returns a ResponseMessage instead of throwing a CosmosException for service related errors like a 404 Not Found. Throwing an exception causes unncessary overhead
2. User still needs to handle the following exceptions.
    1. ArgumentException: This is thrown because it represent a bug in the user code, and should not be hit in production.
    2. OperationCancelledException: Not throwing an exception would violate the CancellationToken contract. 
3. ResponseMessage.IsSuccessStatusCode can be used to validate if the operation was successful
4. ResponseMessage.EnsureSuccessStatusCode() will throw a CosmosException if the operation failed
5. ResponseMessage.ErrorMessage contains the error message. It does not include the diagnostics.
6. ResponseMessage.Diagnostics.ToString() contains information required to troubleshoot most issues.

### Retry Logic <a id="retry-logics"></a>
Cosmos DB SDK on any IO failure will attempt to retry the failed operation if retry in the SDK is feasible. Having a retry in place for any failure is a good practice but specifically handling/retrying write failures is a must.

1. Read and query IO failures will get retried by the SDK without surfacing them to the end user.
2. Writes (Create, Upsert, Replace, Delete) are "not" idempotent and hence SDK cannot always blindly retry the failed write operations. It is a must that user's application logic to handle the failure and retry.

## Common error status codes and troubleshooting guide <a id="error-codes"></a>

To see a list of common error code and issues please see [.NET SDK troubleshooting guide](https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk)
