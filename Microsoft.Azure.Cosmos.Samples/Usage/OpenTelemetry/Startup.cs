namespace AspNetCoreWebApp
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Models;

    public class Startup
    {
        private CosmosClient cosmosClient;
        private Database database;

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Environment.SetEnvironmentVariable("COSMOS.CLIENT_TELEMETRY_ENABLED", "true");
            Environment.SetEnvironmentVariable("COSMOS.CLIENT_TELEMETRY_SCHEDULING_IN_SECONDS", "1");
            Environment.SetEnvironmentVariable("COSMOS.CLIENT_TELEMETRY_ENDPOINT", "https://juno-test2.documents-dev.windows-int.net/api/clienttelemetry/trace");

            services.AddAuthorization();

            services.AddControllersWithViews();
            // Add and initialize the Application Insights SDK.
            services.AddApplicationInsightsTelemetry();

            CosmosDbSettings cosmosDbSettings = this.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();

            Container container = this.CreateClientAndContainer(
                connectionString: "",
                mode: Enum.Parse<ConnectionMode>(cosmosDbSettings.ConnectionMode)/*,
                isEnableOpenTelemetry: cosmosDbSettings.EnableOpenTelemetry*/).Result;

            services.AddSingleton<Container>(container);
        }

        private async Task<Container> CreateClientAndContainer(
            string connectionString,
            ConnectionMode mode,
            Microsoft.Azure.Cosmos.ConsistencyLevel? consistency = null,
            bool isLargeContainer = true/*,
            bool isEnableOpenTelemetry = false*/)
        {
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(connectionString)
                .WithBulkExecution(true);// move it to corresponding test
            if (consistency.HasValue)
            {
                cosmosClientBuilder = cosmosClientBuilder.WithConsistencyLevel(consistency.Value);
            }
/*
            if (isEnableOpenTelemetry)
            {
                cosmosClientBuilder = cosmosClientBuilder.EnableOpenTelemetry();
            }
 */
            this.cosmosClient = mode == ConnectionMode.Gateway
                ? cosmosClientBuilder.WithConnectionModeGateway().Build()
                : cosmosClientBuilder.Build();

            Random randomNumber = new Random();
            string randomNumberString = randomNumber.Next(0, 9999).ToString("0000");

            this.database = await this.cosmosClient.CreateDatabaseAsync("OTelSampleDb" + randomNumberString);

            return await this.database.CreateContainerAsync(
                id: "OTelSampleContainer" + randomNumberString,
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);
        }
        
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                if (endpoints == null)
                {
                    throw new ArgumentNullException(nameof(endpoints));
                }

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
