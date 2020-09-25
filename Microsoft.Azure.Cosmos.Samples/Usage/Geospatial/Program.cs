namespace Cosmos.Samples.Geospatial
{
    using System;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Extensions.Configuration;

    public class Program
    {
        // The Cosmos client instance
        private static CosmosClient cosmosClient;

        // The database we will create
        private static Database database;

        // The container we will create
        private static Container container;

        // The name of the database and container we will use for the demo
        private static readonly string databaseId = "spatial-samples-db";
        private static readonly string containerId = "spatial-samples-cn";

        // The partition key used in the sample
        private static readonly string partitionKey = "/name";

        static async Task Main(string[] args)
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

                Console.WriteLine("Beginning operations...\n");

                //get a Cosmos Client
                using (cosmosClient = new CosmosClient(endpoint, authKey))
                {
                    await Program.RunDemoAsync(cosmosClient);
                }

                Program p = new Program();
            }
            catch (CosmosException de)
            {
                Exception baseException = de.GetBaseException();
                Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run the geospatial demo.
        /// </summary>
        /// <returns>The Task for asynchronous execution.</returns>
        private static async Task RunDemoAsync(CosmosClient cosmosClient)
        {

            // Create the database if necessary
            await Program.Setup(cosmosClient);

            // Create a new container to enable spatial indexing.
            container = await GetContainerWithSpatialIndexingAsync();

            // Spatial Index work
            await Program.SpatialIndex();

            // Uncomment to delete database
            // await database.DeleteAsync();

        }

        private static async Task Setup(CosmosClient cosmosClient)
        {
            database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
        }

        // NOTE: In GeoJSON, longitude comes before latitude.
        // Cosmos DB uses the WGS-84 coordinate reference standard. 
        // Longitudes are between -180 and 180 degrees, and latitudes between -90 and 90 degrees.

        private static async Task SpatialIndex()
        {
            Console.WriteLine("Creating creature items.");

            Creature human = new Creature
            {
                Id = Guid.NewGuid(),
                Name = "stef",
                Species = "Human",
                Location = new Point(31.9, -4.8)
            };

            Creature dragon = new Creature
            {
                Id = Guid.NewGuid(),
                Name = "redEyesBlackDragon",
                Species = "Dragon",
                Location = new Point(31.87, -4.55)
            };

            Creature dragon2 = new Creature
            {
                Id = Guid.NewGuid(),
                Name = "blueEyesWhiteDragon",
                Species = "Dragon",
                Location = new Point(32.33, -4.66)
            };

            Area dragonPit = new Area()
            {
                Id = Guid.NewGuid(),
                Name = "DragonPit",
                Boundary = new Polygon(new List<LinearRing>() { new LinearRing(new Position[] 
                {
                     new Position(31.8, -5), 
                     new Position(32, -5), 
                     new Position(32, -4.7),
                     new Position(31.8, -4.7),
                     new Position(31.8, -5) }) 
                })
            };

            // Insert items with GeoJSON spatial data.
            await container.CreateItemAsync(human, new PartitionKey("stef"));
            await container.CreateItemAsync(dragon, new PartitionKey("redEyesBlackDragon"));
            await container.CreateItemAsync(dragon2, new PartitionKey("blueEyesWhiteDragon"));

            // Check for points within a circle/radius relative to another point. Common for "What's near me?" queries.
            await RunDistanceQuery(human.Location);

            // Check for points within a polygon. Cities/states/natural formations are all commonly represented as polygons.
            await RunWithinPolygonQuery();

            // How to check for valid geospatial objects. Checks for valid latitude/longtiudes and if polygons are well-formed, etc.
            await CheckIfPointOrPolygonIsValid();

            // Now demonstrate how to implement "geo-fencing", i.e. index polygons and query if a point lies within the polygon
            // For example, you can identify when the user of your mobile app enters a new country by storing the polygons representing each country
            // Modify container with Polygon spatial type indexing
            Console.WriteLine("Inserting polygon.");
            await ModifyContainerWithSpatialIndexingAsync();
            await container.CreateItemAsync(dragonPit, new PartitionKey("DragonPit"));

            // Check for points within a polygon. Now, we store polygons, and query for polygons that cover a specified point.
            await RunInverseWithinPolygonQuery();
            await RunIntersectsQuery();
        }

        /// <summary>
        /// Run a distance query using SQL, LINQ and parameterized SQL.
        /// </summary>
        /// <param name="from">The position to measure distance from.</param>
        private static async Task RunDistanceQuery(Point from)
        {
            // Cosmos DB uses the WGS-84 coordinate reference system (CRS). In this reference system, distance is measured in meters. So 30km = 30000m.
            // There are several built-in SQL functions that follow the OGC naming standards and start with the "ST_" prefix for "spatial type".

            // SQL Query
            Console.WriteLine("Performing a ST_DISTANCE proximity query in SQL");
            string sqlQuery = @"SELECT * FROM e where e.species ='Dragon' AND ST_DISTANCE(e.location, {'type': 'Point', 'coordinates':[31.9, -4.8]}) < 30000";
            FeedResponse<Creature> results = await container.GetItemQueryIterator<Creature>(sqlQuery).ReadNextAsync();
            List<Creature> list = results.ToList();
            foreach (Creature item in list)
            {
                Console.WriteLine("SQL QUERY: " + item.Name);
            }
            Console.WriteLine();

            // LINQ query
            Console.WriteLine("Performing a ST_DISTANCE proximity query in LINQ");
            using (FeedIterator<Creature> linqQueryIterator = container.GetItemLinqQueryable<Creature>(allowSynchronousQueryExecution: true)
                .Where(a => a.Species == "Dragon" && a.Location.Distance(from) < 30000)
                .ToFeedIterator<Creature>())
            {
                while (linqQueryIterator.HasMoreResults)
                {
                    foreach (Creature item in await linqQueryIterator.ReadNextAsync())
                    {
                        {
                            Console.WriteLine("LINQ QUERY: " + item);
                        }
                    }
                }
            }
            Console.WriteLine();

            // SQL w/ Parameters
            Console.WriteLine("Performing a ST_DISTANCE proximity query in parameterized SQL");
            QueryDefinition parameterizedSQLQuery = new QueryDefinition("SELECT * FROM e " +
                "WHERE e.species = @species AND ST_DISTANCE(e.location, @human) < 30000")
            .WithParameter("@species", "Dragon")
            .WithParameter("@human", from);
            FeedResponse<Creature> parameterizedSQLQueryResults = await container.GetItemQueryIterator<Creature>(parameterizedSQLQuery).ReadNextAsync();
            List<Creature> parameterizedSQLQueryList = parameterizedSQLQueryResults.ToList();
            foreach (Creature item in parameterizedSQLQueryList)
            {
                Console.WriteLine("QUERY WITH PARAMETER: " + item.Name);
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Run a intersects query using SQL and LINQ to determine if a creature location intersects with a new line. 
        /// </summary>
        private static async Task RunIntersectsQuery()
        {
            // LINQ query
            Console.WriteLine("Performing a ST_INTERSECTS query in LINQ");
            using (FeedIterator<Creature> linqQueryIterator = container.GetItemLinqQueryable<Creature>(allowSynchronousQueryExecution: true)
                .Where(a => a.Location.Intersects(new LineString(new[] 
                    {
                        new Position (32.33, -4.66),
                        new Position (32.34, -4.66),
                        new Position (31.87, -4.55)
                 })))
                .ToFeedIterator<Creature>())
            {
                while (linqQueryIterator.HasMoreResults)
                {
                    foreach (Creature result in await linqQueryIterator.ReadNextAsync())
                    {
                        Console.WriteLine("ST_INTERSECTS - LINQ QUERY:----" + result.Name);
                    }
                }
            }

            Console.WriteLine();

            //SQL query
            Console.WriteLine("Performing a ST_INTERSECTS query in SQL");
            string sqlQuery = "SELECT * FROM e WHERE ST_INTERSECTS(e.location, {'type':'LineString', 'coordinates': [[32.33, -4.66],[32.34, -4.66],[31.87, -4.55]]})";
            FeedResponse<Creature> results = await container.GetItemQueryIterator<Creature>(sqlQuery).ReadNextAsync();
            List<Creature> list = results.ToList();
            foreach (Creature item in list)
            {
                Console.WriteLine("ST_INTERSECTS - SQL QUERY:----" + item.Name);
            }
        }

        /// <summary>
        /// Run a within query (get points within a box/polygon) using SQL and LINQ.
        /// </summary>
        private static async Task RunWithinPolygonQuery()
        {
            // SQL query
            Console.WriteLine("Performing a ST_WITHIN proximity query in SQL");
            string sqlQuery = "SELECT * FROM e WHERE ST_WITHIN(e.location, {'type':'Polygon', 'coordinates': [[[31.8, -5], [32, -5], [32, -4.7], [31.8, -4.7], [31.8, -5]]]})";
            FeedResponse<Creature> results = await container.GetItemQueryIterator<Creature>(sqlQuery).ReadNextAsync();
            List<Creature> list = results.ToList();
            foreach (Creature item in list)
            {
                Console.WriteLine("ST_WITHIN QUERY: " + item.Name);
            }

            // LINQ query
            Console.WriteLine("Performing a ST_WITHIN proximity query in LINQ\n");
            using (FeedIterator<Creature> linqQueryIterator = container.GetItemLinqQueryable<Creature>(allowSynchronousQueryExecution: true).Where(a => a.Location
            .Within(new Polygon(new[] { new LinearRing(new[] { 
                new Position(31.8, -5), 
                new Position(32, -5), 
                new Position(32, -4.7), 
                new Position(31.8, -4.7), 
                new Position(31.8, -5) }) })))
            .ToFeedIterator<Creature>())
            {
                while (linqQueryIterator.HasMoreResults)
                {
                    foreach (Creature item in await linqQueryIterator.ReadNextAsync())
                    {
                        {
                            Console.WriteLine("LINQ QUERY: " + item.Name);
                        }
                    }
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Check if a point or polygon is valid using built-in functions. An important thing to note is that since Cosmos DB's query is designed to handle heterogeneous types, 
        /// bad input parameters will evaluate to "undefined" and get skipped over instead of returning an error. For debugging and fixing malformed geospatial objects, please 
        /// use the built-in functions shown below.
        /// </summary>
        private static async Task CheckIfPointOrPolygonIsValid()
        {
            Console.WriteLine("Checking if a point is valid ...");
            QueryDefinition parameterizedSQLQuery = new QueryDefinition("SELECT ST_ISVALID(@point), ST_ISVALIDDETAILED(@point)")
                .WithParameter("@point", new Point(31.9, -132.8));
            FeedResponse<dynamic> results = await container.GetItemQueryIterator<dynamic>(parameterizedSQLQuery).ReadNextAsync();
            Console.WriteLine($"Point Valid: {results.First()}");

            Console.WriteLine("Checking if a polygon is valid ...");
            QueryDefinition parameterizedSQLQuery2 = new QueryDefinition("SELECT ST_ISVALID(@polygon), ST_ISVALIDDETAILED(@polygon)")
                            .WithParameter("@polygon", new Polygon(new[]
                             {
                                 new LinearRing(new[]
                                 {
                                     new Position(31.8, -5), 
                                     new Position(32, -5), 
                                     new Position(32, -4.7), 
                                     new Position(31.8, -4.7)
                                 })
                             }));
            FeedResponse<dynamic> results2 = await container.GetItemQueryIterator<dynamic>(parameterizedSQLQuery2).ReadNextAsync();
            Console.WriteLine($"Polygon Valid: {results2.First()}");
        }

        /// <summary>
        /// Run a within query (get points within a box/polygon) using SQL and LINQ.
        /// </summary>
        private static async Task RunInverseWithinPolygonQuery()
        {
            // SQL Query
            Console.WriteLine("Performing an inverse ST_WITHIN proximity query in SQL (polygons indexed by Cosmos DB, point supplied as argument)");
            string sqlQuery = "SELECT * FROM everything e WHERE ST_WITHIN({'type':'Point', 'coordinates': [31.9, -4.9]}, e.boundary)";
            FeedResponse<Area> results = await container.GetItemQueryIterator<Area>(sqlQuery).ReadNextAsync();
            List<Area> areaList = results.ToList();
            foreach (Area area in areaList)
            {
                Console.WriteLine("SQL QUERY - ST_WITHIN A POLYGON: " + area);
            }
            Console.WriteLine();

            // LINQ Query
            Console.WriteLine("Performing an inverse ST_WITHIN proximity query in LINQ (polygons indexed by Cosmos DB, point supplied as argument)");
            using (FeedIterator<Area> linqQueryIterator = container.GetItemLinqQueryable<Area>(allowSynchronousQueryExecution: true)
                .Where(a => new Point(31.9, -4.9).Within(a.Boundary))
                .ToFeedIterator<Area>())
                {
                    while (linqQueryIterator.HasMoreResults)
                    {
                        foreach (Area area2 in await linqQueryIterator.ReadNextAsync())
                        {
                            {
                                Console.WriteLine("LINQ QUERY - ST_WITHIN A POLYGON: " + area2);
                            }
                        }
                    }
                }
            Console.WriteLine();
        }

        // Geospatial indexing on a container
        private static async Task<Container> GetContainerWithSpatialIndexingAsync()
        {
            ContainerProperties spatialContainerProperties = new ContainerProperties(containerId, Program.partitionKey);
            SpatialPath locationPath = new SpatialPath { Path = "/location/?" };
            Console.WriteLine("Creating new CONTAINER w/ spatial index...");
               
            locationPath.SpatialTypes.Add(SpatialType.Point);
            spatialContainerProperties.IndexingPolicy.SpatialIndexes.Add(locationPath);
            Container simpleContainer = await database.CreateContainerIfNotExistsAsync(spatialContainerProperties);
     
            return simpleContainer;
        }

        // Modifying geospatial indexing on a container
        private static async Task ModifyContainerWithSpatialIndexingAsync()
        {
            Container containerToUpdate = cosmosClient.GetContainer(databaseId, containerId);

            ContainerProperties updateContainerProperties = new ContainerProperties(containerId, Program.partitionKey);

            //Changing the Geopspatial Config from the deafult of geography to geometry. This change will need to include the addition of a bounding box.
            GeospatialConfig geospatialConfig = new GeospatialConfig(GeospatialType.Geometry);
            updateContainerProperties.GeospatialConfig = geospatialConfig;
            SpatialPath locationPath = new SpatialPath 
            { 
                Path = "/location/?",
                BoundingBox = new BoundingBoxProperties()
                {
                    Xmin = 30,
                    Ymin = -10,
                    Xmax = 40,
                    Ymax = 10
                }
            };

            Console.WriteLine("Updating CONTAINER w/ new spatial index...");

            locationPath.SpatialTypes.Add(SpatialType.Point);
            locationPath.SpatialTypes.Add(SpatialType.LineString);
            locationPath.SpatialTypes.Add(SpatialType.Polygon);

            updateContainerProperties.IndexingPolicy.SpatialIndexes.Add(locationPath);
            await containerToUpdate.ReplaceContainerAsync(updateContainerProperties);
        }
    }
}