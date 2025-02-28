namespace EcsCodeGen.Command;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.CommandBuffer;

[Generator]
public class CommandBufferExtensionsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is StructDeclarationSyntax structDecl &&
                                        structDecl.AttributeLists.Count > 0,
                transform: (context, _) => (context.Node as StructDeclarationSyntax, context.SemanticModel))
            .Where(pair => Utilities.HasAttribute(nameof(CommandAttribute), pair.Item1, pair.Item2))
            .Collect();

        context.RegisterSourceOutput(structDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context, ImmutableArray<(StructDeclarationSyntax, SemanticModel)> structs)
    {
        var namespaces = new HashSet<string>();

        var methodsSb = new StringBuilder();
        foreach (var (structDeclaration, semanticModel) in structs)
        {
            var (methodCode, structNamespaces) = CreateCommandTemplate.Generate(structDeclaration, semanticModel);
            foreach (var @structNamespace in structNamespaces)
            {
                namespaces.Add(@structNamespace);
            }
            methodsSb.Append(methodCode).Append('\n');
        }

        var namespacesSb = new StringBuilder();
        foreach (var ns in namespaces)
        {
            namespacesSb.Append("using ").Append(ns).Append(";\n");
        }

        var code = $$"""
using System.Collections.Generic;
using Core.CommandBuffer;

{{namespacesSb}}

                     namespace Core.CommandBuffer
                     {
                       public static class CommandBufferExtensions
                       {
                             {{methodsSb}}
                       }
                     }
""";

        var formattedCode = code.FormatCode();

        context.AddSource("EcsCodeGen.Command/CommandBufferExtensions.g.cs", formattedCode);
    }
}
