namespace Microsoft.Azure.Cosmos
{
    using System.Text.Json;

    public static class JsonElementHelper
    {
        public static bool TryGetPrimitive<T>(object obj, out T? value)
        {
            value = default;

            if (obj is not JsonElement element)
            {
                return false;
            }

            try
            {
                object? result = element.ValueKind switch
                {
                    JsonValueKind.Number when typeof(T) == typeof(int) && element.TryGetInt32(out var i) => i,
                    JsonValueKind.Number when typeof(T) == typeof(long) && element.TryGetInt64(out var l) => l,
                    JsonValueKind.Number when typeof(T) == typeof(double) && element.TryGetDouble(out var d) => d,

                    JsonValueKind.String when typeof(T) == typeof(string) => element.GetString(),

                    JsonValueKind.True when typeof(T) == typeof(bool) => true,
                    JsonValueKind.False when typeof(T) == typeof(bool) => false,

                    _ => null
                };

                if (result is T typed)
                {
                    value = typed;
                    return true;
                }
            }
            catch
            {
                // Swallow and return false if any cast/conversion fails
            }

            return false;
        }
    }
}
