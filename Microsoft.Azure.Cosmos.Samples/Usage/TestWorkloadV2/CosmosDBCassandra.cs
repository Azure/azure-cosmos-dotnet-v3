namespace TestWorkloadV2
{
    using System.Linq;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cassandra;
    using Microsoft.Extensions.Configuration;
    using System.Net;

    internal class CosmosDBCassandra : IDriver
    {
        internal class Configuration : CommonConfiguration
        {
            public int ThroughputToProvision { get; set; }
            public bool IsAutoScale { get; set; }
        }

        private Configuration configuration;
        private ISession session;
        private DataSource dataSource;
        private PreparedStatement preparedStatement;


        public async Task<(CommonConfiguration, DataSource)> InitializeAsync(IConfigurationRoot configurationRoot)
        {
            this.configuration = new Configuration();
            configurationRoot.Bind(this.configuration);


            this.session = CreateSession(this.configuration.ConnectionString, this.configuration.DatabaseName);
            if (this.configuration.ShouldRecreateContainerOnStart)
            {
                this.RecreateContainer(
                    this.configuration.ContainerName,
                    this.configuration.ThroughputToProvision,
                    this.configuration.IsAutoScale);
                await Task.Delay(5000);
            }

            try
            {
                RowSet tables = this.session.Execute("SELECT * FROM system_schema.tables where table_name = '" + this.configuration.ContainerName + "' ALLOW FILTERING");
                if (tables.FirstOrDefault() == null)
                {
                    throw new Exception("Did not find table " + this.configuration.ContainerName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading table: {0}", ex.Message);
                throw;
            }

            this.dataSource = new DataSource(this.configuration);
            this.dataSource.InitializePadding(this.configuration.ItemSize > 400 ? new string('x', this.configuration.ItemSize - 400) : string.Empty);

            this.preparedStatement = this.session.Prepare("INSERT INTO " + this.configuration.ContainerName + "(pk, ck, mvpk, other) VALUES (?, ?, ?, ?)");

            return (this.configuration, this.dataSource);
        }

        private static ISession CreateSession(
            string connectionString,
            string keyspaceName)
        {
            SSLOptions options = new SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
            CassandraConnectionStringBuilder connectionStringBuilder = new CassandraConnectionStringBuilder(connectionString);
            options.SetHostNameResolver((ipAddress) => connectionStringBuilder.ContactPoints[0]);

            Cluster cluster = Cluster.Builder()
                .WithConnectionString(connectionString)
                //.WithCredentials(endpoint.Split('.', 2)[0], authKey)
                //.WithPort(10350)
                //.AddContactPoint(endpoint)
                .WithSSL(options)
                .Build();

            ISession session = cluster.Connect();
            session.CreateKeyspaceIfNotExists(keyspaceName);
            session.ChangeKeyspace(keyspaceName);
            return session;
        }

        private static bool ValidateServerCertificate(
                 object sender,
                 X509Certificate certificate,
                 X509Chain chain,
                 SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        private void RecreateContainer(
            string containerName,
            int throughputToProvision,
            bool isAutoScale)
        {
            Console.WriteLine("Deleting old table if it exists.");
            this.CleanupContainer();

            Console.WriteLine($"Creating a {throughputToProvision} RU/s {(isAutoScale ? "auto-scale" : "manual throughput")} table...");

            // todo: shouldIndexAllProperties
            string throughputToken = "cosmosdb_provisioned_throughput";
            if (isAutoScale)
            {
                throughputToken = "cosmosdb_autoscale_max_throughput";
            }

            this.session.Execute("CREATE TABLE " + containerName + "(pk text, ck text, mvpk text, other text, primary key(pk, ck)) WITH " + throughputToken + " = " + throughputToProvision);
        }


        public Task CleanupAsync()
        {
            if (this.configuration.ShouldDeleteContainerOnFinish)
            {
                this.CleanupContainer();
            }

            return Task.CompletedTask;
        }

        private void CleanupContainer()
        {
            if (this.session != null)
            {
                try
                {
                    this.session.Execute("DROP TABLE " + this.configuration.ContainerName);
                }
                catch (Exception)
                {
                }
            }
        }

        public Task MakeRequestAsync(CancellationToken cancellationToken, out object context)
        {
            context = null;
            (MyDocument myDocument, _) = this.dataSource.GetNextItem();
            IStatement boundStatement = this.preparedStatement.Bind(myDocument.PK, myDocument.Id, Guid.NewGuid().ToString(), myDocument.Other);
            return this.session.ExecuteAsync(boundStatement);
        }

        public ResponseAttributes HandleResponse(Task request, object context)
        {
            ResponseAttributes responseAttributes = default;
            if (request.IsCompletedSuccessfully)
            {
                Task<RowSet> task = (Task<RowSet>)request;
                using (RowSet rowSet = task.Result)
                {
                    double requestCharge = BitConverter.ToDouble(rowSet.Info.IncomingPayload["RequestCharge"].Reverse().ToArray(), 0);
                    responseAttributes.RequestCharge = requestCharge;
                    responseAttributes.StatusCode = HttpStatusCode.OK;
                }
            }
            else
            {
                Exception ex = request.Exception;
                responseAttributes.StatusCode = ex is AggregateException
                    && ex.InnerException != null
                    && ex.InnerException.Message.Contains("OverloadedException")
                    && ex.InnerException.Message.Contains("3200")
                    ? (HttpStatusCode)429
                    : HttpStatusCode.InternalServerError;
            }

            request.Dispose();
            return responseAttributes;
        }
    }
}
