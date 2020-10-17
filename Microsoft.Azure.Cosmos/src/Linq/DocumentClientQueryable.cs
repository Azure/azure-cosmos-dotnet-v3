//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;

    internal partial class DocumentClient
    {
        #region Query Methods
        /// <summary>
        /// Overloaded. This method creates a query for database resources under an account in the Azure Cosmos DB service. It returns An IOrderedQueryable{Database}.
        /// </summary>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Database} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for databases by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Database database = client.CreateDatabaseQuery().Where(d => d.Id == "mydb").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<Documents.Database> CreateDatabaseQuery(FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Documents.Database>(this, ResourceType.Database, typeof(Database), Paths.Databases_Root, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for database resources under an Azure Cosmos DB database account by using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for databases by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Database database = client.CreateDatabaseQuery("SELECT * FROM dbs d WHERE d.id = 'mydb'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateDatabaseQuery(string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateDatabaseQuery(new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for database resources under an Azure Cosmos DB database account by using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for databases by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM dbs d WHERE d.id = @id",
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "mydb" }}));
        /// dynamic database = client.CreateDatabaseQuery<dynamic>(query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Database"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateDatabaseQuery(SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Database>(this, ResourceType.Database, typeof(Database), Paths.Databases_Root, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a change feed query for databases under an Azure Cosmos DB database account
        /// in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        internal IDocumentQuery<Documents.Database> CreateDatabaseChangeFeedQuery(ChangeFeedOptions feedOptions)
        {
            ValidateChangeFeedOptionsForNotPartitionedResource(feedOptions);
            return new ChangeFeedQuery<Documents.Database>(this, ResourceType.Database, null, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for collections under an Azure Cosmos DB database. It returns An IOrderedQueryable{DocumentCollection}.
        /// </summary>
        /// <param name="databaseLink">The link to the parent database resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{DocumentCollection} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for collections by id.
        /// <code language="c#">
        /// <![CDATA[
        /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink).Where(c => c.Id == "myColl").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<DocumentCollection> CreateDocumentCollectionQuery(string databaseLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<DocumentCollection>(this, ResourceType.Collection, typeof(DocumentCollection), databaseLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for collections under an Azure Cosmos DB database using a SQL statement.   It returns an IQueryable{DocumentCollection}.
        /// </summary>
        /// <param name="databaseLink">The link to the parent database resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for collections by id.
        /// <code language="c#">
        /// <![CDATA[
        /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink, "SELECT * FROM colls c WHERE c.id = 'mycoll'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateDocumentCollectionQuery(string databaseLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateDocumentCollectionQuery(databaseLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for collections under an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="databaseLink">The link to the parent database resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for collections by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM colls c WHERE c.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "mycoll" }}));
        /// DocumentCollection collection = client.CreateDocumentCollectionQuery(databaseLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="DocumentCollection"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateDocumentCollectionQuery(string databaseLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<DocumentCollection>(this, ResourceType.Collection, typeof(DocumentCollection), databaseLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a change feed query for collections under an Azure Cosmos DB database account
        /// in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseLink">Specifies the database to read collections from.</param>
        /// <param name="feedOptions">Specifies the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        internal IDocumentQuery<DocumentCollection> CreateDocumentCollectionChangeFeedQuery(string databaseLink, ChangeFeedOptions feedOptions)
        {
            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentException(nameof(databaseLink));
            }

            ValidateChangeFeedOptionsForNotPartitionedResource(feedOptions);
            return new ChangeFeedQuery<DocumentCollection>(this, ResourceType.Collection, databaseLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for stored procedures under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{StoredProcedure}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{StoredProcedure} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for stored procedures by id.
        /// <code language="c#">
        /// <![CDATA[
        /// StoredProcedure storedProcedure = client.CreateStoredProcedureQuery(collectionLink).Where(c => c.Id == "helloWorld").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="StoredProcedure"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<StoredProcedure> CreateStoredProcedureQuery(string collectionLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<StoredProcedure>(this, ResourceType.StoredProcedure, typeof(StoredProcedure), collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for stored procedures under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for stored procedures by id.
        /// <code language="c#">
        /// <![CDATA[
        /// StoredProcedure storedProcedure = client.CreateStoredProcedureQuery(collectionLink, "SELECT * FROM sprocs s WHERE s.id = 'HelloWorld'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="StoredProcedure"/>
        public IQueryable<dynamic> CreateStoredProcedureQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateStoredProcedureQuery(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for stored procedures under a collection in an Azure Cosmos DB database using a SQL statement using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for stored procedures by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM sprocs s WHERE s.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "HelloWorld" }}));
        /// StoredProcedure storedProcedure = client.CreateStoredProcedureQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="StoredProcedure"/>
        public IQueryable<dynamic> CreateStoredProcedureQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<StoredProcedure>(this, ResourceType.StoredProcedure, typeof(StoredProcedure), collectionLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for triggers under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{Trigger}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Trigger} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for triggers by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Trigger trigger = client.CreateTriggerQuery(collectionLink).Where(t => t.Id == "validate").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<Trigger> CreateTriggerQuery(string collectionLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Trigger>(this, ResourceType.Trigger, typeof(Trigger), collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for triggers under a collection in an Azure Cosmos DB service. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for triggers by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Trigger trigger = client.CreateTriggerQuery(collectionLink, "SELECT * FROM triggers t WHERE t.id = 'validate'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateTriggerQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateTriggerQuery(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for triggers under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{Trigger} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for triggers by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM triggers t WHERE t.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "HelloWorld" }}));
        /// Trigger trigger = client.CreateTriggerQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Trigger"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateTriggerQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Trigger>(this, ResourceType.Trigger, typeof(Trigger), collectionLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for udfs under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{UserDefinedFunction}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{UserDefinedFunction} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for user-defined functions by id.
        /// <code language="c#">
        /// <![CDATA[
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink).Where(u => u.Id == "sqrt").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<UserDefinedFunction> CreateUserDefinedFunctionQuery(string collectionLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<UserDefinedFunction>(this, ResourceType.UserDefinedFunction, typeof(UserDefinedFunction), collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for udfs under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for user-defined functions by id.
        /// <code language="c#">
        /// <![CDATA[
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink, "SELECT * FROM udfs u WHERE u.id = 'sqrt'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateUserDefinedFunctionQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateUserDefinedFunctionQuery(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for udfs under a collection in an Azure Cosmos DB database with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for user-defined functions by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM udfs u WHERE u.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "sqrt" }}));
        /// UserDefinedFunction udf = client.CreateUserDefinedFunctionQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="UserDefinedFunction"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateUserDefinedFunctionQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<UserDefinedFunction>(this, ResourceType.UserDefinedFunction, typeof(UserDefinedFunction), collectionLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for conflicts under a collection in an Azure Cosmos DB service. It returns An IOrderedQueryable{Conflict}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Conflict} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for conflicts by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Conflict conflict = client.CreateConflictQuery(collectionLink).Where(c => c.Id == "summary").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<Conflict> CreateConflictQuery(string collectionLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Conflict>(this, ResourceType.Conflict, typeof(Conflict), collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for conflicts under a collection in an Azure Cosmos DB service. It returns an IQueryable{Conflict}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for conflicts by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM conflicts c WHERE c.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "summary" }}));
        /// Conflict conflict = client.CreateConflictQuery(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateConflictQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateConflictQuery(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for conflicts under a collection in an Azure Cosmos DB database with parameterized values. It returns an IQueryable{dynamic}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{dynamic} that can evaluate the query with the provided SQL statement.</returns>
        /// <example>
        /// This example below queries for conflicts by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec("SELECT * FROM conflicts c WHERE c.id = @id", new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "summary" }}));
        /// dynamic conflict = client.CreateConflictQuery<dynamic>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateConflictQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Conflict>(this, ResourceType.Conflict, typeof(Conflict), collectionLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent collection resource.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for some book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     [JsonProperty("title")]
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        ///  
        /// // Query by the Title property
        /// Book book = client.CreateDocumentQuery<Book>(collectionLink).Where(b => b.Title == "War and Peace").AsEnumerable().FirstOrDefault();
        /// 
        /// // Query a nested property
        /// Book otherBook = client.CreateDocumentQuery<Book>(collectionLink).Where(b => b.Author.FirstName == "Leo").AsEnumerable().FirstOrDefault();
        /// 
        /// // Perform a range query (needs an IndexType.Range on price or FeedOptions.EnableScansInQuery)
        /// foreach (Book matchingBook in client.CreateDocumentQuery<Book>(collectionLink).Where(b => b.Price > 100))
        /// {
        ///     // Iterate through books
        /// }
        /// 
        /// // Query asychronously. Optionally set FeedOptions.MaxItemCount to control page size
        /// using (var queryable = client.CreateDocumentQuery<Book>(
        ///     collectionLink,
        ///     new FeedOptions { MaxItemCount = 10 })
        ///     .Where(b => b.Title == "War and Peace")
        ///     .AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// The Azure Cosmos DB LINQ provider compiles LINQ to SQL statements. Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started#linq-to-documentdb-sql for the list of expressions supported by the Azure Cosmos DB LINQ provider. ToString() on the generated IQueryable returns the translated SQL statement. The Azure Cosmos DB provider translates JSON.NET and DataContract serialization attributes for members to their JSON property names.
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<T> CreateDocumentQuery<T>(string collectionLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<T>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="documentsFeedOrDatabaseLink">The path link for the documents under a collection, e.g. dbs/db_rid/colls/coll_rid/docs/. 
        /// Alternatively, this can be a path link to the database when using an IPartitionResolver, e.g. dbs/db_rid/</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>An IOrderedQueryable{T} that can evaluate the query.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IOrderedQueryable<T> CreateDocumentQuery<T>(string documentsFeedOrDatabaseLink, FeedOptions feedOptions, object partitionKey)
        {
            return new DocumentQuery<T>(this, ResourceType.Document, typeof(Document), documentsFeedOrDatabaseLink, feedOptions, partitionKey);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{T}.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent collection.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for some book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     [JsonProperty("title")]
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        /// 
        /// // Query by the Title property
        /// Book book = client.CreateDocumentQuery<Book>(collectionLink, 
        ///     "SELECT * FROM books b WHERE b.title  = 'War and Peace'").AsEnumerable().FirstOrDefault();
        /// 
        /// // Query a nested property
        /// Book otherBook = client.CreateDocumentQuery<Book>(collectionLink,
        ///     "SELECT * FROM books b WHERE b.Author.FirstName = 'Leo'").AsEnumerable().FirstOrDefault();
        /// 
        /// // Perform a range query (needs an IndexType.Range on price or FeedOptions.EnableScansInQuery)
        /// foreach (Book matchingBook in client.CreateDocumentQuery<Book>(
        ///     collectionLink, "SELECT * FROM books b where b.Price > 1000"))
        /// {
        ///     // Iterate through books
        /// }
        /// 
        /// // Query asychronously. Optionally set FeedOptions.MaxItemCount to control page size
        /// using (var queryable = client.CreateDocumentQuery<Book>(collectionLink, 
        ///     "SELECT * FROM books b WHERE b.title  = 'War and Peace'", 
        ///     new FeedOptions { MaxItemCount = 10 }).AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<T> CreateDocumentQuery<T>(string collectionLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateDocumentQuery<T>(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{T}.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The path link for the documents under a collection, e.g. dbs/db_rid/colls/coll_rid/docs/. 
        /// Alternatively, this can be a path link to the database when using an IPartitionResolver, e.g. dbs/db_rid/</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>An IQueryable{T} that can evaluate the query.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<T> CreateDocumentQuery<T>(string collectionLink, string sqlExpression, FeedOptions feedOptions, object partitionKey)
        {
            return this.CreateDocumentQuery<T>(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions, partitionKey);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{T}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for some book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     [JsonProperty("title")]
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        /// 
        /// // Query using Title
        /// Book book, otherBook;
        /// 
        /// var query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.title = @title", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@title", Value = "War and Peace" }}));
        /// book = client.CreateDocumentQuery<Book>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// 
        /// // Query a nested property
        /// query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.Author.FirstName = @firstName", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@firstName", Value = "Leo" }}));
        /// otherBook = client.CreateDocumentQuery<Book>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// 
        /// // Perform a range query (needs an IndexType.Range on price or FeedOptions.EnableScansInQuery)
        /// query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.Price > @minPrice", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@minPrice", Value = 1000 }}));
        /// foreach (Book b in client.CreateDocumentQuery<Book>(
        ///     collectionLink, query))
        /// {
        ///     // Iterate through books
        /// }
        /// 
        /// // Query asychronously. Optionally set FeedOptions.MaxItemCount to control page size
        /// query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.title = @title", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@title", Value = "War and Peace" }}));
        ///     
        /// using (var queryable = client.CreateDocumentQuery<Book>(collectionLink, query, 
        ///     new FeedOptions { MaxItemCount = 10 }).AsDocumentQuery())
        /// {
        ///     while (queryable.HasMoreResults) 
        ///     {
        ///         foreach(Book b in await queryable.ExecuteNextAsync<Book>())
        ///         {
        ///             // Iterate through books
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<T> CreateDocumentQuery<T>(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<T>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions).AsSQL<T, T>(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{T}.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="collectionLink">The link to the parent document collection.
        /// Alternatively, this can be a path link to the database when using an IPartitionResolver.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>An IQueryable{T} that can evaluate the query.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<T> CreateDocumentQuery<T>(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions, object partitionKey)
        {
            return new DocumentQuery<T>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions, partitionKey).AsSQL<T, T>(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB service. It returns IOrderedQueryable{Document}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Document} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for documents by id.
        /// <code language="c#">
        /// <![CDATA[
        /// Document document = client.CreateDocumentQuery<Document>(collectionLink)
        ///     .Where(d => d.Id == "War and Peace").AsEnumerable().FirstOrDefault();
        ///
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// This overload should be used when the schema of the queried documents is unknown or when querying by ID and replacing/deleting documents.
        /// Since Document is a DynamicObject, it can be dynamically cast back to the original C# object.
        /// </remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<Document> CreateDocumentQuery(string collectionLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Document>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB service. It returns IOrderedQueryable{Document}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.
        /// Alternatively, this can be a path link to the database when using an IPartitionResolver.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <param name="partitionKey">Optional partition key that can be used with an IPartitionResolver.</param>
        /// <returns>An IOrderedQueryable{Document} that can evaluate the query.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IOrderedQueryable<Document> CreateDocumentQuery(string collectionLink, FeedOptions feedOptions, object partitionKey)
        {
            return new DocumentQuery<Document>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions, partitionKey);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic> that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// // SQL querying allows dynamic property access
        /// dynamic document = client.CreateDocumentQuery<dynamic>(collectionLink,
        ///     "SELECT * FROM books b WHERE b.title == 'War and Peace'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateDocumentQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateDocumentQuery(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement. It returns an IQueryable{dynamic}.
        /// </summary>
        /// <param name="collectionLink">The link of the parent document collection.
        /// Alternatively, this can be a path link to the database when using an IPartitionResolver, e.g. dbs/db_rid/</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>an IQueryable{dynamic> that can evaluate the query.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<dynamic> CreateDocumentQuery(string collectionLink, string sqlExpression, FeedOptions feedOptions, object partitionKey)
        {
            return this.CreateDocumentQuery(collectionLink, new SqlQuerySpec(sqlExpression), feedOptions, partitionKey);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic> that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for book documents.
        /// <code language="c#">
        /// <![CDATA[
        /// // SQL querying allows dynamic property access
        /// var query = new SqlQuerySpec(
        ///     "SELECT * FROM books b WHERE b.title = @title", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@title", Value = "War and Peace" }}));
        ///     
        /// dynamic document = client.CreateDocumentQuery<dynamic>(collectionLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Document"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateDocumentQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Document>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for documents under a collection in an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="collectionLink">The link to the parent document collection.
        /// Alternatively, this can be a path link to the database when using an IPartitionResolver, e.g. dbs/db_rid/</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <param name="partitionKey">The partition key that can be used with an IPartitionResolver.</param>
        /// <returns>an IQueryable{dynamic> that can evaluate the query.</returns>
        /// <remarks>
        /// Support for IPartitionResolver based method overloads is now obsolete. It's recommended that you use 
        /// <a href="https://azure.microsoft.com/documentation/articles/documentdb-partition-data">Partitioned Collections</a> for higher storage and throughput.
        /// </remarks>
        [Obsolete("Support for IPartitionResolver based method overloads is now obsolete. " +
                  "It's recommended that you use partitioned collections for higher storage and throughput." +
                  " Please use the override that does not take a partitionKey parameter.")]
        public IQueryable<dynamic> CreateDocumentQuery(string collectionLink, SqlQuerySpec querySpec, FeedOptions feedOptions, object partitionKey)
        {
            return new DocumentQuery<Document>(this, ResourceType.Document, typeof(Document), collectionLink, feedOptions, partitionKey).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a change feed query for documents under a collection in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="collectionLink">Specifies the collection to read documents from.</param>
        /// <param name="feedOptions">The options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        /// <remarks>ChangeFeedOptions.PartitionKeyRangeId must be provided.</remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// string partitionKeyRangeId = "0";   // Use client.ReadPartitionKeyRangeFeedAsync() to obtain the ranges.
        /// string checkpointContinuation = null;
        /// ChangeFeedOptions options = new ChangeFeedOptions
        /// {
        ///     PartitionKeyRangeId = partitionKeyRangeId,
        ///     RequestContinuation = checkpointContinuation,
        ///     StartFromBeginning = true,
        /// };
        /// using(var query = client.CreateDocumentChangeFeedQuery(collection.SelfLink, options))
        /// {
        ///     while (true)
        ///     {
        ///         do
        ///         {
        ///             var response = await query.ExecuteNextAsync<Document>();
        ///             if (response.Count > 0)
        ///             {
        ///                 var docs = new List<Document>();
        ///                 docs.AddRange(response);
        ///                 // Process the documents.
        ///                 // Checkpoint response.ResponseContinuation.
        ///             }
        ///         }
        ///         while (query.HasMoreResults);
        ///         Task.Delay(TimeSpan.FromMilliseconds(500)); // Or break here and use checkpointed continuation token later.
        ///     }       
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery{T}"/>
        /// <seealso cref="ChangeFeedOptions"/>
        /// <seealso cref="Microsoft.Azure.Documents.PartitionKeyRange"/>
        public IDocumentQuery<Document> CreateDocumentChangeFeedQuery(string collectionLink, ChangeFeedOptions feedOptions)
        {
            if (collectionLink == null)
            {
                throw new ArgumentNullException(nameof(collectionLink));
            }

            return new ChangeFeedQuery<Document>(this, ResourceType.Document, collectionLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for offers under an Azure Cosmos DB database account. It returns IOrderedQueryable{Offer}.
        /// </summary>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{Offer} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for offers
        /// <code language="c#">
        /// <![CDATA[
        /// // Find the offer for the collection by SelfLink
        /// Offer offer = client.CreateOfferQuery().Where(o => o.Resource == collectionSelfLink).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IOrderedQueryable<Offer> CreateOfferQuery(FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Offer>(this, ResourceType.Offer, typeof(Offer), Paths.Offers_Root, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for offers under an Azure Cosmos DB database account using a SQL statement. It returns IQueryable{dynamic}.
        /// </summary>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for offers
        /// <code language="c#">
        /// <![CDATA[
        /// // Find the offer for the collection by SelfLink
        /// Offer offer = client.CreateOfferQuery(
        ///     string.Format("SELECT * FROM offers o WHERE o.resource = '{0}'", collectionSelfLink)).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateOfferQuery(string sqlExpression, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Offer>(this, ResourceType.Offer, typeof(Offer), Paths.Offers_Root, feedOptions).AsSQL(new SqlQuerySpec(sqlExpression));
        }

        /// <summary>
        /// Overloaded. This method creates a query for offers under an Azure Cosmos DB database account using a SQL statement with parameterized values. It returns IQueryable{dynamic}.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for offers
        /// <code language="c#">
        /// <![CDATA[
        /// // Find the offer for the collection by SelfLink
        /// Offer offer = client.CreateOfferQuery("SELECT * FROM offers o WHERE o.resource = @collectionSelfLink",
        /// new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@collectionSelfLink", Value = collection.SelfLink }}))
        /// .AsEnumerable().FirstOrDefault();
        /// 
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="Microsoft.Azure.Documents.Offer"/>
        /// <seealso cref="Microsoft.Azure.Cosmos.Linq.IDocumentQuery"/>
        public IQueryable<dynamic> CreateOfferQuery(SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<Offer>(this, ResourceType.Offer, typeof(Offer), Paths.Offers_Root, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a query for user defined types under an Azure Cosmos DB service. It returns IOrderedQueryable{UserDefinedType}.
        /// </summary>
        /// <param name="userDefinedTypesLink">The path link for the user defined types under a database, e.g. dbs/db_rid/udts/.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>An IOrderedQueryable{UserDefinedType} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for user defined types by id.
        /// <code language="c#">
        /// <![CDATA[
        /// UserDefinedType userDefinedTypes = client.CreateUserDefinedTypeQuery(userDefinedTypesLink).Where(u => u.Id == "userDefinedTypeId5").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="UserDefinedType"/>
        /// <seealso cref="IDocumentQuery"/>
        internal IOrderedQueryable<UserDefinedType> CreateUserDefinedTypeQuery(string userDefinedTypesLink, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<UserDefinedType>(this, ResourceType.UserDefinedType, typeof(UserDefinedType), userDefinedTypesLink, feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for user defined types under an Azure Cosmos DB service. It returns IQueryable{dynamic}.
        /// </summary>
        /// <param name="userDefinedTypesLink">The path link for the user defined types under a database, e.g. dbs/db_rid/udts/.</param>
        /// <param name="sqlExpression">The SQL statement.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for user defined types by id.
        /// <code language="c#">
        /// <![CDATA[
        /// UserDefinedType userDefinedTypes = client.CreateUserDefinedTypeQuery(userDefinedTypesLink, "SELECT * FROM userDefinedTypes u WHERE u.id = 'userDefinedTypeId5'").AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="UserDefinedType"/>
        /// <seealso cref="IDocumentQuery"/>
        internal IQueryable<dynamic> CreateUserDefinedTypeQuery(string userDefinedTypesLink, string sqlExpression, FeedOptions feedOptions = null)
        {
            return this.CreateUserDefinedTypeQuery(userDefinedTypesLink, new SqlQuerySpec(sqlExpression), feedOptions);
        }

        /// <summary>
        /// Overloaded. This method creates a query for user defined types under an Azure Cosmos DB database using a SQL statement with parameterized values. It returns an IQueryable{dynamic}.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="SqlQuerySpec"/>.
        /// </summary>
        /// <param name="userDefinedTypesLink">The path link for the user defined types under a database, e.g. dbs/db_rid/udts/.</param>
        /// <param name="querySpec">The SqlQuerySpec instance containing the SQL expression.</param>
        /// <param name="feedOptions">The options for processing the query result feed. For details, see <see cref="T:Microsoft.Azure.Documents.Client.FeedOptions"/></param>
        /// <returns>an IQueryable{dynamic} that can evaluate the query.</returns>
        /// <example>
        /// This example below queries for user defined types by id.
        /// <code language="c#">
        /// <![CDATA[
        /// var query = new SqlQuerySpec(
        ///     "SELECT * FROM userDefinedTypes u WHERE u.id = @id", 
        ///     new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@id", Value = "userDefinedTypeId5" }}));
        ///     
        /// UserDefinedType userDefinedType = client.CreateUserDefinedTypeQuery(userDefinedTypesLink, query).AsEnumerable().FirstOrDefault();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>Refer to https://msdn.microsoft.com/en-us/library/azure/dn782250.aspx and https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.</remarks>
        /// <seealso cref="UserDefinedType"/>
        /// <seealso cref="IDocumentQuery"/>
        internal IQueryable<dynamic> CreateUserDefinedTypeQuery(string userDefinedTypesLink, SqlQuerySpec querySpec, FeedOptions feedOptions = null)
        {
            return new DocumentQuery<UserDefinedType>(this, ResourceType.UserDefinedType, typeof(UserDefinedType), userDefinedTypesLink, feedOptions).AsSQL(querySpec);
        }

        /// <summary>
        /// Overloaded. This method creates a change feed query for user defined types under an Azure Cosmos DB database account
        /// in an Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseLink">Specifies the database to read user defined types from.</param>
        /// <param name="feedOptions">Specifies the options for processing the query results feed.</param>
        /// <returns>the query result set.</returns>
        internal IDocumentQuery<UserDefinedType> CreateUserDefinedTypeChangeFeedQuery(string databaseLink, ChangeFeedOptions feedOptions)
        {
            if (string.IsNullOrEmpty(databaseLink))
            {
                throw new ArgumentException(nameof(databaseLink));
            }

            ValidateChangeFeedOptionsForNotPartitionedResource(feedOptions);
            return new ChangeFeedQuery<UserDefinedType>(this, ResourceType.UserDefinedType, databaseLink, feedOptions);
        }
        #endregion Query Methods

        #region Helpers
        private static void ValidateChangeFeedOptionsForNotPartitionedResource(ChangeFeedOptions feedOptions)
        {
            if (feedOptions != null &&
                (feedOptions.PartitionKey != null || !string.IsNullOrEmpty(feedOptions.PartitionKeyRangeId)))
            {
                throw new ArgumentException(RMResources.CannotSpecifyPKRangeForNonPartitionedResource);
            }
        }
        #endregion Helpers
    }
}
