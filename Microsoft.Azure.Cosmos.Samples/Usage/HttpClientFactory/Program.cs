namespace Cosmos.Samples.Shared
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates usage scenarios for HttpClientFactory
    // ----------------------------------------------------------------------------------------------------------

    public class Program
    {
        // <Main>
        public static void Main(string[] _)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                Program.ConnectToEmulatorWithSslDisabled(endpoint, authKey);
                Program.UseNetCoreIHttpClientFactory(endpoint, authKey);
                Program.ShareHttpHandlers(endpoint, authKey);
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
        // </Main>

        /// <summary>
        /// In scenarios where connection to the Emulator requires disabled SSL verification due to multiple environments.
        /// </summary>
        private static void ConnectToEmulatorWithSslDisabled(
            string endpoint,
            string authKey)
        {
            // For applications running in NET Standard 2.0 compatible frameworks
            {
                // <DisableSSLNETStandard20>
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    HttpClientFactory = () =>
                    {
                        HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                        {
                            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
                        };

                        return new HttpClient(httpMessageHandler);
                    },
                    ConnectionMode = ConnectionMode.Gateway
                };


                CosmosClient client = new CosmosClient(endpoint, authKey, cosmosClientOptions);
                // </DisableSSLNETStandard20>
            }

            // For applications running in NET Standard 2.1+ compatible frameworks
            {
                // <DisableSSLNETStandard21>
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    HttpClientFactory = () =>
                    {
                        HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };

                        return new HttpClient(httpMessageHandler);
                    },
                    ConnectionMode = ConnectionMode.Gateway
                };


                CosmosClient client = new CosmosClient(endpoint, authKey, cosmosClientOptions);
                // </DisableSSLNETStandard21>
            }
        }

        /// <summary>
        /// When running NET Core / ASP.NET Core applications, you can leverage IHttpClientFactory to reuse and share HttpClients across your entire NET Core / ASP.NET Core application.
        /// </summary>
        /// <see href="https://docs.microsoft.com/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests"/>
        /// <see href="https://docs.microsoft.com/aspnet/core/fundamentals/http-requests"/>
        private static void UseNetCoreIHttpClientFactory(
            string endpoint,
            string authKey)
        {
            // Console applications can use IHttpClientFactory by using the HostBuilder
            // https://docs.microsoft.com/aspnet/core/fundamentals/http-requests?view=aspnetcore-2.1#use-ihttpclientfactory-in-a-console-app-2
            {
                // <IHttpClientFactoryConsole>
                IHostBuilder builder = new HostBuilder()
                    .ConfigureServices((hostContext, services) => services.AddHttpClient()).UseConsoleLifetime();

                IHost host = builder.Build();

                using (IServiceScope serviceScope = host.Services.CreateScope())
                {
                    IServiceProvider serviceProvider = serviceScope.ServiceProvider;

                    IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

                    CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                    {
                        HttpClientFactory = httpClientFactory.CreateClient
                    };


                    CosmosClient client = new CosmosClient(endpoint, authKey, cosmosClientOptions);
                }

                // </IHttpClientFactoryConsole>
            }

            //ASP.NET Core applications get it from the Dependency Injection container.
            {
                // <IHttpClientFactoryASPNETCore>
#pragma warning disable CS8321
                void ConfigureServices(IServiceCollection services)
                {
                    services.AddHttpClient();
                    services.AddSingleton<CosmosClient>(serviceProvider =>
                    {
                        IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

                        CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                        {
                            HttpClientFactory = httpClientFactory.CreateClient
                        };


                        return new CosmosClient(endpoint, authKey, cosmosClientOptions);
                    });

                    //... other service registration
                }
#pragma warning restore CS8321
                              // </IHttpClientFactoryASPNETCore>
            }
        }

        /// <summary>
        /// When IHttpClientFactory is not available or not needed, the right way is to share HttpHandlers.
        /// </summary>
        /// <see href="https://docs.microsoft.com/aspnet/core/fundamentals/http-requests?view=aspnetcore-3.1#alternatives-to-ihttpclientfactory"/>
        private static void ShareHttpHandlers(
            string endpoint,
            string authKey)
        {
            // <ReusingHandler>
            // Maintain a single instance of the SocketsHttpHandler for the lifetime of the application
            SocketsHttpHandler socketsHttpHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10), // Customize this value based on desired DNS refresh timer
                MaxConnectionsPerServer = 20 // Customize the maximum number of allowed connections
            };

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => new HttpClient(socketsHttpHandler, disposeHandler: false)
            };


            CosmosClient client = new CosmosClient(endpoint, authKey, cosmosClientOptions);

            HttpClient anotherHttpClient = new HttpClient(socketsHttpHandler, disposeHandler: false);
            //... use the other client for another operations
            // </ReusingHandler>
        }
    }
}
