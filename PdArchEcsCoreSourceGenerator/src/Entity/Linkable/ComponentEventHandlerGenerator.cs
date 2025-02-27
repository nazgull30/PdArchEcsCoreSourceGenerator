using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EcsCodeGen.Entity.Linkable;

[Generator]
public class ComponentEventHandlerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax classDecl,
                // classDecl.AttributeLists.Count > 0,
                transform: (context, _) => (context.Node as ClassDeclarationSyntax, context.SemanticModel))
            .Where(pair => ImplementsILinkable(pair.Item1, pair.Item2))
            .Collect();

        var combined = classDeclarations.Combine(compilationProvider);

        context.RegisterSourceOutput(combined, (context, source) => GenerateCode(context, source.Left, source.Right));
    }

    public static List<MethodDeclarationSyntax> GetEventHandlers(ClassDeclarationSyntax syntax, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(syntax);
        if (classSymbol == null)
            throw new ArgumentException("structSymbol is null");

        var eventHandlers = new List<MethodDeclarationSyntax>();

        foreach (var method in syntax.Members.OfType<MethodDeclarationSyntax>())
        {
            var isComponentEventHandler = MatchesWithComponentEventHandler(method);
            if (isComponentEventHandler)
            {
                eventHandlers.Add(method);
            }
        }

        return eventHandlers;
    }

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(ClassDeclarationSyntax, SemanticModel)> classes, Compilation compilation)
    {
        foreach (var (ctx, semanticModel) in classes)
        {
            var eventHandlerClassCode = ComponentEventHandlerClassTemplate.Generate(compilation, ctx, semanticModel);
            context.AddSource($"EcsCodeGen.Linkable.Handler/{ctx.Identifier}ComponentEventHandler.g.cs", eventHandlerClassCode.FormatCode());
        }
    }

    private static bool MatchesWithComponentEventHandler(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.Text;
        var properEnded = name.EndsWith("Added") || name.EndsWith("Removed") || name.EndsWith("Changed");
        return name.StartsWith("On") && properEnded;
    }

    private bool ImplementsILinkable(ClassDeclarationSyntax ctx, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(ctx) as INamedTypeSymbol;

        while (classSymbol != null)
        {
            if (classSymbol.AllInterfaces.Any(i => i.ToDisplayString() == "Core.Utils.ILinkable"))
            {
                return true;
            }
            classSymbol = classSymbol.BaseType;
        }
        return false;
    }
}
