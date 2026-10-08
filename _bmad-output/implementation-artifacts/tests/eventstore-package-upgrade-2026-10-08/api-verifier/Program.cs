using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

using FileStream assembly = File.OpenRead(args[0]);
using var pe = new PEReader(assembly);
MetadataReader metadata = pe.GetMetadataReader();
TypeDefinition owner = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(type =>
    metadata.GetString(type.Name) == "EventStoreDomainServiceSecurityExtensions");
MethodDefinition method = owner.GetMethods().Select(metadata.GetMethodDefinition).Single(method =>
    metadata.GetString(method.Name) == "RequireEventStoreSidecarChannel");
GenericParameter[] generics = method.GetGenericParameters().Select(metadata.GetGenericParameter).ToArray();
string[] constraints = generics.SelectMany(parameter => parameter.GetConstraints())
    .Select(metadata.GetGenericParameterConstraint)
    .Select(constraint => metadata.GetTypeReference((TypeReferenceHandle)constraint.Type))
    .Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name))
    .ToArray();
bool isPublicType = (owner.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
BlobReader signature = metadata.GetBlobReader(method.Signature);
SignatureHeader header = signature.ReadSignatureHeader();
bool hasBuilderSignature = header.IsGeneric && !header.IsInstance
    && signature.ReadCompressedInteger() == 1
    && signature.ReadCompressedInteger() == 1
    && signature.ReadSignatureTypeCode() == SignatureTypeCode.GenericMethodParameter
    && signature.ReadCompressedInteger() == 0
    && signature.ReadSignatureTypeCode() == SignatureTypeCode.GenericMethodParameter
    && signature.ReadCompressedInteger() == 0
    && signature.RemainingBytes == 0;
bool isExtension = method.GetCustomAttributes().Select(metadata.GetCustomAttribute)
    .Where(attribute => attribute.Constructor.Kind == HandleKind.MemberReference)
    .Select(attribute => metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor))
    .Where(constructor => constructor.Parent.Kind == HandleKind.TypeReference)
    .Select(constructor => metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent))
    .Any(type => metadata.GetString(type.Namespace) == "System.Runtime.CompilerServices"
        && metadata.GetString(type.Name) == "ExtensionAttribute");
bool isPublic = (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
bool isStatic = method.Attributes.HasFlag(MethodAttributes.Static);
bool hasEndpointConstraint = constraints.Contains("Microsoft.AspNetCore.Builder.IEndpointConventionBuilder");
bool hasOneParameter = method.GetParameters().Select(metadata.GetParameter).Count(parameter => parameter.SequenceNumber > 0) == 1;
if (!isPublicType || !isExtension || !hasBuilderSignature || !isPublic || !isStatic || generics.Length != 1 || !hasEndpointConstraint || !hasOneParameter)
{
    throw new InvalidDataException("The published extension does not expose the required generic endpoint-builder API.");
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    declaringType = metadata.GetString(owner.Namespace) + "." + metadata.GetString(owner.Name),
    method = metadata.GetString(method.Name),
    isPublicType,
    isExtension,
    hasBuilderSignature,
    isPublic,
    isStatic,
    genericParameters = generics.Select(parameter => metadata.GetString(parameter.Name)),
    constraints,
    hasOneParameter,
}, new JsonSerializerOptions { WriteIndented = true }));
