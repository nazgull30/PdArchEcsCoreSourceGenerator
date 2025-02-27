namespace EcsCodeGen.Entity.Component;

using global::System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class CreateComponentEventsTemplate
{
    public static string Generate(StructDeclarationSyntax stx, SemanticModel semanticModel)
    {
        var structSymbol = semanticModel.GetDeclaredSymbol(stx) ?? throw new ArgumentException("structSymbol is null");

        var componentName = $"{structSymbol.Name}".Replace("Component", "");

        var onAdded = CreateOnAdded(componentName);
        var onRemoved = CreateOnRemoved(componentName);
        var onChanged = CreateOnChanged(componentName);

        var code = $$"""
                     {{onAdded}}

                     {{onRemoved}}

                     {{onChanged}}
                     """;

        return code;
    }

    private static string CreateOnAdded(string componentName)
    {
        var code = $$"""
                     public readonly struct On{{componentName}}Added(Entity entity, {{componentName}} new{{componentName}})
                     {
                         public Entity Entity => entity;
                         public {{componentName}} New{{componentName}} => new{{componentName}};
                     }
                     """;
        return code;
    }

    private static string CreateOnRemoved(string componentName)
    {
        var code = $$"""
                     public readonly struct On{{componentName}}Removed(Entity entity)
                     {
                         public Entity Entity => entity;
                     }
                     """;
        return code;
    }

    private static string CreateOnChanged(string componentName)
    {
        var code = $$"""
                     public readonly struct On{{componentName}}Changed(Entity entity, {{componentName}} old{{componentName}}, {{componentName}} new{{componentName}})
                     {
                         public Entity Entity => entity;
                         public {{componentName}} Old{{componentName}} => old{{componentName}};
                         public {{componentName}} New{{componentName}} => new{{componentName}};
                     }
                     """;
        return code;
    }
}
