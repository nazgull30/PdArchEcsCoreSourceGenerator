namespace EcsCodeGen.Entity.System;

using EcsCodeGen.Utils;

using global::System;
using global::System.Collections.Immutable;
using global::System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.Entities;

[Generator]
public class ClearCommandsSystemsGenerator : IIncrementalGenerator
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
        var clearSb = new StringBuilder();
        foreach (var (ctx, semanticModel) in structs)
        {
            var structSymbol = semanticModel.GetDeclaredSymbol(ctx);
            if (structSymbol == null)
                throw new ArgumentException("structSymbol is null");

            var componentName = $"{structSymbol.Name}".Replace("Component", "");
            var ns = structSymbol.ContainingNamespace.ToDisplayString();

            clearSb.AppendLine($"commandBuffer.Clear<On{componentName}Added>();");
            clearSb.AppendLine($"commandBuffer.Clear<On{componentName}Removed>();");
            clearSb.AppendLine($"commandBuffer.Clear<On{componentName}Changed>();");
        }

        var code = $$"""
using Arch.Core;
using Arch.Core.Extensions;
using Core.CommandBuffer;
using Core.Systems;
using Ecs.Components;

namespace Ecs.Systems;

                     public sealed class ClearEventCommandsSystem(ICommandBuffer commandBuffer)
                         : IUpdateSystem
                     {
                         public void Update(double t)
                         {
                     	    {{clearSb}}
                         }

                     }
""";
        context.AddSource($"EcsCodeGen.Systems/ClearEventCommandsSystem.g.cs", code.FormatCode());
    }
}
