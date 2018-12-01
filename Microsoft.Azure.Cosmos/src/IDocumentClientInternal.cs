namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;

    internal interface IDocumentClientInternal : IDocumentClient
    {
        Task<CosmosAccountSettings> GetDatabaseAccountInternalAsync(Uri serviceEndpoint);
    }
}
