namespace EcsCodeGen.Entity.Component;

using EcsCodeGen.Utils;

using global::System;
using global::System.Collections.Immutable;
using global::System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.Entities;

[Generator]
public class GameEventSystemsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is StructDeclarationSyntax classDecl &&
                                        classDecl.AttributeLists.Count > 0,
                transform: (context, _) => (context.Node as StructDeclarationSyntax, context.SemanticModel))
            .Where(pair => Utilities.HasAttribute(nameof(ComponentAttribute), pair.Item1, pair.Item2))
            .Where(pair =>
                Utilities.HasAttribute(nameof(ComponentAttribute), pair.Item1, pair.Item2)
                && Utilities.HasAttribute(nameof(EventAttribute), pair.Item1, pair.Item2)
                )
            .Collect();

        context.RegisterSourceOutput(structDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(StructDeclarationSyntax, SemanticModel)> structs)
    {
        var stringBuilder = new StringBuilder();

        foreach (var (ctx, semanticModel) in structs)
        {
            var structSymbol = semanticModel.GetDeclaredSymbol(ctx);
            if (structSymbol == null)
                throw new ArgumentException("structSymbol is null");

            var componentName = $"{structSymbol.Name}".Replace("Component", "");

            stringBuilder.Append($"builder.Register<On{componentName}AddedEventSystem>(Lifetime.Singleton).AsImplementedInterfaces();");
            stringBuilder.Append($"builder.Register<On{componentName}RemovedEventSystem>(Lifetime.Singleton).AsImplementedInterfaces();");
            stringBuilder.Append($"builder.Register<On{componentName}ChangedEventSystem>(Lifetime.Singleton).AsImplementedInterfaces();");
        }

        var code = @$"""

using Ecs.Systems;

namespace Ecs.Installers {{
                     	public static class GameEventSystems {{
                     		public static void Install(IContainerBuilder builder){{
                     			{stringBuilder}

                     			builder.Register<ClearEventCommandsSystem>(Lifetime.Singleton).AsImplementedInterfaces();
                     		}}
                     	}}
                     }}
                     """;

        var formattedCode = code.FormatCode();

        context.AddSource($"EcsCodeGen.Installer/GameEventSystems.g.cs", formattedCode);
    }
}
