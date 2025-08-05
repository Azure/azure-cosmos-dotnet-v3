//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Cosmos.Linq;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Documents.Rntbd.TransportClient;

    /// <summary>
    /// Represents the base class for Azure Cosmos DB database objects and provides methods for serializing and deserializing from JSON.
    /// </summary>
#if COSMOSCLIENT && !COSMOS_GW_AOT
    internal
#else
    public
#endif
    abstract class JsonSerializable //We introduce this type so that we dont have to expose the Newtonsoft type publically.
    {
        internal JsonObject propertyBag;

        private const string POCOSerializationOnly = "POCOSerializationOnly";

        internal static bool JustPocoSerialization;

        static JsonSerializable()
        {
            int result;

            if (int.TryParse(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(POCOSerializationOnly)) ? "0" : Environment.GetEnvironmentVariable(POCOSerializationOnly), out result) && result == 1)
            {
                JustPocoSerialization = true;
            }
            else
            {
                JustPocoSerialization = false;
            }
        }

        internal JsonSerializable() //Prevent external derivation.
        {
            this.propertyBag = new JsonObject();
        }

        internal JsonSerializerOptions SerializerSettings { get; set; }

        //Public Serialization Helpers.
        /// <summary>
        /// Saves the object to the specified stream in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="stream">Saves the object to this output stream.</param>
        /// <param name="formattingPolicy">Uses an optional serialization formatting policy when saving the object. The default policy is set to None.</param>
        public void SaveTo(Stream stream, SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            this.SaveTo(stream, formattingPolicy, null);
        }

        /// <summary>
        /// Saves the object to the specified stream in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="stream">Saves the object to this output stream.</param>
        /// <param name="formattingPolicy">Uses a custom serialization formatting policy when saving the object.</param>
        /// <param name="options">The serializer settings to use.</param>
        public void SaveTo(Stream stream, SerializationFormattingPolicy formattingPolicy, JsonSerializerOptions options)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            this.SerializerSettings = options;
            using var writer = new Utf8JsonWriter(stream);
            this.SaveTo(writer, formattingPolicy);
        }

        internal void SaveTo(Utf8JsonWriter writer, SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            this.OnSave();

            if ((typeof(Document).IsAssignableFrom(this.GetType()) && !this.GetType().Equals(typeof(Document)))
                || (typeof(Attachment).IsAssignableFrom(this.GetType()) && !this.GetType().Equals(typeof(Attachment))))
            {
                JsonSerializer.Serialize(writer, this);
            }
            else
            {
                if (JsonSerializable.JustPocoSerialization)
                {
                    this.propertyBag.WriteTo(writer);
                }
                else
                {
                    JsonSerializer.Serialize(writer, this.propertyBag);
                }
            }
            writer.Flush();
        }

        /// <summary>
        /// Saves the object to the specified string builder
        /// </summary>
        /// <param name="stringBuilder">Saves the object to this output string builder.</param>
        /// <param name="formattingPolicy">Uses an optional serialization formatting policy when saving the object. The default policy is set to None.</param>
        internal void SaveTo(StringBuilder stringBuilder, SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            if (stringBuilder == null)
            {
                throw new ArgumentNullException("stringBuilder");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
            using MemoryStream stream = new MemoryStream(bytes);
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    this.SaveTo(writer, formattingPolicy);
                }
            }
        }

#if !COSMOS_GW_AOT
        /// <summary>
        /// Loads the object from the specified JSON reader in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="reader">Loads the object from this JSON reader.</param>
        public virtual void LoadFrom(JsonReader reader)
        {
            // calling LoadFrom(reader, null) will not work here since this is a virtual method
            // and hence that will instead call the overriden LoadFrom method in the derived class
            // instead of the below overload of this class
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            this.propertyBag = JObject.Load(reader);
        }

        /// <summary>
        /// Loads the object from the specified JSON reader in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="reader">Loads the object from this JSON reader.</param>
        /// <param name="serializerSettings">The JsonSerializerSettings to be used.</param>
        public virtual void LoadFrom(JsonReader reader, JsonSerializerSettings serializerSettings)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            Helpers.SetupJsonReader(reader, serializerSettings);

            this.propertyBag = JObject.Load(reader);
            this.SerializerSettings = serializerSettings;
        }

        /// <summary>
        /// Loads the object from the specified stream in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The object loaded from the specified stream.</returns>
        public static T LoadFrom<T>(Stream stream) where T : JsonSerializable, new()
        {
            return LoadFrom<T>(stream, null);
        }
