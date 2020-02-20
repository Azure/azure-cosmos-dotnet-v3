//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Helper class to assist in creating the various Uris needed for use with the DocumentClient instance in the Azure Cosmos DB service.
    /// </summary>
    /// <example>
    /// The example below uses UriFactory to create a DocumentCollectionLink and then uses that to create a Document.
    /// <code language="c#">
    /// <![CDATA[ 
    /// Uri collUri = UriFactory.CreateDocumentCollectionUri("MyDb", "MyCollection");
    /// var doc = await client.CreateDocumentAsync(collUri, new {id = "MyDoc"});
    /// ]]>
    /// </code>
    /// </example>
    internal static class UriFactory
    {
        /// <summary>
        /// Given a database id, this creates a database link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <returns>
        /// A database link in the format of /dbs/{0}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>
        /// </returns>
        /// <remarks>Would be used when creating or deleting a <see cref="DocumentCollection"/> or a <see cref="User"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateDatabaseUri(string databaseId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database and collection id, this creates a collection link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <returns>
        /// A collection link in the format of /dbs/{0}/colls/{1}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/> and {1} being <paramref name="collectionId"/>
        /// </returns>
        /// <remarks>Would be used when updating or deleting a <see cref="DocumentCollection"/>, creating a <see cref="Document"/>, a <see cref="StoredProcedure"/>, a <see cref="Trigger"/>, a <see cref="UserDefinedFunction"/>, or when executing a query with CreateDocumentQuery in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        [Obsolete("CreateCollectionUri method is deprecated, please use CreateDocumentCollectionUri method instead.")]
        static public Uri CreateCollectionUri(string databaseId, string collectionId)
        {
            return CreateDocumentCollectionUri(databaseId, collectionId);
        }

        /// <summary>
        /// Given a database and collection id, this creates a collection link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <returns>
        /// A collection link in the format of /dbs/{0}/colls/{1}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/> and {1} being <paramref name="collectionId"/>
        /// </returns>
        /// <remarks>Would be used when updating or deleting a <see cref="DocumentCollection"/>, creating a <see cref="Document"/>, a <see cref="StoredProcedure"/>, a <see cref="Trigger"/>, a <see cref="UserDefinedFunction"/>, or when executing a query with CreateDocumentQuery in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateDocumentCollectionUri(string databaseId, string collectionId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}", Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database and client encryption key id, this creates a client encryption key link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="clientEncryptionKeyId">The data encryption key id</param>
        /// <returns>
        /// A data encryption key link in the format of /dbs/{0}/clientEncryptionkeys/{1}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/> and {1} being <paramref name="clientEncryptionKeyId"/>
        /// </returns>
        /// <remarks>
        /// Would be used when updating or deleting a <see cref="ClientEncryptionKey"/>
        /// </remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static internal Uri CreateClientEncryptionKeyUri(string databaseId, string clientEncryptionKeyId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}", Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                Paths.ClientEncryptionKeysPathSegment, Uri.EscapeUriString(clientEncryptionKeyId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database and user id, this creates a user link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="userId">The user id</param>
        /// <returns>
        /// A user link in the format of /dbs/{0}/users/{1}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/> and {1} being <paramref name="userId"/>
        /// </returns>
        /// <remarks>Would be used when creating a <see cref="Permission"/>, or when replacing or deleting a <see cref="User"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateUserUri(string databaseId, string userId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.UsersPathSegment, Uri.EscapeUriString(userId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database and user defined type id, this creates a user defined type link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="userDefinedTypeId">The user defined type id</param>
        /// <returns>
        /// A user defined type link in the format of /dbs/{0}/udts/{1}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/> and {1} being <paramref name="userDefinedTypeId"/>
        /// </returns>
        /// <remarks>Would be used when creating a <see cref="UserDefinedType"/>, or when replacing or deleting a <see cref="UserDefinedType"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static internal Uri CreateUserDefinedTypeUri(string databaseId, string userDefinedTypeId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.UserDefinedTypesPathSegment, Uri.EscapeUriString(userDefinedTypeId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection and document id, this creates a document link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="documentId">The document id</param>
        /// <returns>
        /// A document link in the format of /dbs/{0}/colls/{1}/docs/{2}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/> and {2} being the <paramref name="documentId"/>
        /// </returns>
        /// <remarks>Would be used when creating an <see cref="Attachment"/>, or when replacing or deleting a <see cref="Document"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateDocumentUri(string databaseId, string collectionId, string documentId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.DocumentsPathSegment, Uri.EscapeUriString(documentId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database and user id, this creates a permission link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="userId">The user id</param>
        /// <param name="permissionId">The permission id</param>
        /// <returns>
        /// A permission link in the format of /dbs/{0}/users/{1}/permissions/{2} with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="userId"/> and {2} being <paramref name="permissionId"/>
        /// </returns>
        /// <remarks>Would be used when replacing or deleting a <see cref="Permission"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreatePermissionUri(string databaseId, string userId, string permissionId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.UsersPathSegment, Uri.EscapeUriString(userId),
                            Paths.PermissionsPathSegment, Uri.EscapeUriString(permissionId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection and stored proc id, this creates a stored proc link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="storedProcedureId">The stored procedure id</param>
        /// <returns>
        /// A stored procedure link in the format of /dbs/{0}/colls/{1}/sprocs/{2}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/> and {2} being the <paramref name="storedProcedureId"/>
        /// </returns>
        /// <remarks>Would be used when replacing, executing, or deleting a <see cref="StoredProcedure"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateStoredProcedureUri(string databaseId, string collectionId, string storedProcedureId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.StoredProceduresPathSegment, Uri.EscapeUriString(storedProcedureId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a collection link and stored proc id, this creates a stored proc link.
        /// </summary>
        /// <param name="documentCollectionLink">The collection link</param>
        /// <param name="storedProcedureId">The stored procedure id</param>
        /// <returns>
        /// A stored procedure link in the format of {0}/sprocs/{1}/ with {0} being <paramref name="documentCollectionLink"/> and {1} being <paramref name="storedProcedureId"/>
        /// </returns>
        /// <remarks>Would be used when replacing, executing, or deleting a <see cref="StoredProcedure"/> in Azure DocumentDB.</remarks>
        static internal Uri CreateStoredProcedureUri(string documentCollectionLink, string storedProcedureId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}",
                            documentCollectionLink,
                            Paths.StoredProceduresPathSegment, Uri.EscapeUriString(storedProcedureId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection and trigger id, this creates a trigger link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="triggerId">The trigger id</param>
        /// <returns>
        /// A trigger link in the format of /dbs/{0}/colls/{1}/triggers/{2}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/> and {2} being the <paramref name="triggerId"/>
        /// </returns>
        /// <remarks>Would be used when replacing, executing, or deleting a <see cref="Trigger"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateTriggerUri(string databaseId, string collectionId, string triggerId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.TriggersPathSegment, Uri.EscapeUriString(triggerId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection and udf id, this creates a udf link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="udfId">The udf id</param>
        /// <returns>
        /// A udf link in the format of /dbs/{0}/colls/{1}/udfs/{2}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/> and {2} being the <paramref name="udfId"/>
        /// </returns>
        /// <remarks>Would be used when replacing, executing, or deleting a <see cref="UserDefinedFunction"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateUserDefinedFunctionUri(string databaseId, string collectionId, string udfId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.UserDefinedFunctionsPathSegment, Uri.EscapeUriString(udfId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection and conflict id, this creates a conflict link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="conflictId">The conflict id</param>
        /// <returns>
        /// A conflict link in the format of /dbs/{0}/colls/{1}/conflicts/{2}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/> and {2} being the <paramref name="conflictId"/>
        /// </returns>
        /// <remarks>Would be used when creating a <see cref="Conflict"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateConflictUri(string databaseId, string collectionId, string conflictId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.ConflictsPathSegment, Uri.EscapeUriString(conflictId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection, document, and attachment id, this creates an attachment link.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="documentId">The document id</param>
        /// <param name="attachmentId">The attachment id</param>
        /// <returns>
        /// An attachment link in the format of /dbs/{0}/colls/{1}/docs/{2}/attachments/{3} with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/>, {2} being the <paramref name="documentId"/> and {3} being <paramref name="attachmentId"/>
        /// </returns>
        /// <remarks>Would be used when replacing, or deleting an <see cref="Attachment"/> in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreateAttachmentUri(string databaseId, string collectionId, string documentId, string attachmentId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}/{6}/{7}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.DocumentsPathSegment, Uri.EscapeUriString(documentId),
                            Paths.AttachmentsPathSegment, Uri.EscapeUriString(attachmentId)), UriKind.Relative);
        }

        /// <summary>
        /// Given a database and collection, this creates a partition key ranges link in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <returns>
        /// A partition key ranges link in the format of /dbs/{0}/colls/{1}/pkranges with {0} being a Uri escaped version of the <paramref name="databaseId"/> and {1} being <paramref name="collectionId"/>.
        /// </returns>
        /// <seealso cref="Uri.EscapeUriString"/>
        static public Uri CreatePartitionKeyRangesUri(string databaseId, string collectionId)
        {
            return new Uri(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/{1}/{2}/{3}/{4}",
                    Paths.DatabasesPathSegment,
                    Uri.EscapeUriString(databaseId),
                    Paths.CollectionsPathSegment,
                    Uri.EscapeUriString(collectionId),
                    Paths.PartitionKeyRangesPathSegment),
                UriKind.Relative);
        }

        /// <summary>
        /// Given a database, collection and schema id, this creates a schema link in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="databaseId">The database id</param>
        /// <param name="collectionId">The collection id</param>
        /// <param name="schemaId">The schema id</param>
        /// <returns>
        /// A schema link in the format of /dbs/{0}/colls/{1}/schemas/{2}/ with {0} being a Uri escaped version of the <paramref name="databaseId"/>, {1} being <paramref name="collectionId"/> and {2} being the <paramref name="schemaId"/>
        /// </returns>
        /// <seealso cref="Uri.EscapeUriString"/>
        static internal Uri CreateSchemaUri(string databaseId, string collectionId, string schemaId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/{3}/{4}/{5}",
                            Paths.DatabasesPathSegment, Uri.EscapeUriString(databaseId),
                            Paths.CollectionsPathSegment, Uri.EscapeUriString(collectionId),
                            Paths.SchemasPathSegment, Uri.EscapeUriString(schemaId)), UriKind.Relative);
        }
    }
}