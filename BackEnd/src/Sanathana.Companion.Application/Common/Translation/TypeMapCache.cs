using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Sanathana.Companion.Application.Common.Translation;

internal enum PropKind { String, StringList }

internal sealed record TranslatableProperty(
    string Name,
    Func<object, object?> Get,
    Action<object, object?> Set,
    TranslatableAttribute Attr,
    string Field,
    Func<object, object?>? GetKey,
    PropKind Kind);

internal sealed record ChildProperty(Func<object, object?> Get);

/// <summary>
/// What the walker needs to know about one DTO type. <see cref="IsInert"/> is the important part:
/// a type whose whole reachable graph has no <see cref="TranslatableAttribute"/> is skipped
/// outright, so a big response of untranslatable rows costs one dictionary probe, not a deep walk.
/// </summary>
internal sealed record TypeMap(
    TranslatableProperty[] Translatable,
    ChildProperty[] Children,
    bool IsInert);

/// <summary>
/// Reflection happens once per type, ever. Property access uses compiled expressions rather than
/// <see cref="PropertyInfo.GetValue"/>, which matters when a Panchangam year is 365 rows × 14 fields.
/// </summary>
internal static class TypeMapCache
{
    private static readonly ConcurrentDictionary<Type, TypeMap> Maps = new();
    private static readonly ConcurrentDictionary<Type, bool> Inertness = new();

    private static readonly TypeMap InertMap = new([], [], true);

    public static TypeMap For(Type type) => Maps.GetOrAdd(type, Build);

    private static TypeMap Build(Type type)
    {
        if (IsLeaf(type) || type.GetCustomAttribute<NoTranslateAttribute>() is not null) return InertMap;

        var translatable = new List<TranslatableProperty>();
        var children = new List<ChildProperty>();

        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            if (p.GetCustomAttribute<NoTranslateAttribute>() is not null) continue;
            if (!p.CanRead) continue;

            var attr = p.GetCustomAttribute<TranslatableAttribute>();
            if (attr is not null)
            {
                if (p.PropertyType == typeof(string) && p.CanWrite)
                {
                    translatable.Add(new TranslatableProperty(
                        p.Name, CompileGetter(type, p), CompileSetter(type, p), attr,
                        attr.Field ?? p.Name, CompileKeyGetter(type, attr), PropKind.String));
                    continue;
                }

                if (typeof(IList<string>).IsAssignableFrom(p.PropertyType))
                {
                    // The list is mutated in place, so no setter is required.
                    translatable.Add(new TranslatableProperty(
                        p.Name, CompileGetter(type, p), static (_, _) => { }, attr,
                        attr.Field ?? p.Name, CompileKeyGetter(type, attr), PropKind.StringList));
                    continue;
                }
                // Attribute on an unsupported shape: ignore rather than guess.
                continue;
            }

            if (CouldContainTranslatable(p.PropertyType))
                children.Add(new ChildProperty(CompileGetter(type, p)));
        }

        return translatable.Count == 0 && children.Count == 0
            ? InertMap
            : new TypeMap([.. translatable], [.. children], false);
    }

    /// <summary>True when nothing anywhere under this type can be translated.</summary>
    private static bool IsInertType(Type type, HashSet<Type> visiting)
    {
        if (IsLeaf(type)) return true;
        if (type.GetCustomAttribute<NoTranslateAttribute>() is not null) return true;
        if (Inertness.TryGetValue(type, out var known)) return known;

        // Recursive type: assume inert on this branch; the outer frame decides.
        if (!visiting.Add(type)) return true;

        var inert = true;
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            if (p.GetCustomAttribute<NoTranslateAttribute>() is not null) continue;

            if (p.GetCustomAttribute<TranslatableAttribute>() is not null) { inert = false; break; }

            var inner = ElementTypeOf(p.PropertyType);
            if (inner is not null && !IsInertType(inner, visiting)) { inert = false; break; }
        }

        visiting.Remove(type);
        Inertness[type] = inert;
        return inert;
    }

    private static bool CouldContainTranslatable(Type type)
    {
        var inner = ElementTypeOf(type);
        return inner is not null && !IsInertType(inner, []);
    }

    /// <summary>The type worth descending into: the element type for collections, else the type itself.</summary>
    private static Type? ElementTypeOf(Type type)
    {
        if (IsLeaf(type)) return null;

        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1 && typeof(IEnumerable).IsAssignableFrom(type)) return args[0];
            if (args.Length == 2 && typeof(IDictionary).IsAssignableFrom(type)) return args[1];
        }

        return typeof(IEnumerable).IsAssignableFrom(type) ? null : type;
    }

    private static bool IsLeaf(Type t)
        => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
           || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(DateOnly)
           || t == typeof(TimeOnly) || t == typeof(TimeSpan) || t == typeof(Guid)
           || (Nullable.GetUnderlyingType(t) is { } u && IsLeaf(u));

    private static Func<object, object?> CompileGetter(Type owner, PropertyInfo p)
    {
        var instance = Expression.Parameter(typeof(object), "o");
        var body = Expression.Convert(Expression.Property(Expression.Convert(instance, owner), p), typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, instance).Compile();
    }

    private static Action<object, object?> CompileSetter(Type owner, PropertyInfo p)
    {
        var instance = Expression.Parameter(typeof(object), "o");
        var value = Expression.Parameter(typeof(object), "v");
        var body = Expression.Assign(
            Expression.Property(Expression.Convert(instance, owner), p),
            Expression.Convert(value, p.PropertyType));
        return Expression.Lambda<Action<object, object?>>(body, instance, value).Compile();
    }

    private static Func<object, object?>? CompileKeyGetter(Type owner, TranslatableAttribute attr)
    {
        if (attr.KeyProperty is null) return null;
        var key = owner.GetProperty(attr.KeyProperty, BindingFlags.Public | BindingFlags.Instance);
        return key is null || !key.CanRead ? null : CompileGetter(owner, key);
    }
}