#endif

        /// <summary>
        /// Loads the object from the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="options">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFrom<T>(Stream stream, ITypeResolver<T> typeResolver, JsonSerializerOptions options = null) where T : JsonSerializable, new()
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            var root = JsonNode.Parse(stream) as JsonObject;
            T resource = new T();
            resource.propertyBag = root;
            resource.SerializerSettings = options;
            if (typeResolver != null)
            {
                resource = typeResolver.Resolve(resource.propertyBag);
            }
            return resource;
        }

        /// <summary>
        /// Loads the object from the specified stream using a Resolver. This method does not use a default constructor of the underlying type,
        /// and instead loads the resource from the provided resolver, which is why this method does not impose the new() constraint to T and allows
        /// the creation of an abstract class.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="options">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFromWithResolver<T>(Stream stream, ITypeResolver<T> typeResolver, JsonSerializerOptions options = null) where T : JsonSerializable
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (typeResolver == null)
            {
                throw new ArgumentNullException(nameof(typeResolver));
            }

            var root = JsonNode.Parse(stream) as JsonObject;
            return typeResolver.Resolve(root);
        }

        /// <summary>
        /// Loads the object from the specified stream using a Resolver. This method does not use a default constructor of the underlying type,
        /// and instead loads the resource from the provided resolver, which is why this method does not impose the new() constraint to T and allows
        /// the creation of an abstract class.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="serialized">Serialized payload.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="options">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFromWithResolver<T>(string serialized, ITypeResolver<T> typeResolver, JsonSerializerOptions options = null) where T : JsonSerializable
        {
            if (serialized == null)
            {
                throw new ArgumentNullException(nameof(serialized));
            }

            if (typeResolver == null)
            {
                throw new ArgumentNullException(nameof(typeResolver));
            }

            var root = JsonNode.Parse(serialized) as JsonObject;
            return typeResolver.Resolve(root);
        }

        /// <summary>
        /// Loads the object from the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="serialized">Serialized payload.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="options">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFrom<T>(string serialized, ITypeResolver<T> typeResolver, JsonSerializerOptions options = null) where T : JsonSerializable, new()
        {
            if (serialized == null)
            {
                throw new ArgumentNullException("serialized");
            }

            var root = JsonNode.Parse(serialized) as JsonObject;
            T resource = new T();
            resource.propertyBag = root;
            resource.SerializerSettings = options;
            if (typeResolver != null)
            {
                resource = typeResolver.Resolve(resource.propertyBag);
            }
            return resource;
        }

        /// <summary>
        /// Deserializes the specified stream using the given constructor in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="constructorFunction">The constructor used for the returning object.</param>
        /// <returns>The object loaded from the specified stream.</returns>
        public static T LoadFromWithConstructor<T>(Stream stream, Func<T> constructorFunction)
        {
            return LoadFromWithConstructor<T>(stream, constructorFunction, null);
        }

        /// <summary>
        /// Deserializes the specified stream using the given constructor in the Azure Cosmos DB service.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="constructorFunction">The constructor used for the returning object.</param>
        /// <param name="settings">The JsonSerializerSettings to be used.</param>
        /// <returns>The object loaded from the specified stream.</returns>
        public static T LoadFromWithConstructor<T>(Stream stream, Func<T> constructorFunction, JsonSerializerOptions settings)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (!typeof(T).IsSubclassOf(typeof(JsonSerializable)))
            {
                throw new ArgumentException("type is not serializable");
            }

            T resource = constructorFunction();
            JsonSerializable tOfSerializable = resource as JsonSerializable;
            if (tOfSerializable == null)
            {
                throw new NotSupportedException();
            }

            using var doc = JsonDocument.Parse(stream);
            var root = JsonNode.Parse(doc.RootElement.GetRawText()) as JsonObject;
            tOfSerializable.propertyBag = root;

            return resource;
        }

        /// <summary>
        /// Returns the string representation of the object in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>The string representation of the object.</returns>
        public override string ToString()
        {
            this.OnSave();
            return this.propertyBag.ToString();
        }

        internal virtual void Validate()
        {
        }

        #region Protected Signals
        /// <summary>
        /// Get the value associated with the specified property name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        internal T GetValue<T>(string propertyName)
        {
            return this.GetValue(propertyName, default(T));
        }

        /// <summary>
        /// Get the value associated with the specified property name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal T GetValue<T>(string propertyName, T defaultValue)
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                return this.GetValue(token, defaultValue);
            }

            return defaultValue;
        }

        internal T GetValue<T>(JsonNode token, T defaultValue)
        {
            if (token != null)
            {
                // Enum Backward compatibility support. This code will not be
                // needed with newer Json.Net version which handles enums correctly.
                if (typeof(T).IsEnum && token is JsonValue valueNode && valueNode.TryGetValue<string>(out var strValue))
                {
                    if (Enum.TryParse(typeof(T), strValue, true, out var enumValue))
                    {
                        return (T)enumValue;
                    }
                }

                if (this.SerializerSettings != null)
                {
                    JsonTypeInfo<T> typeInfo = this.SerializerSettings.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
                    if (typeInfo != null)
                    {
                        return token.Deserialize(typeInfo);
                    }
                }
            }

            return default(T);
        }


