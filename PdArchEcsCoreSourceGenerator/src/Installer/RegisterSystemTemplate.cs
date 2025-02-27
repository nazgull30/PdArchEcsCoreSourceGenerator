namespace EcsCodeGen.Installer;

using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.InstallerGenerator.Attributes;

public static class RegisterSystemTemplate
{
    public static string Generate(ClassDeclarationSyntax ctx, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(ctx) ?? throw new ArgumentException("classSymbol is null");
        var isDebug = IsDebug(classSymbol);
        var order = GameEcsSystemsGenerator.GetOrder(classSymbol);

        var sb = new StringBuilder();
        if (isDebug)
        {
            sb.Append("if(isDebug) \n");
        }

        sb.Append($"builder.Register<{classSymbol.Name}>(Lifetime.Singleton).AsImplementedInterfaces(); // {order}");

        return sb.ToString();
    }

    private static bool IsDebug(ISymbol classSymbol)
    {
        return classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name == nameof(DebugSystemAttribute));
    }
}
