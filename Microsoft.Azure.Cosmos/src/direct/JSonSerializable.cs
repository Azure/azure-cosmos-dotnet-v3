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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the base class for Azure Cosmos DB database objects and provides methods for serializing and deserializing from JSON.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    abstract class JsonSerializable //We introduce this type so that we dont have to expose the Newtonsoft type publically.
    {
        internal JObject propertyBag;

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
            this.propertyBag = new JObject();
        }

        internal JsonSerializerSettings SerializerSettings { get; set; }

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
        /// <param name="settings">The serializer settings to use.</param>
        public void SaveTo(Stream stream, SerializationFormattingPolicy formattingPolicy, JsonSerializerSettings settings)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            this.SerializerSettings = settings;
            JsonSerializer serializer = settings == null ? new JsonSerializer() : JsonSerializer.Create(settings);
            JsonTextWriter jsonWriter = new JsonTextWriter(new StreamWriter(stream));
            this.SaveTo(jsonWriter, serializer, formattingPolicy);
        }

        internal void SaveTo(JsonWriter writer, JsonSerializer serializer, SerializationFormattingPolicy formattingPolicy = SerializationFormattingPolicy.None)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }

            if (formattingPolicy == SerializationFormattingPolicy.Indented)
            {
                writer.Formatting = Newtonsoft.Json.Formatting.Indented;
            }
            else
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
            }

            this.OnSave();

            if ((typeof(Document).IsAssignableFrom(this.GetType()) && !this.GetType().Equals(typeof(Document)))
                || (typeof(Attachment).IsAssignableFrom(this.GetType()) && !this.GetType().Equals(typeof(Attachment))))
            {
                serializer.Serialize(writer, this);
            }
            else
            {
                if (JsonSerializable.JustPocoSerialization)
                {
                    this.propertyBag.WriteTo(writer);
                }
                else
                {
                    serializer.Serialize(writer, this.propertyBag);
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

            this.SaveTo(new JsonTextWriter(new StringWriter(stringBuilder, CultureInfo.CurrentCulture)), new JsonSerializer(), formattingPolicy);
        }


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

        /// <summary>
        /// Loads the object from the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="settings">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFrom<T>(Stream stream, ITypeResolver<T> typeResolver, JsonSerializerSettings settings = null) where T : JsonSerializable, new()
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            JsonTextReader jsonReader = new JsonTextReader(new StreamReader(stream));

            return JsonSerializable.LoadFrom<T>(jsonReader, typeResolver, settings);
        }

        /// <summary>
        /// Loads the object from the specified stream using a Resolver. This method does not use a default constructor of the underlying type,
        /// and instead loads the resource from the provided resolver, which is why this method does not impose the new() constraint to T and allows
        /// the creation of an abstract class.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="stream">The stream to load from.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="settings">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFromWithResolver<T>(Stream stream, ITypeResolver<T> typeResolver, JsonSerializerSettings settings = null) where T : JsonSerializable
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (typeResolver == null)
            {
                throw new ArgumentNullException(nameof(typeResolver));
            }

            JsonTextReader jsonReader = new JsonTextReader(new StreamReader(stream));
            return JsonSerializable.LoadFromWithResolver(typeResolver, settings, jsonReader);
        }

        /// <summary>
        /// Loads the object from the specified stream using a Resolver. This method does not use a default constructor of the underlying type,
        /// and instead loads the resource from the provided resolver, which is why this method does not impose the new() constraint to T and allows
        /// the creation of an abstract class.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="serialized">Serialized payload.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="settings">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFromWithResolver<T>(string serialized, ITypeResolver<T> typeResolver, JsonSerializerSettings settings = null) where T : JsonSerializable
        {
            if (serialized == null)
            {
                throw new ArgumentNullException(nameof(serialized));
            }

            if (typeResolver == null)
            {
                throw new ArgumentNullException(nameof(typeResolver));
            }

            JsonTextReader jsonReader = new JsonTextReader(new StringReader(serialized));
            return JsonSerializable.LoadFromWithResolver(typeResolver, settings, jsonReader);
        }

        /// <summary>
        /// Loads the object from the specified stream.
        /// </summary>
        /// <typeparam name="T">The type of the returning object.</typeparam>
        /// <param name="serialized">Serialized payload.</param>
        /// <param name="typeResolver">Used to get a correct object from a stream.</param>
        /// <param name="settings">The JsonSerializerSettings to be used</param>
        /// <returns>The object loaded from the specified stream.</returns>
        internal static T LoadFrom<T>(string serialized, ITypeResolver<T> typeResolver, JsonSerializerSettings settings = null) where T : JsonSerializable, new()
        {
            if (serialized == null)
            {
                throw new ArgumentNullException("serialized");
            }

            JsonTextReader jsonReader = new JsonTextReader(new StringReader(serialized));

            return JsonSerializable.LoadFrom<T>(jsonReader, typeResolver, settings);
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
        public static T LoadFromWithConstructor<T>(Stream stream, Func<T> constructorFunction, JsonSerializerSettings settings)
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

            JsonTextReader jsonReader = new JsonTextReader(new StreamReader(stream));
            ((JsonSerializable)(object)resource).LoadFrom(jsonReader, settings);
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
            if (this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];
                if (token != null)
                {
                    // Enum Backward compatibility support. This code will not be
                    // needed with newer Json.Net version which handles enums correctly.
                    if (typeof(T).IsEnum())
                    {
                        if (token.Type == JTokenType.String)
                        {
                            return token.ToObject<T>(JsonSerializer.CreateDefault());
                        }
                    }
                    return this.SerializerSettings == null ?
                        token.ToObject<T>() :
                        token.ToObject<T>(JsonSerializer.Create(this.SerializerSettings));
                }
            }
            return default(T);
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
                JToken token = this.propertyBag[propertyName];
                if (token != null)
                {
                    // Enum Backward compatibility support. This code will not be
                    // needed with newer Json.Net version which handles enums correctly.
                    if (typeof(T).IsEnum())
                    {
                        if (token.Type == JTokenType.String)
                        {
                            return token.ToObject<T>(JsonSerializer.CreateDefault());
                        }
                    }
                    return this.SerializerSettings == null ?
                        token.ToObject<T>() :
                        token.ToObject<T>(JsonSerializer.Create(this.SerializerSettings));
                }
            }
            return defaultValue;
        }

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
                JToken token = this.propertyBag[fieldNames[0]];
                for (int i = 1; i < fieldNames.Length; ++i)
                {
                    if (token == null)
                    {
                        break;
                    }

                    if (!(token is JObject))
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
                    // Enum Backward compatibility support. This code will not be
                    // needed with newer Json.Net version which handles enums correctly.
                    if (typeof(T).IsEnum())
                    {
                        if (token.Type == JTokenType.String)
                        {
                            return token.ToObject<T>(JsonSerializer.CreateDefault());
                        }
                    }

                    return this.SerializerSettings == null ?
                        token.ToObject<T>() :
                        token.ToObject<T>(JsonSerializer.Create(this.SerializerSettings));
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
                this.propertyBag = new JObject();
            }

            if (value != null)
            {
                this.propertyBag[name] = JToken.FromObject(value);
            }
            else
            {
                this.propertyBag.Remove(name);
            }
        }

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

        internal Collection<T> GetValueCollection<T>(string propertyName)
        {
            if (this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];
                if (token != null)
                {
                    Collection<JToken> jTokenCollection = token.ToObject<Collection<JToken>>();
                    Collection<T> valueCollection = new Collection<T>();
                    foreach (JToken childToken in jTokenCollection)
                    {
                        if (childToken != null)
                        {
                            if (typeof(T).IsEnum())
                            {
                                if (childToken.Type == JTokenType.String)
                                {
                                    valueCollection.Add(childToken.ToObject<T>(JsonSerializer.CreateDefault()));
                                    continue;
                                }
                            }
                            valueCollection.Add(
                                this.SerializerSettings == null ?
                                    childToken.ToObject<T>() :
                                    childToken.ToObject<T>(JsonSerializer.Create(this.SerializerSettings)));
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
                this.propertyBag = new JObject();
            }

            if (value != null)
            {
                Collection<JToken> jTokenCollection = new Collection<JToken>();
                foreach (T childValue in value)
                {
                    if (childValue != null)
                    {
                        jTokenCollection.Add(JToken.FromObject(childValue));
                    }
                    else
                    {
                        jTokenCollection.Add(null);
                    }
                }
                this.propertyBag[propertyName] = JToken.FromObject(jTokenCollection);
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
                JToken token = this.propertyBag[propertyName];

                if (token != null && (returnEmptyObject || token.HasValues))
                {
                    TSerializable result = new TSerializable();
                    result.propertyBag = JObject.FromObject(token);
                    return result;
                }
            }

            return null;
        }

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
                JToken token = this.propertyBag[propertyName];

                if (token != null && (returnEmptyObject || token.HasValues))
                {
                    if (token is JObject)
                    {
                        return typeResolver.Resolve(token as JObject);
                    }
                    else
                    {
                        throw new ArgumentException($"Cannot resolve property type. The property {propertyName} is not an object, it is a {token.Type}.");
                    }
                }
            }

            return null;
        }

        internal void SetObject<TSerializable>(string propertyName, TSerializable value) where TSerializable : JsonSerializable
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JObject();
            }
            this.propertyBag[propertyName] = value != null ? value.propertyBag : null;
        }

        internal Collection<TSerializable> GetObjectCollection<TSerializable>(string propertyName, Type resourceType = null, string ownerName = null, ITypeResolver<TSerializable> typeResolver = null) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];

                if (typeResolver == null)
                {
                    typeResolver = GetTypeResolver<TSerializable>();
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

        internal Collection<TSerializable> GetObjectCollectionWithResolver<TSerializable>(string propertyName, ITypeResolver<TSerializable> typeResolver) where TSerializable : JsonSerializable
        {
            if (this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];
                
                if (token != null)
                {
                    Collection<JObject> jobjectCollection = token.ToObject<Collection<JObject>>();
                    Collection<TSerializable> objectCollection = new Collection<TSerializable>();
                    foreach (JObject childObject in jobjectCollection)
                    {
                        TSerializable result = typeResolver.Resolve(childObject);
                        result.propertyBag = childObject;
                        
                        objectCollection.Add(result);
                    }
                    return objectCollection;
                }
            }
            return null;
        }

        internal void SetObjectCollection<TSerializable>(string propertyName, Collection<TSerializable> value) where TSerializable : JsonSerializable
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JObject();
            }

            if (value != null)
            {
                Collection<JObject> jobjectCollection = new Collection<JObject>();
                foreach (TSerializable childValue in value)
                {
                    childValue.OnSave();
                    jobjectCollection.Add(childValue.propertyBag ?? new JObject());
                }
                this.propertyBag[propertyName] = JToken.FromObject(jobjectCollection);
            }
        }

        internal Dictionary<string, TSerializable> GetObjectDictionary<TSerializable>(string propertyName, ITypeResolver<TSerializable> typeResolver = null) where TSerializable : JsonSerializable, new()
        {
            if (this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];

                if (typeResolver == null)
                {
                    typeResolver = GetTypeResolver<TSerializable>();
                }

                if (token != null)
                {
                    Dictionary<string, JObject> jobjectDictionary = token.ToObject<Dictionary<string, JObject>>();
                    Dictionary<string, TSerializable> objectDictionary = new Dictionary<string, TSerializable>();
                    foreach (KeyValuePair<string, JObject> kvp in jobjectDictionary)
                    {
                        TSerializable result = typeResolver != null ? typeResolver.Resolve(kvp.Value) : new TSerializable();
                        result.propertyBag = kvp.Value;

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
                this.propertyBag = new JObject();
            }

            if (value != null)
            {
                Dictionary<string, JObject> jobjectDictionary = new Dictionary<string, JObject>();
                foreach (KeyValuePair<string, TSerializable> kvp in value)
                {
                    kvp.Value.OnSave();
                    jobjectDictionary.Add(kvp.Key, kvp.Value.propertyBag ?? new JObject());
                }
                this.propertyBag[propertyName] = JToken.FromObject(jobjectDictionary);
            }
        }

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

        private static T LoadFrom<T>(JsonTextReader jsonReader, ITypeResolver<T> typeResolver, JsonSerializerSettings settings = null) where T : JsonSerializable, new()
        {
            T resource = new T();

            resource.LoadFrom(jsonReader, settings);
            resource = (typeResolver != null) ? typeResolver.Resolve(resource.propertyBag) : resource;
            return resource;
        }

        private static T LoadFromWithResolver<T>(ITypeResolver<T> typeResolver, JsonSerializerSettings settings, JsonTextReader jsonReader) where T : JsonSerializable
        {
            Helpers.SetupJsonReader(jsonReader, settings);
            JObject jObject = JObject.Load(jsonReader);
            return typeResolver.Resolve(jObject);
        }
    }
}