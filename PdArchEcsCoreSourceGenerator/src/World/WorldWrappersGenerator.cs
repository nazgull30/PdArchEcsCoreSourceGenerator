namespace EcsCodeGen.World;

using System.Collections.Immutable;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public class WorldWrappersGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is InterfaceDeclarationSyntax,
                transform: (context, _) => (context.Node as InterfaceDeclarationSyntax, context.SemanticModel))
            .Where(pair => Utilities.ImplementsInterface("Core.Worlds.IWorld", pair.Item1, pair.Item2))
            .Collect();

        context.RegisterSourceOutput(interfaceDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(InterfaceDeclarationSyntax, SemanticModel)> interfaces)
    {
        foreach (var (ctx, semanticModel) in interfaces)
        {
            var interfaceSymbol = semanticModel.GetDeclaredSymbol(ctx);

            var className = interfaceSymbol.Name.Remove(0, 1);
            var code = @$"""

using Core.Worlds;

using {interfaceSymbol.ContainingNamespace.ToDisplayString()};

                         public class {className}(Arch.Core.World world) : WorldWrapper(world), {interfaceSymbol.Name};

                         """;

            var formattedCode = code.FormatCode();

            context.AddSource($"EcsCodeGen.Worlds/{className}Wrapper.g.cs", formattedCode);
        }


    }
}
