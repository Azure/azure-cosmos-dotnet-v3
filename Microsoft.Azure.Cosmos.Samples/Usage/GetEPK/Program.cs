namespace Cosmos.Samples.Shared
{
    using System;
    using System.Reflection;

    public class Program
    {
        public static void Main(string[] args)
        {
            Assembly direct = Assembly.Load("Microsoft.Azure.Cosmos.Direct");
            Type pkiType = direct.GetType("Microsoft.Azure.Documents.Routing.PartitionKeyInternal");
            MethodInfo fromJsonStringMethod = pkiType.GetMethod("FromJsonString");
            object pki = fromJsonStringMethod.Invoke(null, new object[] { "[ \"" + args[0] + "\" ]" });

            string methodName = "GetEffectivePartitionKeyForHashPartitioning";
            if(args.Length > 1 && args[1].Contains("2"))
            {
                methodName += "V2";
            }

            MethodInfo getEpkMethod = pkiType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            string epk = (string)getEpkMethod.Invoke(pki, null);
            Console.WriteLine(epk);
        }
    }
}