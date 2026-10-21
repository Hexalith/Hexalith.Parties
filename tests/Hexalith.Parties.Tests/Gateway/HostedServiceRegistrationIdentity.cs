using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.Parties.Tests.Gateway;

/// <summary>Identifies excluded registrations without constructing their hosted services.</summary>
internal static class HostedServiceRegistrationIdentity
{
    private static readonly IReadOnlyDictionary<ushort, OpCode> s_opcodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));

    /// <summary>Identifies concrete service types referenced by a factory without compiler or version noise.</summary>
    /// <param name="descriptor">The hosted-service registration.</param>
    /// <returns>A concrete type name or the sorted possible factory service types.</returns>
    internal static string Get(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.ImplementationType is { } implementation)
        {
            return implementation.Name;
        }

        if (descriptor.ImplementationInstance is { } instance)
        {
            return instance.GetType().Name;
        }

        MethodInfo method = descriptor.ImplementationFactory?.Method
            ?? throw new InvalidOperationException("Hosted-service factory metadata is required.");
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new InvalidOperationException("Hosted-service factory IL is required.");
        var services = new SortedSet<string>(StringComparer.Ordinal);
        for (int offset = 0; offset < bytes.Length;)
        {
            ushort value = bytes[offset++];
            if (value == 0xfe)
            {
                value = (ushort)(0xfe00 | bytes[offset++]);
            }

            OpCode opcode = s_opcodes[value];
            OperandType operand = opcode.OperandType;
            if (operand is OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineField or OperandType.InlineTok)
            {
                MemberInfo member = method.Module.ResolveMember(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset)),
                    method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments())!;
                AddServiceType(services, member is Type type ? type : member.DeclaringType);
                if (member is MethodInfo called)
                {
                    AddServiceType(services, called.ReturnType);
                    foreach (Type argument in called.GetGenericArguments())
                    {
                        AddServiceType(services, argument);
                    }
                }
                else if (member is FieldInfo field)
                {
                    AddServiceType(services, field.FieldType);
                }
            }

            offset += operand switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => checked(4 + 4 * BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset))),
                OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or OperandType.InlineMethod
                    or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                _ => throw new InvalidOperationException($"Unsupported factory operand: {operand}."),
            };
        }

        if (services.Count == 0)
        {
            throw new InvalidOperationException("Hosted-service factory has no concrete service type evidence.");
        }

        return "factory:" + string.Join("|", services);
    }

    private static void AddServiceType(ISet<string> services, Type? type)
    {
        if (type is { IsAbstract: false, IsInterface: false } && typeof(IHostedService).IsAssignableFrom(type))
        {
            services.Add(type.FullName ?? throw new InvalidOperationException("Hosted-service type name is required."));
        }
    }
}
