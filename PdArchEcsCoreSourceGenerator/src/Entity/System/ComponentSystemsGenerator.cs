namespace EcsCodeGen.Entity.System;

using global::System.Collections.Immutable;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.Entities;

[Generator]
public class ComponentSystemsGenerator : IIncrementalGenerator
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
        foreach (var (ctx, semanticModel) in structs)
        {
            var (componentName, code) = CreateComponentSystemsTemplate.Generate(ctx, semanticModel);

            var formattedCode = code.FormatCode();

            context.AddSource($"EcsCodeGen.Systems/{componentName}Systems.g.cs", formattedCode);
        }
    }
}
