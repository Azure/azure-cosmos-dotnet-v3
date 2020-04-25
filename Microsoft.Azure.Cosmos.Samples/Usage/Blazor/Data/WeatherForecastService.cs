using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorCosmosApp.Data
{
    public class WeatherForecastService
    {
        private CosmosClient cosmosClient;
        private Container container;

        public WeatherForecastService(
            string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Please update the connection string in the appsettings.json file");
            }

            this.cosmosClient = new CosmosClient(
                    connectionString,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        LimitToEndpoint = true
                    });

            this.container = this.cosmosClient.GetContainer("BlazorDB", "BlazorContainer");
        }

        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public async Task<WeatherForecast> CreateRandomForecastAsync()
        {
            var rng = new Random();
            WeatherForecast weatherForecast = new WeatherForecast
            {
                id = Guid.NewGuid().ToString(),
                Date = DateTime.UtcNow,
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            };

            return await this.container.CreateItemAsync(weatherForecast, new PartitionKey(weatherForecast.id));
        }

        public async Task<List<WeatherForecast>> GetAllForecastsAsync()
        {
            FeedIterator<WeatherForecast> feedIterator = container.GetItemQueryIterator<WeatherForecast>(
                "select * from T");

            List<WeatherForecast> weatherForecasts = new List<WeatherForecast>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<WeatherForecast> feedResponse = await feedIterator.ReadNextAsync();
                weatherForecasts.AddRange(feedResponse);
            }

            return weatherForecasts;
        }
    }
}
