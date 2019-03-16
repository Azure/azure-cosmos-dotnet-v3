//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class DirectExtensions
    {
        public static TResource GetResource<TResource>(this DocumentServiceResponse documentServiceResponse, ITypeResolver<TResource> typeResolver = null) where TResource : CosmosResource, new()
        {
            if (documentServiceResponse.ResponseBody != null && !(documentServiceResponse.ResponseBody.CanSeek && documentServiceResponse.ResponseBody.Length == 0))
            {
                // attempt to get a type resolver.
                if (typeResolver == null)
                {
                    // attempt to get a type resolver.
                    typeResolver = GetTypeResolver<TResource>();
                }

                if (!documentServiceResponse.ResponseBody.CanSeek)
                {
                    MemoryStream ms = new MemoryStream();
                    documentServiceResponse.ResponseBody.CopyTo(ms);
                    documentServiceResponse.ResponseBody.Dispose();
                    documentServiceResponse.ResponseBody = ms;
                    documentServiceResponse.ResponseBody.Seek(0, SeekOrigin.Begin);
                }

                TResource resource = CosmosResource.LoadFrom<TResource>(documentServiceResponse.ResponseBody, typeResolver, documentServiceResponse.serializerSettings);
                resource.SerializerSettings = documentServiceResponse.serializerSettings;
                documentServiceResponse.ResponseBody.Seek(0, SeekOrigin.Begin);

                if (DirectExtensions.IsPublicResource(typeof(TResource)))
                {
                    resource.AltLink = DirectExtensions.GeneratePathForNameBased(typeof(TResource), documentServiceResponse.GetOwnerFullName(), resource.Id);
                }
                else if (typeof(TResource).IsGenericType() &&
                         typeof(TResource).GetGenericTypeDefinition() == typeof(FeedResource<>))
                {
                    // for feed, set FeedResource.Altlink to be the ownerFullName
                    resource.AltLink = documentServiceResponse.GetOwnerFullName();
                }

                return resource;
            }
            else
            {
                return default(TResource);
            }
        }

        //All Query Responses are materialized as IEnumerable<dynamic>. This enumerator can be handed out to
        //client as IQueryable<dynamic> which can be casted/materialized to whatever type client
        //wanted to see them as.
        public static IEnumerable<dynamic> GetQueryResponse(this DocumentServiceResponse documentServiceResponse, Type resourceType, out int itemCount)
        {
            return documentServiceResponse.GetQueryResponse<object>(resourceType, false, out itemCount);
        }

        public static IEnumerable<T> GetQueryResponse<T>(this DocumentServiceResponse documentServiceResponse, Type resourceType, bool lazy, out int itemCount)
        {
            if (!int.TryParse(documentServiceResponse.Headers[HttpConstants.HttpHeaders.ItemCount], out itemCount))
            {
                itemCount = 0;
            }

            IEnumerable<T> enumerable;

            if (typeof(T) == typeof(object))
            {
                string ownerName = null;
                if (DirectExtensions.IsPublicResource(resourceType))
                {
                    ownerName = documentServiceResponse.GetOwnerFullName();
                }

                IEnumerable<object> objectEnumerable = documentServiceResponse.GetEnumerable<object>(resourceType, (jsonReader) =>
                {
                    JToken jToken = JToken.Load(jsonReader);
                    if (jToken.Type == JTokenType.Object || jToken.Type == JTokenType.Array) //If it is non-primitive type, wrap them in QueryResult.
                    {
                        return new QueryResult((JContainer)jToken, ownerName, documentServiceResponse.serializerSettings);
                    }
                    else //Primitive type.
                    {
                        return jToken;
                    }
                });

                enumerable = (IEnumerable<T>)objectEnumerable;
            }
            else
            {
                JsonSerializer serializer = documentServiceResponse.serializerSettings != null ? JsonSerializer.Create(documentServiceResponse.serializerSettings) : JsonSerializer.Create();
                enumerable = documentServiceResponse.GetEnumerable(resourceType, (jsonReader) => serializer.Deserialize<T>(jsonReader));
            }

            if (lazy)
            {
                return enumerable;
            }
            else
            {
                List<T> result = new List<T>(itemCount);
                result.AddRange(enumerable);
                return result;
            }
        }

        private static IEnumerable<T> GetEnumerable<T>(this DocumentServiceResponse documentServiceResponse, Type resourceType, Func<JsonReader, T> callback)
        {
            // Execute the callback an each element of the page
            // For example just could get a response like this
            // {
            //    "_rid": "qHVdAImeKAQ=",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }
            // And you should execute the callback on each document in "Documents".

            if (documentServiceResponse.ResponseBody == null)
            {
                yield break;
            }

            using (JsonReader jsonReader = DocumentServiceResponse.Create(documentServiceResponse.ResponseBody))
            {
                Helpers.SetupJsonReader(jsonReader, documentServiceResponse.serializerSettings);
                string resourceName = DirectExtensions.GetResourceNameForDeserialization(resourceType);
                string propertyName = string.Empty;
                while (jsonReader.Read())
                {
                    // Keep reading until you find the property name
                    if (jsonReader.TokenType == JsonToken.PropertyName)
                    {
                        // Set the propertyName so that we can compare it to the desired resource name later.
                        propertyName = jsonReader.Value.ToString();
                    }

                    // If we found the correct property with the array of items,
                    // then execute the callback on each of them.
                    if (jsonReader.Depth == 1 &&
                        jsonReader.TokenType == JsonToken.StartArray &&
                        string.Equals(propertyName, resourceName, StringComparison.Ordinal))
                    {
                        while (jsonReader.Read() && jsonReader.Depth == 2)
                        {
                            yield return callback(jsonReader);
                        }

                        break;
                    }
                }
            }
        }

        internal static string GetResourceNameForDeserialization(Type resourceType)
        {
            if (typeof(Document).IsAssignableFrom(resourceType))
            {
                return "Documents";
            }
            else if (typeof(Attachment).IsAssignableFrom(resourceType))
            {
                return "Attachments";
            }
            else if (typeof(CosmosDatabaseSettings).IsAssignableFrom(resourceType))
            {
                return "Databases";
            }
            else if (typeof(Offer).IsAssignableFrom(resourceType))
            {
                return "Offers";
            }
            else if (typeof(CosmosContainerSettings).IsAssignableFrom(resourceType))
            {
                return "DocumentCollections";
            }
            else if (typeof(CosmosStoredProcedureSettings).IsAssignableFrom(resourceType))
            {
                return "StoredProcedures";
            }
            else if (typeof(CosmosTriggerSettings).IsAssignableFrom(resourceType))
            {
                return "Triggers";
            }
            else if (typeof(CosmosUserDefinedFunctionSettings).IsAssignableFrom(resourceType))
            {
                return "UserDefinedFunctions";
            }

            return resourceType.Name + "s";
        }

        private static ITypeResolver<TResource> GetTypeResolver<TResource>() where TResource : JsonSerializable, new()
        {
            ITypeResolver<TResource> typeResolver = null;
            if (typeof(TResource) == typeof(Offer))
            {
                typeResolver = (ITypeResolver<TResource>)(OfferTypeResolver.ResponseOfferTypeResolver);
            }
            return typeResolver;
        }

        internal static bool IsPublicResource(Type resourceType)
        {
            if (resourceType == typeof(CosmosDatabaseSettings) ||
                resourceType == typeof(CosmosContainerSettings) ||
                resourceType == typeof(CosmosStoredProcedureSettings) ||
                resourceType == typeof(CosmosUserDefinedFunctionSettings) ||
                resourceType == typeof(CosmosTriggerSettings) ||
                resourceType == typeof(Conflict) ||
                typeof(Attachment).IsAssignableFrom(resourceType) ||
                resourceType == typeof(User) ||
                typeof(Permission).IsAssignableFrom(resourceType) ||
                typeof(Document).IsAssignableFrom(resourceType) ||
                resourceType == typeof(Offer) ||
                resourceType == typeof(Schema))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GeneratePathForNameBased(Type resourceType, string resourceOwnerFullName, string resourceName)
        {
            if (resourceName == null)
                return null;

            if (resourceType == typeof(CosmosDatabaseSettings))
            {
                return Paths.DatabasesPathSegment + "/" + resourceName;
            }
            else if (resourceOwnerFullName == null)
            {
                return null;
            }
            else if (resourceType == typeof(CosmosContainerSettings))
            {
                return resourceOwnerFullName + "/" + Paths.CollectionsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(CosmosStoredProcedureSettings))
            {
                return resourceOwnerFullName + "/" + Paths.StoredProceduresPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(CosmosUserDefinedFunctionSettings))
            {
                return resourceOwnerFullName + "/" + Paths.UserDefinedFunctionsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(CosmosTriggerSettings))
            {
                return resourceOwnerFullName + "/" + Paths.TriggersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Conflict))
            {
                return resourceOwnerFullName + "/" + Paths.ConflictsPathSegment + "/" + resourceName;
            }
            else if (typeof(Attachment).IsAssignableFrom(resourceType))
            {
                return resourceOwnerFullName + "/" + Paths.AttachmentsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(User))
            {
                return resourceOwnerFullName + "/" + Paths.UsersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(UserDefinedType))
            {
                return resourceOwnerFullName + "/" + Paths.UserDefinedTypesPathSegment + "/" + resourceName;
            }
            else if (typeof(Permission).IsAssignableFrom(resourceType))
            {
                return resourceOwnerFullName + "/" + Paths.PermissionsPathSegment + "/" + resourceName;
            }
            else if (typeof(Document).IsAssignableFrom(resourceType))
            {
                return resourceOwnerFullName + "/" + Paths.DocumentsPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Offer))
            {
                return Paths.OffersPathSegment + "/" + resourceName;
            }
            else if (resourceType == typeof(Schema))
            {
                return resourceOwnerFullName + "/" + Paths.SchemasPathSegment + "/" + resourceName;
            }
            else if (typeof(CosmosResource).IsAssignableFrom(resourceType))
            {
                // just generic Resource type.
                return null;
            }

            string errorMessage = string.Format(CultureInfo.CurrentUICulture, RMResources.UnknownResourceType, resourceType.ToString());
            Debug.Assert(false, errorMessage);
            throw new BadRequestException(errorMessage);
        }

        internal static Collection<TSerializable> GetObjectCollection<TSerializable>(this JsonSerializable jsonSerializable, string propertyName, Type resourceType = null, string ownerName = null, ITypeResolver<TSerializable> typeResolver = null) where TSerializable : JsonSerializable, new()
        {
            if (jsonSerializable.propertyBag != null)
            {
                JToken token = jsonSerializable.propertyBag[propertyName];

                if (typeResolver == null)
                {
                    typeResolver = DirectExtensions.GetTypeResolver<TSerializable>();
                }

                if (token != null)
                {
                    Collection<JObject> jobjectCollection = token.ToObject<Collection<JObject>>();
                    Collection<TSerializable> objectCollection = new Collection<TSerializable>();
                    foreach (JObject childObject in jobjectCollection)
                    {
                        TSerializable result = typeResolver != null ? typeResolver.Resolve(childObject) : new TSerializable();
                        result.propertyBag = childObject;

                        // for public resource, let's add the Altlink
                        if (DirectExtensions.IsPublicResource(typeof(TSerializable)))
                        {
                            CosmosResource resource = result as CosmosResource;
                            resource.AltLink = DirectExtensions.GeneratePathForNameBased(resourceType, ownerName, resource.Id);
                        }
                        objectCollection.Add(result);
                    }
                    return objectCollection;
                }
            }
            return null;
        }
    }
}
