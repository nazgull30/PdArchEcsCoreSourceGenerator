namespace EcsCodeGen.Entity.System;

using EcsCodeGen.Utils;

using global::System;
using global::System.Diagnostics.Tracing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class CreateComponentSystemsTemplate
{
    public static (string, string) Generate(StructDeclarationSyntax stx, SemanticModel semanticModel)
    {
        var structSymbol = semanticModel.GetDeclaredSymbol(stx);
        if (structSymbol == null)
            throw new ArgumentException("structSymbol is null");

        var componentName = $"{structSymbol.Name}".Replace("Component", "");
        var ns = structSymbol.ContainingNamespace.ToDisplayString();

        var generateEvents = Utilities.HasAttribute(nameof(EventAttribute), stx, semanticModel);

        var onAddedEventSystem = generateEvents ? CreateOnAddedEventSystem(componentName) : "";
        var onRemovedEventSystem = generateEvents ? CreateOnRemovedEventSystem(componentName) : "";
        var onChangedEventSystem = generateEvents ? CreateOnChangedEventSystem(componentName) : "";

        var onAddedReactiveSystem = CreateOnAddedReactiveSystem(componentName);
        var onRemovedReactiveSystem = CreateOnRemovedReactiveSystem(componentName);
        var onChangedReactiveSystem = CreateOnChangedReactiveSystem(componentName);

        var code = @$"""
using System;
using System.Collections.Generic;
using Arch.Core;
using Core.CommandBuffer;
using Core.CommandBuffer.Systems;
using Core.Entities;
using Ecs.Components;
using PdEventBus.Impls;

using {ns};

                     namespace Ecs.Systems
                     {{
                        {onAddedEventSystem}

                        {onRemovedEventSystem}

                        {onChangedEventSystem}

                        {onAddedReactiveSystem}

                        {onRemovedReactiveSystem}

                        {onChangedReactiveSystem}
                     }}
                     """;


        return (componentName, code);
    }

    private static string CreateOnAddedEventSystem(string componentName)
    {
        var code = $$"""
                     public sealed class On{{componentName}}AddedEventSystem(ICommandBuffer commandBuffer)
                         : CommandSystem<On{{componentName}}Added>(commandBuffer)
                     {
                         private readonly Dictionary<Entity, On{{componentName}}Added> _commands = new();

                         protected override void Execute(Span<On{{componentName}}Added> commands)
                         {
                             _commands.Clear();
                             foreach (var command in commands)
                             {
                                 _commands[command.Entity] = command;
                             }

                             foreach (var (entity, command) in _commands)
                             {
                                 Event<On{{componentName}}Added>.Fire(command);
                             }
                         }
                     }
                     """;
        return code;
    }

    private static string CreateOnRemovedEventSystem(string componentName)
    {
        var code = $$"""
                     public sealed class On{{componentName}}RemovedEventSystem(ICommandBuffer commandBuffer)
                         : CommandSystem<On{{componentName}}Removed>(commandBuffer)
                     {
                         private readonly Dictionary<Entity, On{{componentName}}Removed> _commands = new();

                         protected override void Execute(Span<On{{componentName}}Removed> commands)
                         {
                             _commands.Clear();
                             foreach (var command in commands)
                             {
                                 _commands[command.Entity] = command;
                             }

                             foreach (var (entity, command) in _commands)
                             {
                                 Event<On{{componentName}}Removed>.Fire(command);
                             }
                         }
                     }
                     """;
        return code;
    }

    private static string CreateOnChangedEventSystem(string componentName)
    {
        var code = $$"""
                     public sealed class On{{componentName}}ChangedEventSystem(ICommandBuffer commandBuffer)
                         : CommandSystem<On{{componentName}}Changed>(commandBuffer)
                     {
                         private readonly Dictionary<Entity, On{{componentName}}Changed> _commands = new();

                         protected override void Execute(Span<On{{componentName}}Changed> commands)
                         {
                             _commands.Clear();
                             foreach (var command in commands)
                             {
                                 _commands[command.Entity] = command;
                             }

                             foreach (var (entity, command) in _commands)
                             {
                                 Event<On{{componentName}}Changed>.Fire(command);
                             }
                         }
                     }
                     """;
        return code;
    }

    private static string CreateOnAddedReactiveSystem(string componentName)
    {
        var code = $$"""
                     public abstract class On{{componentName}}AddedReactiveSystem(ICommandBuffer commandBuffer)
                         : CommandSystem<On{{componentName}}Added>(commandBuffer)
                     {
                         protected sealed override bool CleanUp => false;

                         protected sealed override void Execute(Span<On{{componentName}}Added> commands)
                         {
                             var lastEvent = commands[^1];
                             var e = lastEvent.Entity;

                             if (Filter(e))
                             {
                                 Execute(lastEvent.Entity, lastEvent.New{{componentName}});
                             }
                         }

                         protected virtual bool Filter(Entity entity) => true;

                         protected virtual void Execute(Entity entity, {{componentName}} new{{componentName}}) {}
                     }
                     """;
        return code;
    }

    private static string CreateOnRemovedReactiveSystem(string componentName)
    {
        var code = $$"""
                     public abstract class On{{componentName}}RemovedReactiveSystem(ICommandBuffer commandBuffer)
                         : CommandSystem<On{{componentName}}Removed>(commandBuffer)
                     {
                         protected sealed override bool CleanUp => false;

                         protected sealed override void Execute(Span<On{{componentName}}Removed> commands)
                         {
                             var lastEvent = commands[^1];
                             var e = lastEvent.Entity;

                             if (Filter(e))
                             {
                                 Execute(lastEvent.Entity);
                             }
                         }

                         protected virtual bool Filter(Entity entity) => true;

                         protected virtual void Execute(Entity entity) {}
                     }
                     """;
        return code;
    }

    private static string CreateOnChangedReactiveSystem(string componentName)
    {
        var code = $$"""
                     public abstract class On{{componentName}}ChangedReactiveSystem(ICommandBuffer commandBuffer)
                         : CommandSystem<On{{componentName}}Changed>(commandBuffer)
                     {
                         protected sealed override bool CleanUp => false;

                         protected sealed override void Execute(Span<On{{componentName}}Changed> commands)
                         {
                             var lastEvent = commands[^1];
                             var e = lastEvent.Entity;

                             if (Filter(e) && !lastEvent.Old{{componentName}}.Equals(lastEvent.New{{componentName}}))
                             {
                                 Execute(lastEvent.Entity, lastEvent.Old{{componentName}}, lastEvent.New{{componentName}});
                             }
                         }

                         protected virtual bool Filter(Entity entity) => true;

                         protected virtual void Execute(Entity entity, {{componentName}} old{{componentName}}, {{componentName}} new{{componentName}}) {}
                     }
                     """;
        return code;
    }
}
