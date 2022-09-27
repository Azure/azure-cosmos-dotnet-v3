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
    using OpenTelemetry.Util;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization();

            services.AddControllersWithViews();
            // Add and initialize the Application Insights SDK.
            services.AddApplicationInsightsTelemetry();

            string multiRegionAccountConnectionString = "";

            CosmosDbSettings cosmosDbSettings = this.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();

            Container container = CosmosClientInit.CreateClientAndContainer(
                connectionString: cosmosDbSettings.ConnectionString,
                mode: Enum.Parse<ConnectionMode>(cosmosDbSettings.ConnectionMode),
                isEnableOpenTelemetry: cosmosDbSettings.EnableOpenTelemetry).Result;
            CosmosClientInit.singleRegionAccount = container;

            Container largeContainer = CosmosClientInit.CreateClientAndContainer(
                connectionString: cosmosDbSettings.ConnectionString,
                mode: Enum.Parse<ConnectionMode>(cosmosDbSettings.ConnectionMode),
                dbAndContainerNameSuffix: "_large",
                isLargeContainer: true,
                isEnableOpenTelemetry: cosmosDbSettings.EnableOpenTelemetry).Result;
            CosmosClientInit.largeRegionAccount = largeContainer;

            if(!string.IsNullOrEmpty(multiRegionAccountConnectionString))
            {
                Container multiContainer = CosmosClientInit.CreateClientAndContainer(
                 connectionString: "",
                 mode: Enum.Parse<ConnectionMode>(cosmosDbSettings.ConnectionMode),
                 isEnableOpenTelemetry: cosmosDbSettings.EnableOpenTelemetry).Result;
                CosmosClientInit.multiRegionAccount = multiContainer;
            }

            services.AddSingleton<CosmosDbSettings>(cosmosDbSettings);
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
