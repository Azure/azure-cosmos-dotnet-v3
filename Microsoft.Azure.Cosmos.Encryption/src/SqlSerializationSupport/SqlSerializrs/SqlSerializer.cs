namespace Microsoft.Azure.Cosmos.Encryption
{

    /// <summary>
    /// A marker interface used to mark the capability of a class as implementing a generic <c>ISqlSerializer</c>.
    /// </summary>
    public interface ISqlSerializer { }

    /// <summary>
    /// Contains the methods for serializing and deserializing data objects in a way that is
    /// fully compatible with SQL Server's Always Encrypted feature.
    /// </summary>
    /// <typeparam name="T">The type on which this will perform serialization operations.</typeparam>
    public abstract class SqlSerializer<T> : Serializer<T>, ISqlSerializer { }
}
