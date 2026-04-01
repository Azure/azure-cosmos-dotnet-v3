namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    /// <summary>
    /// Represents the type of contract validation to perform.
    /// </summary>
    public enum ContractType
    {
        /// <summary>Standard public API contract</summary>
        Standard,
        
        /// <summary>Telemetry API contract</summary>
        Telemetry,
        
        /// <summary>Preview API contract (requires official baseline for comparison)</summary>
        Preview
    }
}
