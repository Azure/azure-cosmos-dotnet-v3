namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Work-around: For Etag and LastModified read-only properties.
    /// 
    /// SDK need to explore way so addressing this gap. Few possibilities are
    /// 1. First class helper utility (Azure SDK guidelines)
    /// 2. Protected CTOR (Since reaonly is limited to system properties Etag/LastModified only, will not result in many overlaods)
    /// 3. Public conditional setter (ex: application config)
    /// </summary>
    public class PropertiesHelper
    {
        public static void SetETag(object properties, string value)
        {
            if (value != null)
            {
                PropertiesHelper.SetProperty(properties, "ETag", value);
            }
        }

        public static void SetLastModified(object properties, DateTime? value)
        {
            if (value != null)
            {
                PropertiesHelper.SetProperty(properties, "LastModified", value);
            }
        }

        private static void SetProperty(object properties, string name, object value)
        {
            var prop = properties.GetType().GetProperties().SingleOrDefault(e => e.Name == name);
            if (prop != null)
            {
                prop.SetValue(properties, value);
            }
        }
    }
}
