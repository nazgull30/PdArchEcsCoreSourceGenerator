namespace EcsCodeGen.Entity.Component;

using EcsCodeGen.Utils;
using global::System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public class BasicComponentExtensionsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (context, _) => (ClassDeclarationSyntax)context.Node)
            .Collect();

        context.RegisterSourceOutput(classDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context, ImmutableArray<ClassDeclarationSyntax> classes)
    {
        var code = $$"""

using PdArchEcsCore.CommandBuffer;

namespace PdArchEcsCore.Components
                     {
                         public static partial class ComponentExtensions
                         {
                             private static ICommandBuffer _commandBuffer;

                             public static void Init(ICommandBuffer commandBuffer)
                             {
                                 _commandBuffer = commandBuffer;
                             }
                         }
                     }
""";

        var formattedCode = code.FormatCode();

        context.AddSource("EcsCodeGen.Components/ComponentExtensions.g.cs", formattedCode);
    }
}
