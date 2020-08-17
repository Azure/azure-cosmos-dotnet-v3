// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Default Serializer
    /// </summary>
    public class SerializerDefaultMappings
    {
        /// <summary>
        /// Maps DataTypes to Sql Datatype Serializer.
        /// </summary>
        public static readonly Dictionary<Type, ISqlSerializer> SqlSerializerByType = new Dictionary<Type, ISqlSerializer>()
        {
            [typeof(bool)] = new SqlBitSerializer(),
            [typeof(byte)] = new SqlTinyintSerializer(),
            [typeof(byte[])] = new SqlVarbinarySerializer(),
            [typeof(DateTime)] = new SqlDatetime2Serializer(),
            [typeof(DateTimeOffset)] = new SqlDatetimeoffsetSerializer(),
            [typeof(DateTimeOffset?)] = new SqlNullableDatetimeoffsetSerializer(),
            [typeof(decimal)] = new SqlDecimalSerializer(),
            [typeof(double)] = new SqlFloatSerializer(),
            [typeof(double?)] = new SqlNullableFloatSerializer(),
            [typeof(float)] = new SqlRealSerializer(),
            [typeof(Guid)] = new SqlUniqueidentifierSerializer(),
            [typeof(int)] = new SqlIntSerializer(),
            [typeof(int?)] = new SqlNullableIntSerializer(),
            [typeof(long)] = new SqlBigIntSerializer(),
            [typeof(short)] = new SqlSmallintSerializer(),
            [typeof(string)] = new SqlNvarcharSerializer(),
            [typeof(TimeSpan)] = new SqlTimeSerializer(),
        };

        /// <summary>
        /// Standard Serializer.
        /// </summary>
        public static readonly Dictionary<Type, ISerializer> SerializerByType = new Dictionary<Type, ISerializer>()
        {
            [typeof(DateTime)] = new DateTimeSerializer(),
            [typeof(double)] = new DoubleSerializer(),
            [typeof(float)] = new FloatSerializer(),
            [typeof(int)] = new IntSerializer(),
            [typeof(long)] = new LongSerializer(),
            [typeof(string)] = new StringSerializer(),
            [typeof(TimeSpan)] = new TimeSpanSerializer(),
        };

        /// <summary>
        /// Get the Default Serializer
        /// </summary>
        /// <typeparam name="T"> typeof(T) </typeparam>
        /// <returns> serializer </returns>
        public static Serializer<T> GetDefaultSerializer<T>()
        {
            if (SerializerByType.ContainsKey(typeof(T)))
            {
                return (Serializer<T>)SerializerByType[typeof(T)];
            }

            throw new NotImplementedException($"A default serializer cannot be found for type {typeof(T).Name}. A serializer can be registered for this type with the {nameof(RegisterDefaultSerializer)} method.");
        }

        /// <summary>
        /// Gets the Default Sql Serializer for T
        /// </summary>
        /// <typeparam name="T"> typeof(T) </typeparam>
        /// <returns> Serializer </returns>
        public static Serializer<T> GetDefaultSqlSerializer<T>()
        {
            if (SqlSerializerByType.ContainsKey(typeof(T)))
            {
                return (Serializer<T>)SqlSerializerByType[typeof(T)];
            }

            throw new NotImplementedException($"A default Always Encrypted compatible serializer cannot be found for type {typeof(T).Name}. A serializer can be registered for this type with the {nameof(RegisterDefaultSqlSerializer)} method.");
        }

        /// <summary>
        /// Register a Defaul Serializer
        /// </summary>
        /// <param name="type"> DataType </param>
        /// <param name="serializer"> serializer </param>
        public static void RegisterDefaultSerializer(Type type, ISerializer serializer)
        {
            type.ValidateNotNull(nameof(type));
            serializer.ValidateNotNull(nameof(serializer));

            SerializerByType[type] = serializer;
        }

        /// <summary>
        /// Registers a default Sql Serializer.
        /// </summary>
        /// <param name="type"> type </param>
        /// <param name="serializer"> serializer </param>
        public static void RegisterDefaultSqlSerializer(Type type, ISqlSerializer serializer)
        {
            type.ValidateNotNull(nameof(type));
            serializer.ValidateNotNull(nameof(serializer));

            SqlSerializerByType[type] = serializer;
        }
    }
}
