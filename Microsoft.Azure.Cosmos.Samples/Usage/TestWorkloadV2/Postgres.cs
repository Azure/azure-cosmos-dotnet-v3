namespace TestWorkloadV2
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Npgsql;

    internal class Postgres : IDriver
    {
        internal class Configuration : CommonConfiguration
        {
        }

        private Configuration configuration;
        private NpgsqlDataSource pgDataSource;
        private Random random;
        private DataSource dataSource;
        private int isExceptionPrinted;
        private string insertStatement;

        public Task CleanupAsync()
        {
            return Task.CompletedTask;
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            NpgsqlConnection conn = context as NpgsqlConnection;
            conn.Dispose();

            ResponseAttributes responseAttributes = default;
            if (request.IsCompletedSuccessfully)
            {
                responseAttributes.StatusCode = HttpStatusCode.OK;
            }
            else
            {
                if (Interlocked.CompareExchange(ref this.isExceptionPrinted, 1, 0) == 0)
                {
                    Console.WriteLine(request.Exception.ToString());
                }

                responseAttributes.StatusCode = HttpStatusCode.InternalServerError;
            }

            return responseAttributes;
        }

        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);
            string connectionString = configurationRoot.GetValue<string>(this.configuration.ConnectionStringRef);


            this.configuration.SetConnectionPoolLimits();
            connectionString += $"Minimum Pool Size={this.configuration.MinConnectionPoolSize};Maximum Pool Size={this.configuration.MaxConnectionPoolSize};";

            this.pgDataSource = new NpgsqlDataSourceBuilder(connectionString).Build();

            // todo: parse the string better to expose only the host
            this.configuration.ConnectionStringForLogging = this.pgDataSource.ConnectionString[..20];

            if (this.configuration.ShouldRecreateContainerOnStart)
            {
                using (NpgsqlConnection conn = await this.pgDataSource.OpenConnectionAsync())
                {
                    NpgsqlCommand cmd = new NpgsqlCommand($"DROP TABLE IF EXISTS {this.configuration.ContainerName}", conn);
                    await cmd.ExecuteNonQueryAsync();
                    cmd = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS {this.configuration.ContainerName} (id text PRIMARY KEY, pk text, other text)", conn);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            this.dataSource = await DataSource.CreateAsync(this.configuration,
                paddingGenerator: (DataSource d) =>
                {
                    (MyDocument doc, _) = d.GetNextItemToInsert();
                    int currentLen = doc.Id.Length + doc.PK.Length + doc.Other.Length;
                    string padding = this.configuration.ItemSize > currentLen ? new string('x', this.configuration.ItemSize - currentLen) : string.Empty;
                    return Task.FromResult(padding);
                },
                initialItemIdFinder: async () =>
                {
                    long lastId = -1;
                    if (!this.configuration.ShouldRecreateContainerOnStart)
                    {
                        using (NpgsqlConnection conn = await this.pgDataSource.OpenConnectionAsync())
                        {
                            NpgsqlCommand cmd = new NpgsqlCommand($"SELECT max(id) FROM {this.configuration.ContainerName}", conn);
                            NpgsqlDataReader dataReader = await cmd.ExecuteReaderAsync();
                            if (await dataReader.ReadAsync())
                            {
                                string lastDocId = dataReader.GetString(0);
                                long.TryParse(lastDocId, out lastId);
                            }
                        }
                    }

                    return lastId + 1;
                });

            this.random = new Random(CommonConfiguration.RandomSeed);
            this.insertStatement = $"INSERT INTO {this.configuration.ContainerName} (id, pk, other) VALUES (@id, @pk, @other)";

            return (this.configuration, this.dataSource);
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            // https://www.npgsql.org/doc/prepare.html#persistency - new connection create and repeated prepare are exepected to be lightweight as they reuse use existing internal artifacts on the client
            NpgsqlConnection conn = this.pgDataSource.OpenConnection();
            context = conn;

            if (this.configuration.RequestType == RequestType.Create)
            {

                (MyDocument doc, _) = this.dataSource.GetNextItemToInsert();

                NpgsqlCommand cmd = new NpgsqlCommand(this.insertStatement, conn);
                cmd.Parameters.AddWithValue("id", doc.Id);
                cmd.Parameters.AddWithValue("pk", doc.PK);
                cmd.Parameters.AddWithValue("other", doc.Other);
                cmd.Prepare();

                return cmd.ExecuteNonQueryAsync();
            }

            throw new NotImplementedException(this.configuration.RequestType.ToString());
        }
    }
}
