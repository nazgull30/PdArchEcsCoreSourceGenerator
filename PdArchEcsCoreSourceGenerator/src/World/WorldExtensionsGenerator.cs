namespace EcsCodeGen.World;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.Attributes;
using PdArchEcsCore.Entities;

[Generator]
public class WorldExtensionsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is StructDeclarationSyntax classDecl &&
                                        classDecl.AttributeLists.Count > 0,
                transform: (context, _) => (context.Node as StructDeclarationSyntax, context.SemanticModel))
            .Where(pair => Utilities.HasAttribute(nameof(ComponentAttribute), pair.Item1, pair.Item2))
            .Collect();

        context.RegisterSourceOutput(structDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(StructDeclarationSyntax, SemanticModel)> structs)
    {
        var methodsSb = new StringBuilder();
        var namespaces = new HashSet<string>();
        foreach (var (ctx, semanticModel) in structs)
        {
            var structSymbol = semanticModel.GetDeclaredSymbol(ctx) ?? throw new ArgumentException("structSymbol is null");

            var componentName = $"{structSymbol.Name}".Replace("Component", "");

            var componentNamespace = structSymbol.ContainingNamespace.ToDisplayString();
            namespaces.Add(componentNamespace);

            if (Utilities.HasAttribute(nameof(UniqueAttribute), ctx, semanticModel))
            {
                var methodForUnique = WorldExtensionsMethodsTemplate.CreateForUniqueComponent(componentName);
                methodsSb.AppendLine(methodForUnique);
            }

            var fieldsWithPrimaryEntityIndexAttribute = ctx.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(field => field.Declaration.Variables
                    .Select(variable => new
                    {
                        Field = field,
                        Symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol
                    }))
                .Where(x => x.Symbol != null && HasAttribute(x.Symbol, "PdArchEcsCore.Attributes.PrimaryEntityIndexAttribute"))
                .ToList();

            foreach (var field in fieldsWithPrimaryEntityIndexAttribute)
            {
                var fieldTypeSymbol = semanticModel.GetTypeInfo(field.Field.Declaration.Type).Type;
                var methodForPrimaryEntityIndex = WorldExtensionsMethodsTemplate.CreateForPrimaryEntityIndex(componentName, fieldTypeSymbol);
                methodsSb.AppendLine(methodForPrimaryEntityIndex);
                namespaces.Add(field.Symbol.ContainingNamespace.ToDisplayString());
            }


            var fieldsWithEntityIndexAttribute = ctx.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(field => field.Declaration.Variables
                    .Select(variable => new
                    {
                        Field = field,
                        Symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol
                    }))
                .Where(x => x.Symbol != null && HasAttribute(x.Symbol, "PdArchEcsCore.Attributes.EntityIndexAttribute"))
                .ToList();

            foreach (var field in fieldsWithEntityIndexAttribute)
            {
                var fieldTypeSymbol = semanticModel.GetTypeInfo(field.Field.Declaration.Type).Type;
                var methodForEntityIndex = WorldExtensionsMethodsTemplate.CreateForEntityIndex(componentName, fieldTypeSymbol);
                methodsSb.AppendLine(methodForEntityIndex);
                namespaces.Add(field.Symbol.ContainingNamespace.ToDisplayString());
            }
        }

        methodsSb.AppendLine(WorldExtensionsMethodsTemplate.CreateGetEntities());

        var namespacesSb = new StringBuilder();
        namespaces.ToList().ForEach(ns => namespacesSb.AppendLine($"using {ns};"));

        var code = $$"""
using System;
using System.Collections.Generic;
using Arch.Core;
using PdArchEcsCore.Components;
using PdArchEcsCore.Exceptions;
using PdArchEcsCore.Utils;
using PdArchEcsCore.Worlds;
using PdPools;

{{namespacesSb}}

                     public static class WorldExtensions
                     {
                        {{methodsSb}}
                     }
""";

        context.AddSource($"EcsCodeGen.Worlds/WorldExtensions.g.cs", code.FormatCode());
    }

    private static bool HasAttribute(ISymbol symbol, string attributeFullName)
    {
        return symbol.GetAttributes().Any(attr => attr.AttributeClass.ToDisplayString() == attributeFullName);

    }
}
