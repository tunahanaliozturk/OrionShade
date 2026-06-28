namespace Moongazing.OrionShade.Logging;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

/// <summary>
/// Re-stamps a wrapping <see cref="ILoggerProvider"/> with the <c>ProviderAlias</c> of the provider it
/// wraps, so per-provider log-level filters keyed on that alias still resolve after decoration.
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.Extensions.Logging</c> reads a provider's filter identity from the <c>ProviderAlias</c>
/// attribute on the concrete runtime type of the instance it holds, and the attribute is not inherited.
/// A decorator is therefore a different type with no alias, which silently drops alias-keyed filter
/// rules. To preserve them this forwarder emits, once per alias, a small public type carrying that same
/// <c>ProviderAlias</c> that delegates every <see cref="ILoggerProvider"/>, <see cref="ISupportExternalScope"/>
/// and <see cref="IDisposable"/> call to the wrapped decorator.
/// </para>
/// <para>
/// <c>ProviderAliasAttribute</c> is not part of <c>Microsoft.Extensions.Logging.Abstractions</c> on
/// every supported target (it moved there only in .NET 10), and this package builds against
/// Abstractions alone, so the attribute type is resolved reflectively from whichever logging assembly
/// the host has loaded. When that type cannot be found, the inner provider declares no alias, or the
/// runtime does not support dynamic code (for example a fully ahead-of-time-compiled app), there is
/// nothing to re-stamp and the wrapped decorator is returned unchanged. Category-keyed filters keep
/// working in every case because the category name is preserved regardless.
/// </para>
/// </remarks>
internal static class ProviderAliasForwarder
{
    private const string ProviderAliasAttributeFullName = "Microsoft.Extensions.Logging.ProviderAliasAttribute";

    private static readonly ConcurrentDictionary<string, Type?> EmittedTypes = new(StringComparer.Ordinal);
    private static readonly Lazy<ModuleBuilder?> Module = new(CreateModule);
    private static readonly Lazy<AliasAttributeReflection?> AliasAttribute = new(ResolveAliasAttribute);

    /// <summary>
    /// Wrap <paramref name="provider"/> in a type carrying the alias of <paramref name="innerProvider"/>
    /// when one is present and dynamic code is available; otherwise return <paramref name="provider"/>
    /// unchanged.
    /// </summary>
    /// <param name="provider">The decorator whose filter identity should mirror the inner provider.</param>
    /// <param name="innerProvider">The concrete provider whose alias is forwarded.</param>
    public static ILoggerProvider WithForwardedAlias(ILoggerProvider provider, ILoggerProvider innerProvider)
    {
        var reflection = AliasAttribute.Value;
        if (reflection is null || !RuntimeFeature.IsDynamicCodeSupported)
        {
            return provider;
        }

        var alias = reflection.ReadAlias(innerProvider.GetType());
        if (string.IsNullOrEmpty(alias))
        {
            return provider;
        }

        var forwarderType = EmittedTypes.GetOrAdd(alias, static a => Emit(a));
        if (forwarderType is null)
        {
            return provider;
        }

        return (ILoggerProvider)Activator.CreateInstance(forwarderType, provider)!;
    }

    private static AliasAttributeReflection? ResolveAliasAttribute()
    {
        // The attribute lives in Microsoft.Extensions.Logging (net8/net9) or, type-forwarded, in
        // Microsoft.Extensions.Logging.Abstractions (net10+). Probe the assemblies already loaded by the
        // host rather than taking a compile-time dependency on either concrete logging assembly.
        var attributeType = Type.GetType(ProviderAliasAttributeFullName, throwOnError: false);
        if (attributeType is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                attributeType = assembly.GetType(ProviderAliasAttributeFullName, throwOnError: false);
                if (attributeType is not null)
                {
                    break;
                }
            }
        }

        if (attributeType is null)
        {
            return null;
        }

        var aliasProperty = attributeType.GetProperty("Alias", BindingFlags.Public | BindingFlags.Instance);
        var constructor = attributeType.GetConstructor([typeof(string)]);
        return aliasProperty is not null && constructor is not null
            ? new AliasAttributeReflection(attributeType, aliasProperty, constructor)
            : null;
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Guarded by RuntimeFeature.IsDynamicCodeSupported; the bare decorator is returned otherwise.")]
    private static ModuleBuilder? CreateModule()
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return null;
        }

        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Moongazing.OrionShade.Logging.AliasForwarders"),
            AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule("Main");
    }

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Guarded by RuntimeFeature.IsDynamicCodeSupported in WithForwardedAlias and CreateModule.")]
    private static Type? Emit(string alias)
    {
        var module = Module.Value;
        var reflection = AliasAttribute.Value;
        if (module is null || reflection is null)
        {
            return null;
        }

        // A public sealed type that holds the wrapped provider and forwards every interface call to it,
        // carrying the inner provider's alias so MEL filter resolution matches it.
        var type = module.DefineType(
            "AliasForwarder_" + alias,
            TypeAttributes.Public | TypeAttributes.Sealed,
            typeof(object),
            [typeof(ILoggerProvider), typeof(ISupportExternalScope), typeof(IDisposable)]);

        type.SetCustomAttribute(new CustomAttributeBuilder(reflection.Constructor, [alias]));

        var innerField = type.DefineField("inner", typeof(ILoggerProvider), FieldAttributes.Private | FieldAttributes.InitOnly);

        EmitConstructor(type, innerField);
        EmitForwardingMethod(type, innerField, typeof(ILoggerProvider).GetMethod(nameof(ILoggerProvider.CreateLogger))!);
        EmitForwardingMethod(type, innerField, typeof(ISupportExternalScope).GetMethod(nameof(ISupportExternalScope.SetScopeProvider))!);
        EmitForwardingMethod(type, innerField, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!);

        return type.CreateType();
    }

    private static void EmitConstructor(TypeBuilder type, FieldBuilder innerField)
    {
        var ctor = type.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(ILoggerProvider)]);
        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, innerField);
        il.Emit(OpCodes.Ret);
    }

    private static void EmitForwardingMethod(TypeBuilder type, FieldBuilder innerField, MethodInfo target)
    {
        var parameters = Array.ConvertAll(target.GetParameters(), static p => p.ParameterType);
        var method = type.DefineMethod(
            target.Name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            target.ReturnType,
            parameters);

        var il = method.GetILGenerator();

        // The inner field is typed as ILoggerProvider; cast it to the interface that declares the call.
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, innerField);
        il.Emit(OpCodes.Castclass, target.DeclaringType!);
        for (var i = 0; i < parameters.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, i + 1);
        }

        il.Emit(OpCodes.Callvirt, target);
        il.Emit(OpCodes.Ret);

        type.DefineMethodOverride(method, target);
    }

    /// <summary>The reflected <c>ProviderAliasAttribute</c> members used to read and stamp an alias.</summary>
    private sealed class AliasAttributeReflection(Type attributeType, PropertyInfo aliasProperty, ConstructorInfo constructor)
    {
        public ConstructorInfo Constructor => constructor;

        public string? ReadAlias(Type providerType)
        {
            var attribute = providerType.GetCustomAttribute(attributeType, inherit: false);
            return attribute is null ? null : (string?)aliasProperty.GetValue(attribute);
        }
    }
}
