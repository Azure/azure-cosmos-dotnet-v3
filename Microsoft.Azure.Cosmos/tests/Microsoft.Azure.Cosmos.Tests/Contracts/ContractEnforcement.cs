namespace Microsoft.Azure.Cosmos.Tests.Contracts
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Versioning;
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

            // Get the target framework of the currently executing test assembly
            Assembly testAssembly = Assembly.GetExecutingAssembly();
            TargetFrameworkAttribute testTfmAttr = testAssembly.GetCustomAttribute<TargetFrameworkAttribute>();
            string testTfmName = testTfmAttr?.FrameworkName;

            // Find all matching assemblies
            Assembly[] matchingAssemblies = loadedAssemblies
                .Where((candidate) => candidate.FullName.Contains(name + ","))
                .ToArray();

            if (matchingAssemblies.Length == 0)
            {
                return null;
            }

            // If we have multiple matches and know our test TFM, try to find the best match
            if (matchingAssemblies.Length > 1 && !string.IsNullOrEmpty(testTfmName))
            {
                // Try to find an assembly with matching or compatible TFM
                foreach (Assembly candidate in matchingAssemblies)
                {
                    TargetFrameworkAttribute candidateTfmAttr = candidate.GetCustomAttribute<TargetFrameworkAttribute>();
                    string candidateTfmName = candidateTfmAttr?.FrameworkName;

                    // Direct match or compatible framework
                    if (candidateTfmName == testTfmName ||
                        IsCompatibleFramework(candidateTfmName, testTfmName))
                    {
                        return candidate;
                    }
                }
            }

            // Fallback to first match
            return matchingAssemblies.FirstOrDefault();
        }

        /// <summary>
        /// Determines if the candidate framework is compatible with the test framework.
        /// For example, netstandard2.0 is compatible with net6.0 or net8.0.
        /// </summary>
        private static bool IsCompatibleFramework(string candidateFramework, string testFramework)
        {
            if (string.IsNullOrEmpty(candidateFramework) || string.IsNullOrEmpty(testFramework))
            {
                return false;
            }

            try
            {
                FrameworkName candidateFn = new FrameworkName(candidateFramework);
                FrameworkName testFn = new FrameworkName(testFramework);

                // If candidate is .NETStandard, it's compatible with .NETCoreApp
                if (candidateFn.Identifier == ".NETStandard" && testFn.Identifier == ".NETCoreApp")
                {
                    return true;
                }

                // Same framework identifier
                if (candidateFn.Identifier == testFn.Identifier)
                {
                    // For .NETCoreApp, prefer exact or higher version match
                    if (testFn.Identifier == ".NETCoreApp")
                    {
                        return candidateFn.Version.Major == testFn.Version.Major;
                    }
                    return true;
                }
            }
            catch
            {
                // If framework name parsing fails, fall back to false
                return false;
            }

            return false;
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

        /// <summary>
        /// Generates a normalized, deterministic string representation of a CustomAttributeData.
        /// This ensures that attribute parameters are always in the same order, making the
        /// contract comparison machine-agnostic and independent of .NET reflection ordering.
        /// </summary>
        private static string NormalizeCustomAttributeString(CustomAttributeData attributeData)
        {
            // Start with the attribute type name
            string result = attributeData.AttributeType.ToString();

            // Build lists of constructor args and named args
            List<string> parts = new List<string>();

            // Add constructor arguments in order (these are positional, so order matters)
            if (attributeData.ConstructorArguments.Count > 0)
            {
                foreach (CustomAttributeTypedArgument arg in attributeData.ConstructorArguments)
                {
                    parts.Add(FormatAttributeValue(arg));
                }
            }

            // Add named arguments (properties/fields) in sorted order for determinism
            if (attributeData.NamedArguments.Count > 0)
            {
                List<string> namedArgs = new List<string>();
                foreach (CustomAttributeNamedArgument namedArg in attributeData.NamedArguments)
                {
                    namedArgs.Add($"{namedArg.MemberName} = {FormatAttributeValue(namedArg.TypedValue)}");
                }
                namedArgs.Sort(StringComparer.Ordinal);
                parts.AddRange(namedArgs);
            }

            // Always add parentheses for consistency, even if there are no arguments
            result += "(" + string.Join(", ", parts) + ")";

            return result;
        }

        /// <summary>
        /// Formats an attribute value for consistent string representation.
        /// </summary>
        private static string FormatAttributeValue(CustomAttributeTypedArgument arg)
        {
            return arg.Value switch
            {
                null => "null",
                string stringValue => $"\"{stringValue}\"",
                Type typeValue => $"typeof({typeValue})",
                _ when arg.ArgumentType.IsEnum => FormatEnumValue(arg),
                _ when arg.ArgumentType.IsPrimitive => $"({arg.ArgumentType.Name}){arg.Value}",
                _ => arg.Value.ToString()
            };
        }

        /// <summary>
        /// Formats an enum value including both the enum name and numeric value.
        /// </summary>
        private static string FormatEnumValue(CustomAttributeTypedArgument arg)
        {
            string enumName = Enum.GetName(arg.ArgumentType, arg.Value) ?? arg.Value.ToString();
            return $"{arg.ArgumentType.Name}.{enumName} = {arg.Value}";
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

        /// <summary>
        /// Normalizes the string representation of a MemberInfo to ensure machine-agnostic output.
        /// For static methods, ensures the calling convention uses '.' (dot notation).
        /// </summary>
        private static string NormalizeMemberInfoString(MemberInfo memberInfo)
        {
            // Only MethodBase (MethodInfo and ConstructorInfo) has calling convention representation issues
            if (memberInfo is MethodBase methodBase && methodBase.IsStatic && methodBase.DeclaringType != null)
            {
                // Normalize to always use ClassName.MethodName format for static methods
                string declaringTypeName = methodBase.DeclaringType.FullName ?? methodBase.DeclaringType.Name;
                string returnType = methodBase is MethodInfo mi ? mi.ReturnType.ToString() : "Void";
                string methodName = methodBase.Name;
                string parameters = string.Join(", ", methodBase.GetParameters().Select(p => p.ParameterType.ToString()));
                
                return $"{returnType} {declaringTypeName}.{methodName}({parameters})";
            }
            
            return memberInfo.ToString();
        }

        private static string GenerateNameWithMethodAttributes(MethodInfo methodInfo)
        {
            return $"{NormalizeMemberInfoString(methodInfo)};{nameof(methodInfo.IsAbstract)}:{(methodInfo.IsAbstract ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsStatic)}:{(methodInfo.IsStatic ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsVirtual)}:{(methodInfo.IsVirtual ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsGenericMethod)}:{(methodInfo.IsGenericMethod ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsConstructor)}:{(methodInfo.IsConstructor ? bool.TrueString : bool.FalseString)};" +
                $"{nameof(methodInfo.IsFinal)}:{(methodInfo.IsFinal ? bool.TrueString : bool.FalseString)};";
        }

        private static string GenerateNameWithPropertyAttributes(PropertyInfo propertyInfo)
        {
            string name = $"{NormalizeMemberInfoString(propertyInfo)};{nameof(propertyInfo.CanRead)}:{(propertyInfo.CanRead ? bool.TrueString : bool.FalseString)};" +
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
            return $"{NormalizeMemberInfoString(fieldInfo)};{nameof(fieldInfo.IsInitOnly)}:{(fieldInfo.IsInitOnly ? bool.TrueString : bool.FalseString)};" +
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
                    .Select(memberInfo => new KeyValuePair<string, MemberInfo>(
                        $"{NormalizeMemberInfoString(memberInfo)}{string.Join("-", ContractEnforcement.RemoveDebugSpecificAttributes(memberInfo.CustomAttributes).Select(attr => "[" + NormalizeCustomAttributeString(attr) + "]"))}",
                        memberInfo))
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
                    methodSignature = NormalizeMemberInfoString(memberInfo.Value);
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
                "Compute",
                "NetworkMetricData",
                "OperationMetricData"
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