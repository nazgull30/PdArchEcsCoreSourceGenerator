namespace EcsCodeGen.Entity.Component;

using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class CreateComponentExtensionsTemplate
{
    public static (string, string) Generate(StructDeclarationSyntax stx, SemanticModel semanticModel)
    {
        var structSymbol = semanticModel.GetDeclaredSymbol(stx) ?? throw new ArgumentException("structSymbol is null");

        var componentName = $"{structSymbol.Name}".Replace("Component", "");

        var componentNamespace = structSymbol.ContainingNamespace.ToDisplayString();
        var namespaces = new HashSet<string> { componentNamespace };

        var properties = GetProperties(stx, semanticModel);

        var extension = properties.Count > 0 ? CreateExtension(componentName, properties, componentNamespace) : "";
        var getter = CreateGetter(componentName);
        var has = CreateHas(componentName);

        var (addCode, addNs) = CreateAdd(componentName, properties);
        addNs.ForEach(ns => namespaces.Add(ns));

        var removeCode = CreateRemove(componentName);
        var (replaceCode, replaceNs) = CreateReplace(componentName, properties);
        replaceNs.ForEach(ns => namespaces.Add(ns));

        var namespacesBuilder = new StringBuilder();
        foreach (var ns in namespaces)
        {
            namespacesBuilder.Append($"using {ns};\n");
        }

        var eventsCode = CreateComponentEventsTemplate.Generate(stx, semanticModel);

        var code = $$"""
using Arch.Core;
using Arch.Core.Extensions;
using PdArchEcsCore.CommandBuffer;
using PdArchEcsCore.Exceptions;

{{namespacesBuilder}}

                     {{extension}}

                     namespace Ecs.Components
                     {
                         public static partial class ComponentExtensions
                         {
                            {{getter}}

                            {{has}}

                            {{addCode}}

                            {{replaceCode}}

                            {{removeCode}}
                         }

                         {{eventsCode}}
                     }
""";


        return (componentName, code);
    }

    private static string CreateExtension(string componentName, List<PropertyInfo> properties, string componentNamespace)
    {
        var body = new StringBuilder();
        foreach (var property in properties)
        {
            body.Append($"{property.FieldName}: ").Append('{' + property.FieldName + '}').Append(", ");
        }

        if (body.Length > 0)
        {
            body.Remove(body.Length - 2, 2);
        }

        var stringExpression = properties.Count > 1
            ? $"|{componentName} -> {body}|"
            : '{' + properties[0].FieldName + '}';

        var code = $$"""
                     namespace {{componentNamespace}}
                     {
                         public partial struct {{componentName}}
                         {
                             public override string ToString() => $"{{stringExpression}}";
                         }
                     }
                     """;
        return code;
    }

    private static string CreateGetter(string componentName)
    {
        var code = $$"""
                     public static ref {{componentName}} {{componentName}}(this ref Entity entity)
                     {
                        if(!entity.Has<{{componentName}}>())
                        {
                            throw new EntityDoesNotHaveComponentException("{{componentName}}");
                        }
                        return ref entity.Get<{{componentName}}>();
                     }
                     """;
        return code;
    }

    private static string CreateHas(string componentName)
    {
        var code = $$"""
                     public static bool Has{{componentName}}(this Entity entity)
                     {
                         return entity.Has<{{componentName}}>();
                     }
                     """;
        return code;
    }

    private static (string, List<string>) CreateAdd(string componentName, List<PropertyInfo> properties)
    {
        var parameters = new StringBuilder();
        var initialParams = new StringBuilder();

        var namespaces = new List<string>();
        foreach (var property in properties)
        {
            parameters.Append(property.FieldType).Append(' ').Append($"new{property.FieldName}").Append(',');
            initialParams.Append($"{property.FieldName} = new{property.FieldName},\n");
            namespaces.Add(property.Namespace);
        }

        if (parameters.Length > 0)
        {
            parameters.Remove(parameters.Length - 1, 1);
        }

        var methodDeclaration = parameters.Length > 0
            ? $"public static void Add{componentName}(this Entity entity, {parameters})"
            : $"public static void Add{componentName}(this Entity entity)";

        var code = $$"""
                     {{methodDeclaration}}
                     {
                         if (entity.Has{{componentName}}())
                         {
                             throw new EntityAlreadyHasComponentException("{{componentName}}");
                         }
                         var new{{componentName}} = new {{componentName}}
                         {
                             {{initialParams}}
                         };
                         entity.Add(new{{componentName}});
                         _commandBuffer.Create(new On{{componentName}}Added(entity, new{{componentName}}));
                     }
                     """;
        return (code, namespaces);
    }

    private static (string, List<string>) CreateReplace(string componentName, List<PropertyInfo> properties)
    {
        var parameters = new StringBuilder();
        var initialParams = new StringBuilder();

        var namespaces = new List<string>();
        foreach (var property in properties)
        {
            parameters.Append(property.FieldType).Append(' ').Append($"new{property.FieldName}").Append(',');
            initialParams.Append($"{property.FieldName} = new{property.FieldName},\n");
            namespaces.Add(property.Namespace);
        }
        if (parameters.Length > 0)
        {
            parameters.Remove(parameters.Length - 1, 1);
        }

        var methodDeclaration = parameters.Length > 0
            ? $"public static void Replace{componentName}(this Entity entity, {parameters})"
            : $"public static void Replace{componentName}(this Entity entity)";

        var code = $$"""
                     {{methodDeclaration}}
                     {
                         var new{{componentName}} = new {{componentName}}
                         {
                             {{initialParams}}
                         };
                         var old{{componentName}} = entity.Has{{componentName}}() ? entity.{{componentName}}() : default;
                         if (entity.Has{{componentName}}())
                         {
                             entity.Set(new{{componentName}});
                         }
                         else
                         {
                             entity.Add(new{{componentName}});
                         }
                         _commandBuffer.Create(new On{{componentName}}Changed(entity, old{{componentName}}, new{{componentName}}));
                     }
                     """;
        return (code, namespaces);
    }

    private static string CreateRemove(string componentName)
    {
        var code = $$"""
                     public static void Remove{{componentName}}(this Entity entity)
                     {
                         if (!entity.Has{{componentName}}())
                         {
                             throw new EntityDoesNotHaveComponentException("{{componentName}}");
                         }
                         entity.Remove<{{componentName}}>();
                         _commandBuffer.Create(new On{{componentName}}Removed(entity));
                     }
                     """;
        return code;
    }

    private static List<PropertyInfo> GetProperties(StructDeclarationSyntax stx, SemanticModel semanticModel)
    {
        var properties = new List<PropertyInfo>();
        foreach (var field in stx.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldTypeSyntax = field.Declaration.Type;
                var fieldTypeSymbol = semanticModel.GetTypeInfo(fieldTypeSyntax).Type;

                var fieldName = variable.Identifier.Text;
                var fieldType = fieldTypeSymbol.ToDisplayString();

                properties.Add(new PropertyInfo(fieldName, fieldType, fieldTypeSymbol.ContainingNamespace.ToDisplayString()));

            }
        }
        return properties;
    }

    private struct PropertyInfo(string fieldName, string fieldType, string ns)
    {
        public readonly string FieldName = fieldName;
        public readonly string FieldType = fieldType;
        public readonly string Namespace = ns;
    }
}
