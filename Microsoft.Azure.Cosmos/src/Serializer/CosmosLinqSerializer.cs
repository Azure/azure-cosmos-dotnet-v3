//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Reflection;

    /// <summary>
    /// This abstract class can be implemented to allow a custom serializer (Non [Json.NET serializer](https://www.newtonsoft.com/json/help/html/Introduction.htm)'s) 
    /// to be used by the CosmosClient for LINQ queries.
    /// </summary>
    /// <example>
    /// This example implements the CosmosLinqSerializer contract.
    /// This example custom serializer will honor System.Text.Json attributes.
    /// <code language="c#">
    /// <![CDATA[
    /// class SystemTextJsonSerializer : CosmosLinqSerializer
    /// {
    ///    private readonly JsonObjectSerializer systemTextJsonSerializer;
    ///
    ///    public SystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
    ///    {
    ///        this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
    ///    }
    ///
    ///    public override T FromStream<T>(Stream stream)
    ///    {
    ///        if (stream == null)
    ///            throw new ArgumentNullException(nameof(stream));
    ///
    ///        using (stream)
    ///        {
    ///            if (stream.CanSeek && stream.Length == 0)
    ///            {
    ///                return default;
    ///            }
    ///
    ///            if (typeof(Stream).IsAssignableFrom(typeof(T)))
    ///            {
    ///                return (T)(object)stream;
    ///            }
    ///
    ///            return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
    ///        }
    ///    }
    ///
    ///    public override Stream ToStream<T>(T input)
    ///    {
    ///        MemoryStream streamPayload = new MemoryStream();
    ///        this.systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
    ///        streamPayload.Position = 0;
    ///        return streamPayload;
    ///    }
    ///
    ///    public override string SerializeMemberName(MemberInfo memberInfo)
    ///    {
    ///        JsonPropertyNameAttribute jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);
    ///
    ///        string memberName = !string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name)
    ///            ? jsonPropertyNameAttribute.Name
    ///            : memberInfo.Name;
    ///
    ///        return memberName;
    ///    }
    /// }
    /// ]]>
    /// </code>
    /// </example>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class CosmosLinqSerializer : CosmosSerializer
    {
        /// <summary>
        /// Convert a MemberInfo to a string for use in LINQ query translation.
        /// This must be implemented when using a custom serializer for LINQ queries.
        /// </summary>
        /// <param name="memberInfo">Any MemberInfo used in the query.</param>
        /// <returns>A serialized representation of the member.</returns>
        public abstract string SerializeMemberName(MemberInfo memberInfo);
    }
}