#if !COSMOS_GW_AOT
        /// <summary>
        /// Get the enum value associated with the specified property name.
        /// </summary>
        /// <typeparam name="TEnum"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        internal TEnum? GetEnumValue<TEnum>(string propertyName) where TEnum : struct
        {
            if (!typeof(TEnum).IsEnum())
            {
                throw new ArgumentException($"{typeof(TEnum)} is not an Enum.");
            }

            string valueString = this.GetValue<string>(propertyName);

            if (string.IsNullOrWhiteSpace(valueString))
            {
                return null;
            }

            if (!Enum.TryParse<TEnum>(valueString, ignoreCase: true, out TEnum value)
                || !Enum.IsDefined(typeof(TEnum), value))
            {
                throw new BadRequestException($"Could not parse [{valueString}] as a valid enum value for property [{propertyName}].");
            }

            return value;
        }
#endif
        /// <summary>
        /// Get the value associated with the specified property name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fieldNames">Field names which compose a path to the property to be retrieved.</param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal T GetValueByPath<T>(string[] fieldNames, T defaultValue)
        {
            if (fieldNames == null)
            {
                throw new ArgumentNullException("fieldNames");
            }

            if (fieldNames.Length == 0)
            {
                throw new ArgumentException("fieldNames is empty.");
            }

            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[fieldNames[0]];
                for (int i = 1; i < fieldNames.Length; ++i)
                {
                    if (token == null)
                    {
                        break;
                    }

                    if (!(token is JsonObject))
                    {
                        token = null;
                    }
                    else
                    {
                        token = token[fieldNames[i]];
                    }
                }

                if (token != null)
                {
                    return this.GetValue(token, defaultValue);
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Set the value associated with the specified name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        internal void SetValue(string name, object value)
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }

            if (value != null)
            {
                this.propertyBag[name] = JsonSerializer.SerializeToNode(value, this.SerializerSettings.GetTypeInfo(value.GetType()));
            }
            else
            {
                this.propertyBag.Remove(name);
            }
        }

#if !COSMOS_GW_AOT
        /// <summary>
        /// Set the value associated with the specified property name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fieldNames">Field names which compose a path to the property to be retrieved.</param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <remarks>This will overwrite the existing properties</remarks>
        internal void SetValueByPath<T>(string[] fieldNames, T value)
        {
            if (fieldNames == null)
            {
                throw new ArgumentNullException("fieldNames");
            }

            if (fieldNames.Length == 0)
            {
                throw new ArgumentException("fieldNames is empty.");
            }

            if (this.propertyBag == null)
            {
                this.propertyBag = new JObject();
            }

            JToken token = this.propertyBag;
            for (int i = 0; i < fieldNames.Length - 1; ++i)
            {
                if (token[fieldNames[i]] == null)
                {
                    token[fieldNames[i]] = new JObject();
                }

                token = token[fieldNames[i]];
            }

            JObject tokenAsJObject = token as JObject;
            if (value == null && tokenAsJObject != null)
            {
                tokenAsJObject.Remove(fieldNames[fieldNames.Length - 1]);
            }
            else
            {
                token[fieldNames[fieldNames.Length - 1]] = value == null ? null : JToken.FromObject(value);
            }
        }
