namespace EcsCodeGen.Entity.Linkable;

using EcsCodeGen.Utils;

using global::System;
using global::System.Collections.Generic;
using global::System.Collections.Immutable;
using global::System.Linq;
using global::System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.Attributes.Linkable;

public static class ComponentEventHandlerClassTemplate
{
    public static string Generate(Compilation compilation, ClassDeclarationSyntax ctx, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(ctx);
        if (classSymbol == null)
            throw new ArgumentException("classSymbol is null");

        var eventHandlers = ComponentEventHandlerGenerator.GetEventHandlers(ctx, semanticModel);
        var eventHandlersCode = new StringBuilder();
        foreach (var eventHandler in eventHandlers)
        {
            var eventHandlerCode = CreateEventHandler(compilation, eventHandler);
            eventHandlersCode.Append(eventHandlerCode).Append('\n');
        }



        var eventSubscriptionsCode = new StringBuilder();
        foreach (var eventHandler in eventHandlers)
        {
            var methodName = eventHandler.Identifier.Text;
            eventSubscriptionsCode.AppendLine($"Event<{methodName}>.Instance.Subscribe({methodName}Internal).AddTo(_disposables);");
        }

        var onDestroyed = Utilities.HasAttribute(nameof(OnDestroyedAttribute), ctx, semanticModel);
        var onInitializeOnLink = Utilities.HasAttribute(nameof(InitializeOnLinkAttribute), ctx, semanticModel);

        var initializeOnLinkCode = new StringBuilder();
        if (onInitializeOnLink)
        {
            var components = new HashSet<string>();
            foreach (var eventHandler in eventHandlers)
            {
                var methodName = eventHandler.Identifier.Text;
                var component = methodName.Replace("On", "").Replace("Added", "").Replace("Removed", "").Replace("Changed", "");
                components.Add(component);
            }

            foreach (var component in components)
            {
                initializeOnLinkCode.AppendLine($"Set{component}(this.entity.{component}().Value);");
            }
        }

        var exitTreeCode = onDestroyed
            ? $$"""
                public override void _ExitTree()
                {
                    OnDestroyed();
                    _disposables.Dispose();
                }
                """
            : $$"""
                public override void _ExitTree()
                {
                    _disposables.Dispose();
                }
                """;


        var code = $$"""
namespace {{classSymbol.ContainingNamespace.ToDisplayString()}};

using Arch.Core;
using PdArchEcsCore.Utils;
using PdArchEcsCore.Components;
using Godot;
using PdEventBus.Impls;
using PdEventBus.Utils;
using VContainer;

                     public partial class {{classSymbol.Name}}
                     {
                         private readonly CompositeDisposable _disposables = new();
                         protected Entity entity;

                         [Inject]
                         private ILinkedEntityRepository _linkedEntityRepository;

                         public void Link(Entity entity, ILinkedEntityRepository linkedEntityRepository)
                         {
                             this.entity = entity;
                             _linkedEntityRepository = linkedEntityRepository;
                             _linkedEntityRepository.Add(GetInstanceId(), entity);

                             {{eventSubscriptionsCode}}
                             {{initializeOnLinkCode}}
                         }

                         public void Link(Entity entity)
                         {
                             this.entity = entity;
                             _linkedEntityRepository.Add(GetInstanceId(), entity);

                             {{eventSubscriptionsCode}}
                             {{initializeOnLinkCode}}
                         }

                         public void Unlink()
                         {
                            entity = default;
                            _disposables.Dispose();
                         }

                         private bool CanHandleEvent(Entity entity)
                         {
                             var e = _linkedEntityRepository?.Get(GetInstanceId());
                             return e.Equals(entity);
                         }

                         {{eventHandlersCode}}

                         {{exitTreeCode}}
                     }
""";
        return code;
    }

    private static string CreateEventHandler(Compilation compilation, MethodDeclarationSyntax method)
    {
        var semanticModel = compilation.GetSemanticModel(method.SyntaxTree);
        var methodSymbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

        var methodParameters = GetMethodParameters(methodSymbol);
        var methodName = method.Identifier.Text;

        // methodParameters = methodParameters.RemoveAll(p => p.Name == "Entity");
        if (methodParameters.Length == 0)
            return CreateEventHandlerMethod(methodName);

        var parameters = new StringBuilder();
        foreach (var (name, type) in methodParameters)
        {
            parameters.Append($"{methodName.FirstCharToLower()}.{name.FirstCharToUpper()},");
        }
        parameters.Remove(parameters.Length - 1, 1);

        return CreateEventHandlerMethod(methodName, parameters.ToString());
    }

    private static string CreateEventHandlerMethod(string methodName, string parameters)
    {
        var code = $$"""
                     private void {{methodName}}Internal({{methodName}} {{methodName.FirstCharToLower()}})
                     {
                         if (CanHandleEvent({{methodName.FirstCharToLower()}}.Entity))
                         {
                             {{methodName}}({{parameters}});
                         }
                     }
                     """;
        return code;
    }

    private static string CreateEventHandlerMethod(string methodName)
    {
        var code = $$"""
                     private void {{methodName}}Internal({{methodName}} {{methodName.FirstCharToLower()}})
                     {
                         if (CanHandleEvent({{methodName.FirstCharToLower()}}.Entity))
                         {
                             {{methodName}}();
                         }
                     }
                     """;
        return code;
    }

    private static ImmutableArray<(string Name, string Type)> GetMethodParameters(IMethodSymbol methodSymbol)
    {
        return methodSymbol.Parameters
            .Select(param => (param.Name, param.Type.ToDisplayString()))
            .ToImmutableArray();
    }
}
