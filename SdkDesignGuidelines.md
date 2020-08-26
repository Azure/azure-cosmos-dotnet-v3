# SDK Design Guidelines

## Public contract changes

### API Review
Public contract changes must go through an API review. Please create a github issue that explains the reason for the change and proposed design. For internal Microsoft engineers please schedule a meeting to review this with the SDK team. For external customers we will provide feedback to the github issue.

### What is considered public contract?
1. Public APIs 
2. Preview APIs
3. Dependencies including nuget package version
4. Behavioral changes. For example trying to convert the default serializer from Newtonsoft to System.Text.Json. 

### Request options
Request options should not be sealed.
1. This allows internal teams to extend and add custom properties to request options which they can later access in lower layers of the SDK.
2. Request options only has public properties, so there is no concern with users extending the type.

### Central SDK
The V3 SDK follow the [central SDK .NET guidelines](https://azure.github.io/azure-sdk/dotnet_introduction.html) where possible. V3 SDK was released before these guidelines existed so it does not have the same types. Consistency with the current V3 public API is more important than follow the central SDK .NET guidelines as it provides a better user experience.