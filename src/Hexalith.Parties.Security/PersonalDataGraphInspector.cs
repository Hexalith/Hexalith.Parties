using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Parties.Contracts;
using Hexalith.Parties.Contracts.Commands;
using Hexalith.Parties.Contracts.ValueObjects;

namespace Hexalith.Parties.Security;

internal static class PersonalDataGraphInspector
{
    public static bool ContainsProtectedData(object? instance)
    {
        return ContainsProtectedData(instance, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public static string GetJsonPropertyName(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        JsonPropertyNameAttribute? jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyName is not null)
        {
            return jsonPropertyName.Name;
        }

        return JsonNamingPolicy.CamelCase.ConvertName(property.Name);
    }

    public static bool ShouldProtectProperty(object owner, PropertyInfo property, object? value)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(property);

        if (value is null)
        {
            return false;
        }

        if (property.IsDefined(typeof(PersonalDataAttribute), inherit: true))
        {
            return true;
        }

        if (owner is OrganizationDetails organization
            && organization.IsNaturalPerson
            && property.Name != nameof(OrganizationDetails.IsNaturalPerson)
            && property.PropertyType == typeof(string))
        {
            return true;
        }

        return false;
    }

    public static bool IsScalarType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type actualType = Nullable.GetUnderlyingType(type) ?? type;
        return actualType.IsPrimitive
            || actualType.IsEnum
            || actualType == typeof(string)
            || actualType == typeof(decimal)
            || actualType == typeof(Guid)
            || actualType == typeof(DateTime)
            || actualType == typeof(DateTimeOffset)
            || actualType == typeof(TimeSpan);
    }

    private static bool ContainsProtectedData(object? instance, HashSet<object> visited)
    {
        if (instance is null)
        {
            return false;
        }

        Type type = instance.GetType();
        if (IsScalarType(type))
        {
            return false;
        }

        if (!type.IsValueType && !visited.Add(instance))
        {
            return false;
        }

        if (instance is OrganizationDetails organization && organization.IsNaturalPerson)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == nameof(OrganizationDetails.IsNaturalPerson))
                {
                    continue;
                }

                if (property.GetValue(instance) is string)
                {
                    return true;
                }
            }
        }

        if (instance is IEnumerable enumerable && instance is not string)
        {
            foreach (object? item in enumerable)
            {
                if (ContainsProtectedData(item, visited))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            object? value = property.GetValue(instance);
            if (ShouldProtectProperty(instance, property, value))
            {
                return true;
            }

            if (ContainsProtectedData(value, visited))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}