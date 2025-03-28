namespace EcsCodeGen.Installer;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public class GameEcsInstallManagerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is InterfaceDeclarationSyntax,
                transform: (context, _) => (context.Node as InterfaceDeclarationSyntax, context.SemanticModel))
            .Where(pair =>
                Utilities.ImplementsInterface("PdArchEcsCore.Worlds.IWorld", pair.Item1, pair.Item2)
                || Utilities.ImplementsInterface("PdArchEcsCore.Interfaces.IGroup", pair.Item1, pair.Item2)
                )
            .Collect();

        context.RegisterSourceOutput(interfaceDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(InterfaceDeclarationSyntax, SemanticModel)> interfaces)
    {
        var worldsSb = new StringBuilder();
        var nameSpaces = new HashSet<string>();

        foreach (var (ctx, semanticModel) in interfaces)
        {
            if (!Utilities.ImplementsInterface("PdArchEcsCore.Worlds.IWorld", ctx, semanticModel))
            {
                continue;
            }

            var worldSymbol = semanticModel.GetDeclaredSymbol(ctx);
            nameSpaces.Add($"using {worldSymbol.ContainingNamespace.ToDisplayString()};");

            var worldName = worldSymbol.Name.Remove(0, 1);
            worldsSb.Append($"var {worldName.FirstCharToLower()} = new {worldName}(World.Create());");
            worldsSb.Append($"builder.RegisterInstance<{worldSymbol}>({worldName.FirstCharToLower()});");
        }

        var groupsSb = new StringBuilder();
        foreach (var (ctx, semanticModel) in interfaces)
        {
            if (!Utilities.ImplementsInterface("PdArchEcsCore.Interfaces.IGroup", ctx, semanticModel))
            {
                continue;
            }

            var groupSymbol = semanticModel.GetDeclaredSymbol(ctx);
            nameSpaces.Add($"using {groupSymbol.ContainingNamespace.ToDisplayString()};");
            nameSpaces.Add($"using {groupSymbol.ContainingNamespace.ToDisplayString()}.Impl;");

            var groupName = groupSymbol.Name.Remove(0, 1);
            groupsSb.Append($"builder.Register<{groupName}>(Lifetime.Singleton).As<{groupSymbol}>();");
        }

        var nameSpacesSb = new StringBuilder();
        nameSpaces.ToList().ForEach(ns => nameSpacesSb.Append(ns));

        var code = $$"""
namespace PdArchEcsCore.Installers;

using Arch.Core;
using PdArchEcsCore.CommandBuffer;
using PdArchEcsCore.Components;
using PdArchEcsCore.Systems;
using PdArchEcsCore.Installers;
using PdArchEcsCore.Utils;
using PdArchEcsCore.Utils.Impl;
using VContainer;
using VContainer.Godot;

{{nameSpacesSb}}

public static class GameEcsInstallManager
{
    public static void Install(IContainerBuilder builder)
    {
        var commandBuffer = new PdArchEcsCore.CommandBuffer.CommandBuffer();
        ComponentExtensions.Init(commandBuffer);
        builder.RegisterInstance<ICommandBuffer>(commandBuffer);

        {{worldsSb}}

        {{groupsSb}}

        builder.Register<Feature>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        builder.Register<Bootstrap>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

        builder.Register<LinkedEntityRepository>(Lifetime.Singleton).As<ILinkedEntityRepository>();

        GameEcsSystems.Install(builder, false);
        GameEventSystems.Install(builder);
    }
}

""";
        context.AddSource($"EcsCodeGen.Installer/GameEcsInstallManager.g.cs", code.FormatCode());

    }
}
