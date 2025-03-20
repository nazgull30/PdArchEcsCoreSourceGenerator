namespace EcsCodeGen.Entity.Linkable;

using EcsCodeGen.Utils;
using global::System;
using global::System.Collections.Generic;
using global::System.Collections.Immutable;
using global::System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;



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

    private static void GenerateCode(SourceProductionContext context,
        ImmutableArray<(ClassDeclarationSyntax, SemanticModel)> classes, Compilation compilation)
    {
        foreach (var (ctx, semanticModel) in classes)
        {
            var eventHandlerClassCode = ComponentEventHandlerClassTemplate.Generate(compilation, ctx, semanticModel);
            context.AddSource($"EcsCodeGen.Linkable.Handler/{ctx.Identifier}ComponentEventHandler.g.cs", eventHandlerClassCode.FormatCode());
        }
    }

    private static bool ImplementsILinkable(ClassDeclarationSyntax ctx, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(ctx) as INamedTypeSymbol;

        while (classSymbol != null)
        {
            if (classSymbol.AllInterfaces.Any(i => i.ToDisplayString() == "PdArchEcsCore.Utils.ILinkable"))
            {
                return true;
            }
            classSymbol = classSymbol.BaseType;
        }
        return false;
    }
}
