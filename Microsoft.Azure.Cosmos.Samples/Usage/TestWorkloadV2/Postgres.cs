namespace TestWorkloadV2
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Npgsql;

    internal class Postgres : IDriver
    {
        internal class Configuration : CommonConfiguration
        {
            public int ConnectionCount { get; set; }
        }

        private Configuration configuration;
        private NpgsqlDataSource dataSource;
        private NpgsqlConnection[] connections;
        private Random random;

        public async Task CleanupAsync()
        {
            foreach (NpgsqlConnection connection in this.connections)
            {
                await connection.CloseAsync();
            }
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            throw new System.NotImplementedException();
        }

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);

            this.dataSource = new NpgsqlDataSourceBuilder(this.configuration.ConnectionString).Build();

            this.connections = new NpgsqlConnection[this.configuration.ConnectionCount];

            for (int i = 0; i < this.configuration.ConnectionCount; i++)
            {
                this.connections[i] = await this.dataSource.OpenConnectionAsync();
            }

            this.random = new Random();

            return (this.configuration, new DataSource(this.configuration));
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            context = null;

            NpgsqlConnection conn = this.connections[this.random.Next(this.configuration.ConnectionCount)];
            NpgsqlCommand cmd = new NpgsqlCommand("INSERT INTO data (some_field) VALUES (@p)", conn);
            cmd.Parameters.AddWithValue("p", "Hello world");
            return cmd.ExecuteNonQueryAsync();
        }
    }
}
