namespace TestWorkloadV2
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    internal interface IDriver
    {
        Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot);

        Task MakeRequestAsync(CancellationToken cancellationToken, out object context);

        ResponseAttributes HandleResponse(Task request, object context);

        Task CleanupAsync();
    }
}
