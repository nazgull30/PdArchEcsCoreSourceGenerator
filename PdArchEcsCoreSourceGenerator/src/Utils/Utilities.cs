namespace EcsCodeGen.Utils;

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class Utilities
{
    public static bool HasAttribute(string attributeName, TypeDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(declaration);
        if (symbol == null)
        {
            return false;
        }

        return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
    }

    public static bool ImplementsInterface(string interfaceFullName, TypeDeclarationSyntax ctx, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(ctx) as INamedTypeSymbol;

        while (classSymbol != null)
        {
            if (classSymbol.AllInterfaces.Any(i => i.ToDisplayString() == interfaceFullName))
            {
                return true;
            }
            classSymbol = classSymbol.BaseType;
        }
        return false;
    }
}
