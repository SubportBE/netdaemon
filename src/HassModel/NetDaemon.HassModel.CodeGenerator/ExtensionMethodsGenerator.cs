﻿using NetDaemon.Client.HomeAssistant.Model;

namespace NetDaemon.HassModel.CodeGenerator;

internal static class ExtensionMethodsGenerator
{
    public static IEnumerable<MemberDeclarationSyntax> Generate(IEnumerable<HassServiceDomain> serviceDomains, IReadOnlyCollection<HassState> entities)
    {
        var entityDomains = entities.GroupBy(e => EntityIdHelper.GetDomain(e.EntityId)).Select(x => x.Key);

        return serviceDomains
            .Where(sd =>
                sd.Services?.Any() == true
                && sd.Services.Any(s => entityDomains.Contains(s.Target?.Entity?.Domain)))
            .GroupBy(x => x.Domain, x => x.Services)
            .Select(GenerateClass);
    }

    private static ClassDeclarationSyntax GenerateClass(IGrouping<string?, IReadOnlyCollection<HassService>?> domainServicesGroup)
    {
        var domain = domainServicesGroup.Key!;

        var domainServices = domainServicesGroup
            .SelectMany(services => services!)
            .Where(s => s.Target?.Entity?.Domain != null)
            .Select(group => @group)
            .OrderBy(x => x.Service)
            .ToList();

        return GenerateDomainExtensionClass(domain, domainServices);
    }

    private static ClassDeclarationSyntax GenerateDomainExtensionClass(string domain, IEnumerable<HassService> services)
    {
        var serviceTypeDeclaration = Class(GetEntityDomainExtensionMethodClassName(domain)).ToPublic().ToStatic();

        var serviceMethodDeclarations = services
            .SelectMany(service => GenerateExtensionMethod(domain, service))
            .ToArray();

        return serviceTypeDeclaration.AddMembers(serviceMethodDeclarations);
    }

    private static IEnumerable<MemberDeclarationSyntax> GenerateExtensionMethod(string domain, HassService service)
    {
        var serviceName = service.Service!;
        var serviceArguments = ServiceArguments.Create(domain, service);
        var entityTypeName = GetDomainEntityTypeName(service.Target?.Entity?.Domain!);
        var enumerableTargetTypeName = $"IEnumerable<{entityTypeName}>";

        if (serviceArguments is null)
        {
            yield return ExtensionMethodWithoutArguments(service, serviceName, entityTypeName);
            yield return ExtensionMethodWithoutArguments(service, serviceName, enumerableTargetTypeName);
        }
        else
        {
            yield return ExtensionMethodWithClassArgument(service, serviceName, entityTypeName, serviceArguments);
            yield return ExtensionMethodWithClassArgument(service, serviceName, enumerableTargetTypeName, serviceArguments);

            yield return ExtensionMethodWithSeparateArguments(service, serviceName, entityTypeName, serviceArguments);
            yield return ExtensionMethodWithSeparateArguments(service, serviceName, enumerableTargetTypeName, serviceArguments);
        }
    }

    private static GlobalStatementSyntax ExtensionMethodWithoutArguments(HassService service, string serviceName, string entityTypeName)
    {
        return ParseMethod(
                $@"void {GetServiceMethodName(serviceName)}(this {entityTypeName} target)
                {{
                    target.CallService(""{serviceName}"");
                }}")
            .ToPublic()
            .ToStatic()
            .WithSummaryComment(service.Description);
    }
    
    private static GlobalStatementSyntax ExtensionMethodWithClassArgument(HassService service, string serviceName, string entityTypeName, ServiceArguments serviceArguments)
    {
        return ParseMethod(
                $@"void {GetServiceMethodName(serviceName)}(this {entityTypeName} target, {serviceArguments.TypeName} data)
                {{
                    target.CallService(""{serviceName}"", data);
                }}")
            .ToPublic()
            .ToStatic()
            .WithSummaryComment(service.Description);
    }
    
    private static MemberDeclarationSyntax ExtensionMethodWithSeparateArguments(HassService service, string serviceName, string entityTypeName, ServiceArguments serviceArguments)
    {
        return ParseMethod(
                $@"void {GetServiceMethodName(serviceName)}(this {entityTypeName} target, {serviceArguments.GetParametersList()})
                {{
                    target.CallService(""{serviceName}"", {serviceArguments.GetNewServiceArgumentsTypeExpression()});
                }}")
            .ToPublic()
            .ToStatic()
            .WithSummaryComment(service.Description)
            .WithParameterComment("target", $"The {entityTypeName} to call this service for")
            .WithParameterComments(serviceArguments);
    }
}
