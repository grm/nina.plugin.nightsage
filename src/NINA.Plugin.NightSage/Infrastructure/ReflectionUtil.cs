using System.Collections;
using System.Reflection;

namespace NINA.Plugin.NightSage.Infrastructure;

internal static class ReflectionUtil {
    public static object? Get(object? obj, string name) {
        if (obj == null) return null;
        var type = obj.GetType();
        var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null) return p.GetValue(obj);
        var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return f?.GetValue(obj);
    }

    public static T? Get<T>(object? obj, string name) {
        var value = Get(obj, name);
        if (value == null) return default;
        if (value is T t) return t;
        try { return (T)Convert.ChangeType(value, typeof(T)); } catch { return default; }
    }

    public static void Set(object obj, string name, object? value) {
        var type = obj.GetType();
        var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite) {
            p.SetValue(obj, ConvertFor(value, p.PropertyType));
            return;
        }
        var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) {
            f.SetValue(obj, ConvertFor(value, f.FieldType));
            return;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    public static object? Invoke(object obj, string method, params object?[] args) {
        var candidates = obj.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.Name == method)
            .OrderBy(x => Math.Abs(x.GetParameters().Length - args.Length))
            .ToList();

        foreach (var m in candidates) {
            var ps = m.GetParameters();
            if (args.Length > ps.Length) continue;
            if (args.Length < ps.Count(p => !p.IsOptional)) continue;
            var callArgs = new object?[ps.Length];
            var ok = true;
            for (var i = 0; i < ps.Length; i++) {
                if (i < args.Length) {
                    try { callArgs[i] = ConvertFor(args[i], ps[i].ParameterType); }
                    catch { ok = false; break; }
                } else {
                    callArgs[i] = ps[i].DefaultValue;
                }
            }
            if (ok) return m.Invoke(obj, callArgs);
        }

        throw new MissingMethodException(obj.GetType().FullName, method);
    }

    public static IEnumerable<object> AsObjects(object? value) {
        if (value is not IEnumerable enumerable) yield break;
        foreach (var item in enumerable) if (item != null) yield return item;
    }

    private static object? ConvertFor(object? value, Type targetType) {
        if (value == null) return null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;
        if (underlying.IsEnum) {
            if (value is string s) return Enum.Parse(underlying, s, true);
            return Enum.ToObject(underlying, Convert.ToInt32(value));
        }
        return Convert.ChangeType(value, underlying);
    }
}
