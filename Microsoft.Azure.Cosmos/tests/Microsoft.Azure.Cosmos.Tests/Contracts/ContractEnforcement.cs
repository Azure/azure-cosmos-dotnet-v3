namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    public class ContractEnforcement
    {
        private static readonly InvariantComparer invariantComparer = new();

        private static Assembly GetAssemblyLocally(string name)
        {
            Assembly.Load(name);
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            return loadedAssemblies
                .Where((candidate) => candidate.FullName.Contains(name + ","))
                .FirstOrDefault();
        }

        private sealed class MemberMetadata
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public MemberTypes Type { get; }
            public List<string> Attributes { get; }
            public string MethodInfo { get; }
            public MemberMetadata(MemberTypes type, List<string> attributes, string methodInfo)
            {
                this.Type = type;
                this.Attributes = attributes;
                this.MethodInfo = methodInfo;
            }
        }

        private sealed class TypeTree
        {
            [JsonIgnore]
            public Type Type { get; }
            public SortedDictionary<string, TypeTree> Subclasses { get; } = new SortedDictionary<string, TypeTree>();
            public SortedDictionary<string, MemberMetadata> Members { get; } = new SortedDictionary<string, MemberMetadata>();
            public SortedDictionary<string, TypeTree> NestedTypes { get; } = new SortedDictionary<string, TypeTree>();

            public TypeTree(Type type)
            {
                this.Type = type;
            }
        }

        private static IEnumerable<CustomAttributeData> RemoveDebugSpecificAttributes(IEnumerable<CustomAttributeData> attributes)
        {
            return attributes.Where(x =>
                !x.AttributeType.Name.Contains("SuppressMessageAttribute") &&
                !x.AttributeType.Name.Contains("DynamicallyInvokableAttribute") &&
                !x.AttributeType.Name.Contains("NonVersionableAttribute") &&
                !x.AttributeType.Name.Contains("ReliabilityContractAttribute") &&
                !x.AttributeType.Name.Contains("NonVersionableAttribute") &&
                !x.AttributeType.Name.Contains("DebuggerStepThroughAttribute") &&
                !x.AttributeType.Name.Contains("IsReadOnlyAttribute")
            );
        }

        private static string GenerateNameWithClassAttributes(Type type)
        {
            // FullName contains unwanted assembly artifacts like version when it has a generic type
            Type baseType = type.BaseType;
            string baseTypeString = string.Empty;
            if (baseType != null)
            {
                // Remove assembly info to avoid breaking the contract just from version change
                baseTypeString = baseType.FullName;
                string assemblyInfo = baseType.Assembly?.ToString();
                if (!string.IsNullOrEmpty(assemblyInfo) &&
                    !string.IsNullOrWhiteSpace(baseTypeString))
                {
                    baseTypeString = baseTypeString.Replace(assemblyInfo, string.Empty);
                }
            }

            return $"{type.FullName};{baseTypeString};{nameof(type.IsAbstract)}:{(type.IsAbstract ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsSealed)}:{(type.IsSealed ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsInterface)}:{(type.IsInterface ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsEnum)}:{(type.IsEnum ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsClass)}:{(type.IsClass ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsValueType)}:{(type.IsValueType ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsNested)}:{(type.IsNested ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(type.IsGenericType)}:{(type.IsGenericType ? bool.TrueString : bool.FalseString)};" +
#pragma warning disable SYSLIB0050 // 'Type.IsSerializable' is obsolete: 'Formatter-based serialization is obsolete and should not be used.
                $"{nameof(type.IsSerializable)}:{(type.IsSerializable ? bool.TrueString : bool.FalseString)}";
#pragma warning restore SYSLIB0050 // 'Type.IsSerializable' is obsolete: 'Formatter-based serialization is obsolete and should not be used.

        }

        private static string GenerateNameWithMethodAttributes(MethodInfo methodInfo)
        {
            return $"{methodInfo};{nameof(methodInfo.IsAbstract)}:{(methodInfo.IsAbstract ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsStatic)}:{(methodInfo.IsStatic ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsVirtual)}:{(methodInfo.IsVirtual ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsGenericMethod)}:{(methodInfo.IsGenericMethod ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsConstructor)}:{(methodInfo.IsConstructor ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsFinal)}:{(methodInfo.IsFinal ? bool.TrueString : bool.FalseString)};";
        }

        private static string GenerateNameWithPropertyAttributes(PropertyInfo propertyInfo)
        {
            string name = $"{propertyInfo};{nameof(propertyInfo.CanRead)}:{(propertyInfo.CanRead ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(propertyInfo.CanWrite)}:{(propertyInfo.CanWrite ? bool.TrueString : bool.FalseString)};";

            MethodInfo getMethodInfo = propertyInfo.GetGetMethod();
            if (getMethodInfo != null)
            {
                name += ContractEnforcement.GenerateNameWithMethodAttributes(getMethodInfo);
            }

            MethodInfo setMethodInfo = propertyInfo.GetSetMethod();
            if (setMethodInfo != null)
            {
                name += ContractEnforcement.GenerateNameWithMethodAttributes(setMethodInfo);
            }

            return name;
        }

        private static string GenerateNameWithFieldAttributes(FieldInfo fieldInfo)
        {
            return $"{fieldInfo};{nameof(fieldInfo.IsInitOnly)}:{(fieldInfo.IsInitOnly ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(fieldInfo.IsStatic)}:{(fieldInfo.IsStatic ? bool.TrueString : bool.FalseString)};";
        }

        private static TypeTree BuildTypeTree(TypeTree root, Type[] types, BindingFlags bindingflags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        {
            IEnumerable<Type> subclassTypes = types.Where((type) => type.IsSubclassOf(root.Type)).OrderBy(o => o.FullName, invariantComparer);
            foreach (Type subclassType in subclassTypes)
            {
                root.Subclasses[ContractEnforcement.GenerateNameWithClassAttributes(subclassType)] = ContractEnforcement.BuildTypeTree(new TypeTree(subclassType), types);
            }

            IEnumerable<KeyValuePair<string, MemberInfo>> memberInfos =
                root.Type.GetMembers(bindingflags)
                    .Select(memberInfo => new KeyValuePair<string, MemberInfo>($"{memberInfo}{string.Join("-", ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.CustomAttributes))}", memberInfo))
                    .OrderBy(o => o.Key, invariantComparer);
            foreach (KeyValuePair<string, MemberInfo> memberInfo in memberInfos)
            {
                List<string> attributes = ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.Value.CustomAttributes)
                        .Select((customAttribute) => customAttribute.AttributeType.Name)
                        .ToList();
                attributes.Sort(invariantComparer);

                string methodSignature = null;

                if (memberInfo.Value.MemberType == MemberTypes.Method)
                {
                    MethodInfo methodInfo = (MethodInfo)memberInfo.Value;
                    methodSignature = ContractEnforcement.GenerateNameWithMethodAttributes(methodInfo);
                }
                else if (memberInfo.Value.MemberType == MemberTypes.Property)
                {
                    PropertyInfo propertyInfo = (PropertyInfo)memberInfo.Value;
                    methodSignature = ContractEnforcement.GenerateNameWithPropertyAttributes(propertyInfo);
                }
                else if (memberInfo.Value.MemberType == MemberTypes.Field)
                {
                    FieldInfo fieldInfo = (FieldInfo)memberInfo.Value;
                    methodSignature = ContractEnforcement.GenerateNameWithFieldAttributes(fieldInfo);
                }
                else if (memberInfo.Value.MemberType == MemberTypes.Constructor || memberInfo.Value.MemberType == MemberTypes.Event)
                {
                    methodSignature = memberInfo.ToString();
                }

                // Certain custom attributes add the following to the string value "d__9" which sometimes changes
                // based on the .NET SDK version it is being built on. This removes the value to avoid showing
                // breaking change when there is none.
                string key = Regex.Replace(memberInfo.Key, @"d__\d+", string.Empty);
                root.Members[
                        key
                    ] = new MemberMetadata(
                    memberInfo.Value.MemberType,
                    attributes,
                    methodSignature);
            }

            foreach (Type nestedType in root.Type.GetNestedTypes().OrderBy(o => o.FullName))
            {
                root.NestedTypes[ContractEnforcement.GenerateNameWithClassAttributes(nestedType)] = ContractEnforcement.BuildTypeTree(new TypeTree(nestedType), types);
            }

            return root;
        }

        public static void ValidateContractContainBreakingChanges(
            string dllName,
            string baselinePath,
            string breakingChangesPath)
        {
            string localJson = GetCurrentContract(dllName);
            File.WriteAllText($"Contracts/{breakingChangesPath}", localJson);

            string baselineJson = GetBaselineContract(baselinePath);
            ContractEnforcement.ValidateJsonAreSame(baselineJson, localJson);
        }

        public static void ValidateTelemetryContractContainBreakingChanges(
          string dllName,
          string baselinePath,
          string breakingChangesPath)
        {
            string localTelemetryJson = GetCurrentTelemetryContract(dllName);
            File.WriteAllText($"Contracts/{breakingChangesPath}", localTelemetryJson);

            string telemetryBaselineJson = GetBaselineContract(baselinePath);
            ContractEnforcement.ValidateJsonAreSame(localTelemetryJson, telemetryBaselineJson);
        }

        public static void ValidatePreviewContractContainBreakingChanges(
            string dllName,
            string officialBaselinePath,
            string previewBaselinePath,
            string previewBreakingChangesPath)
        {
            string currentPreviewJson = ContractEnforcement.GetCurrentContract(
              dllName);

            JObject currentJObject = JObject.Parse(currentPreviewJson);
            JObject officialBaselineJObject = JObject.Parse(File.ReadAllText("Contracts/" + officialBaselinePath));

            string currentJsonNoOfficialContract = ContractEnforcement.RemoveDuplicateContractElements(
                localContract: currentJObject,
                officialContract: officialBaselineJObject);

            Assert.IsNotNull(currentJsonNoOfficialContract);

            string baselinePreviewJson = ContractEnforcement.GetBaselineContract(previewBaselinePath);
            File.WriteAllText($"Contracts/{previewBreakingChangesPath}", currentJsonNoOfficialContract);

            ContractEnforcement.ValidateJsonAreSame(baselinePreviewJson, currentJsonNoOfficialContract);
        }

        public static string GetCurrentContract(string dllName)
        {
            TypeTree locally = new(typeof(object));
            Assembly assembly = ContractEnforcement.GetAssemblyLocally(dllName);
            Type[] exportedTypes = assembly.GetExportedTypes();
            ContractEnforcement.BuildTypeTree(locally, exportedTypes);

            string localJson = JsonConvert.SerializeObject(locally, Formatting.Indented);
            return localJson;
        }

        public static string GetCurrentTelemetryContract(string dllName)
        {
            List<string> nonTelemetryModels = new()
            {
                "AzureVMMetadata",
                "Compute"
            };

            TypeTree locally = new(typeof(object));
            Assembly assembly = ContractEnforcement.GetAssemblyLocally(dllName);
            Type[] exportedTypes = assembly.GetTypes().Where(t =>
                                                                t != null &&
                                                                t.Namespace != null &&
                                                                t.Namespace.Contains("Microsoft.Azure.Cosmos.Telemetry.Models") &&
                                                                !nonTelemetryModels.Contains(t.Name))
                                                       .ToArray();

            ContractEnforcement.BuildTypeTree(locally, exportedTypes, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            string localJson = JsonConvert.SerializeObject(locally, Formatting.Indented);
            return localJson;
        }

        public static string GetBaselineContract(string baselinePath)
        {
            string baselineFile = File.ReadAllText("Contracts/" + baselinePath);
            return NormalizeJsonString(baselineFile);
        }

        public static string RemoveDuplicateContractElements(JObject localContract, JObject officialContract)
        {
            RemoveDuplicateContractHelper(localContract, officialContract);
            string noDuplicates = localContract.ToString();
            return NormalizeJsonString(noDuplicates);
        }

        private static string NormalizeJsonString(string file)
        {
            TypeTree baseline = JsonConvert.DeserializeObject<TypeTree>(file);
            string updatedString = JsonConvert.SerializeObject(baseline, Formatting.Indented);
            return updatedString;
        }

        private static void RemoveDuplicateContractHelper(JObject previewContract, JObject officialContract)
        {
            foreach (KeyValuePair<string, JToken> token in officialContract)
            {
                JToken previewLocalToken = previewContract[token.Key];
                if (previewLocalToken != null)
                {
                    if (JToken.DeepEquals(previewLocalToken, token.Value))
                    {
                        previewContract.Remove(token.Key);
                    }
                    else if (previewLocalToken.Type == JTokenType.Object && token.Value.Type == JTokenType.Object)
                    {
                        RemoveDuplicateContractHelper(previewLocalToken as JObject, token.Value as JObject);
                    }
                }
            }
        }

        public static void ValidateJsonAreSame(string baselineJson, string currentJson)
        {
            // This prevents failures caused by it being serialized slightly different order
            string normalizedBaselineJson = NormalizeJsonString(baselineJson);
            string normalizedCurrentJson = NormalizeJsonString(currentJson);
            System.Diagnostics.Trace.TraceWarning($"String length Expected: {normalizedBaselineJson.Length};Actual:{normalizedCurrentJson.Length}");
            if (!string.Equals(normalizedCurrentJson, normalizedBaselineJson, StringComparison.InvariantCulture))
            {
                System.Diagnostics.Trace.TraceWarning($"Expected: {normalizedBaselineJson}");
                System.Diagnostics.Trace.TraceWarning($"Actual: {normalizedCurrentJson}");
                Assert.Fail($@"Public API has changed. If this is expected, then run (EnlistmentRoot)\UpdateContracts.ps1 . To see the differences run the update script and use git diff.");
            }
        }

        private class InvariantComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                return Comparer.DefaultInvariant.Compare(a, b);
            }
        }
    }
}