#endif

        internal Collection<T> GetValueCollection<T>(string propertyName)
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                if (token is JsonArray array)
                {
                    Collection<T> valueCollection = new Collection<T>();
                    foreach (JsonNode childToken in array)
                    {
                        if (childToken != null)
                        {
                            if (typeof(T).IsEnum && childToken is JsonValue val && val.TryGetValue<string>(out var strValue))
                            {
                                if (Enum.TryParse(typeof(T), strValue, true, out var enumValue))
                                {
                                    valueCollection.Add((T)enumValue);
                                    continue;
                                }
                            }
                            valueCollection.Add(this.SerializerSettings == null
                                ? childToken.Deserialize<T>()
                                : childToken.Deserialize<T>(this.SerializerSettings));
                        }
                        else
                        {
                            valueCollection.Add(default(T));
                        }
                    }
                    return valueCollection;
                }
            }
            return null;
        }

        internal void SetValueCollection<T>(string propertyName, Collection<T> value)
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }

            if (value != null)
            {
                var array = new JsonArray();
                foreach (T childValue in value)
                {
                    array.Add(childValue != null
                        ? JsonSerializer.SerializeToNode(childValue, typeof(T), this.SerializerSettings)
                        : null);
                }
                this.propertyBag[propertyName] = array;
            }
            else
            {
                this.propertyBag.Remove(propertyName);
            }
        }

        /// <summary>
        /// Gets a deserialized child object with the given property name.
        /// </summary>
        /// <typeparam name="TSerializable">The type of the child object, which must be a <see cref="JsonSerializable"/>.</typeparam>
        /// <param name="propertyName">The child property name.</param>
        /// <param name="returnEmptyObject">
        /// Determines how an empty child object (i.e. '{}') will be handled. When this is true, an empty JObject will be used to initialize the child TSerializable.
        /// When this if false, an empty JOBject will be treated as null, causing null to be returned.
        /// </param>
        /// <returns>The deserialized child object.</returns>
        internal TSerializable GetObject<TSerializable>(string propertyName, bool returnEmptyObject = false) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                if (token is JsonObject obj && (returnEmptyObject || obj.Count > 0))
                {
                    TSerializable result = new TSerializable();
                    result.propertyBag = obj;
                    return result;
                }
            }
            return null;
        }

#if !COSMOS_GW_AOT
        /// <summary>
        /// Gets a deserialized child object with the given property name.
        /// </summary>
        /// <typeparam name="TSerializable">The type of the child object, which must be a <see cref="JsonSerializable"/>.</typeparam>
        /// <param name="propertyName">The child property name.</param>
        /// <param name="typeResolver">The type resolver for polymorphic types.</param>
        /// <param name="returnEmptyObject">
        /// Determines how an empty child object (i.e. '{}') will be handled. When this is true, an empty JObject will be used to initialize the child TSerializable.
        /// When this if false, an empty JOBject will be treated as null, causing null to be returned.
        /// </param>
        /// <returns>The deserialized child object.</returns>
        internal TSerializable GetObjectWithResolver<TSerializable>(
            string propertyName,
            ITypeResolver<TSerializable> typeResolver,
            bool returnEmptyObject = false) where TSerializable : JsonSerializable
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                if (token is JsonObject obj && (returnEmptyObject || obj.Count > 0))
                {
                    return typeResolver.Resolve(obj);
                }
                else if (token != null)
                {
                    throw new ArgumentException($"Cannot resolve property type. The property {propertyName} is not an object.");
                }
            }
            return null;
        }
#endif

        internal void SetObject<TSerializable>(string propertyName, TSerializable value) where TSerializable : JsonSerializable
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }
            this.propertyBag[propertyName] = value != null ? value.propertyBag : null;
        }

        internal Collection<TSerializable> GetObjectCollection<TSerializable>(string propertyName, Type resourceType = null, string ownerName = null, ITypeResolver<TSerializable> typeResolver = null) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                if (typeResolver == null)
                {
                    typeResolver = GetTypeResolver<TSerializable>();
                }
                if (token is JsonArray array)
                {
                    Collection<TSerializable> objectCollection = new Collection<TSerializable>();
                    foreach (JsonNode childNode in array)
                    {
                        if (childNode is not JsonObject childObject)
                        {
                            continue;
                        }
                        TSerializable result = typeResolver != null ? typeResolver.Resolve(childObject) : new TSerializable();
                        result.propertyBag = childObject;
                        if (PathsHelper.IsPublicResource(typeof(TSerializable)))
                        {
                            Resource resource = result as Resource;
                            resource.AltLink = PathsHelper.GeneratePathForNameBased(resourceType, ownerName, resource.Id);
                        }
                        objectCollection.Add(result);
                    }
                    return objectCollection;
                }
            }
            return null;
        }

#if !COSMOS_GW_AOT
        internal Collection<TSerializable> GetObjectCollectionWithResolver<TSerializable>(string propertyName, ITypeResolver<TSerializable> typeResolver) where TSerializable : JsonSerializable
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                if (token is JsonArray array)
                {
                    Collection<TSerializable> objectCollection = new Collection<TSerializable>();
                    foreach (JsonNode childNode in array)
                    {
                        if (childNode is not JsonObject childObject)
                        {
                            continue;
                        }
                        TSerializable result = typeResolver.Resolve(childObject);
                        result.propertyBag = childObject;
                        objectCollection.Add(result);
                    }
                    return objectCollection;
                }
            }
            return null;
        }
#endif

        internal void SetObjectCollection<TSerializable>(string propertyName, Collection<TSerializable> value) where TSerializable : JsonSerializable
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }

            if (value != null)
            {
                var array = new JsonArray();
                foreach (TSerializable childValue in value)
                {
                    childValue.OnSave();
                    array.Add(childValue.propertyBag ?? new JsonObject());
                }
                this.propertyBag[propertyName] = array;
            }
        }

#if !COSMOS_GW_AOT
        internal Dictionary<string, TSerializable> GetObjectDictionary<TSerializable>(
            string propertyName,
            ITypeResolver<TSerializable> typeResolver = null,
            IEqualityComparer<string> comparer = null) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];
                if (typeResolver == null)
                {
                    typeResolver = GetTypeResolver<TSerializable>();
                }
                if (token is JsonObject obj)
                {
                    var objectDictionary = comparer != null
                        ? new Dictionary<string, TSerializable>(comparer)
                        : new Dictionary<string, TSerializable>();
                    foreach (var kvp in obj)
                    {
                        if (kvp.Value is not JsonObject childObj)
                        {
                            continue;
                        }
                        TSerializable result = typeResolver != null ? typeResolver.Resolve(childObj) : new TSerializable();
                        result.propertyBag = childObj;
                        objectDictionary.Add(kvp.Key, result);
                    }
                    return objectDictionary;
                }
            }
            return null;
        }

        internal Dictionary<string, TSerializable> GetObjectDictionaryWithNullableValues<TSerializable>(string propertyName) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag != null)
            {
                JsonNode token = this.propertyBag[propertyName];

                if (token != null)
                {
                    Dictionary<string, JsonObject> jobjectDictionary = token.ToObject<Dictionary<string, JsonObject>>();
                    if (jobjectDictionary == null)
                    {
                        return null;
                    } 

                    Dictionary<string, TSerializable> objectDictionary = new Dictionary<string, TSerializable>();
                    foreach (KeyValuePair<string, JsonObject> kvp in jobjectDictionary)
                    {
                        TSerializable result;
                        if (kvp.Value == null)
                        {
                            result = null;
                        }
                        else
                        {
                            result = new TSerializable();
                            result.propertyBag = kvp.Value;
                        }

                        objectDictionary.Add(kvp.Key, result);
                    }

                    return objectDictionary;
                }
            }
            return null;
        }

        internal void SetObjectDictionary<TSerializable>(string propertyName, Dictionary<string, TSerializable> value) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }

            if (value != null)
            {
                Dictionary<string, JsonObject> jobjectDictionary = new Dictionary<string, JsonObject>();
                foreach (KeyValuePair<string, TSerializable> kvp in value)
                {
                    kvp.Value.OnSave();
                    jobjectDictionary.Add(kvp.Key, kvp.Value.propertyBag ?? new JsonObject());
                }
                this.propertyBag[propertyName] = JsonNode.FromObject(jobjectDictionary);
            }
        }

        internal void SetObjectDictionaryWithNullableValues<TSerializable>(string propertyName, Dictionary<string, TSerializable> value) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }

            if (value != null)
            {
                Dictionary<string, JsonObject> jobjectDictionary = new Dictionary<string, JsonObject>();
                foreach (KeyValuePair<string, TSerializable> kvp in value)
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.OnSave();
                        jobjectDictionary.Add(kvp.Key, kvp.Value.propertyBag ?? new JsonObject());
                    }
                    else
                    {
                        jobjectDictionary.Add(kvp.Key, null);
                    }
                }
                this.propertyBag[propertyName] = JsonNode.FromObject(jobjectDictionary);
            }
        }
#endif

        internal virtual void OnSave()
        {

        }

        internal static ITypeResolver<TResource> GetTypeResolver<TResource>() where TResource : JsonSerializable, new()
        {
            ITypeResolver<TResource> typeResolver = null;
            if (typeof(TResource) == typeof(Offer))
            {
                typeResolver = (ITypeResolver<TResource>)(OfferTypeResolver.ResponseOfferTypeResolver);
            }

            return typeResolver;
        }
#endregion
    }
}