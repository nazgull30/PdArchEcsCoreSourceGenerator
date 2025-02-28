namespace EcsCodeGen.Command;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class CreateCommandTemplate
{
    public static (string, ISet<string>) Generate(StructDeclarationSyntax stx, SemanticModel semanticModel)
    {
        var structSymbol = semanticModel.GetDeclaredSymbol(stx) ?? throw new ArgumentException("structSymbol is null");

        var methodName = $"{structSymbol.Name}".Replace("Cmd", "").Replace("Command", "");

        var namespaces = new HashSet<string> { structSymbol.ContainingNamespace.ToDisplayString() };

        var parameters = new StringBuilder();
        var bodyLines = new StringBuilder();

        foreach (var field in stx.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldTypeSyntax = field.Declaration.Type;
                var fieldTypeSymbol = semanticModel.GetTypeInfo(fieldTypeSyntax).Type;

                var fieldName = variable.Identifier.Text;
                var fieldType = fieldTypeSymbol.ToDisplayString();

                namespaces.Add(fieldTypeSymbol.ContainingNamespace.ToDisplayString());

                var paramName = fieldName.FirstCharToLower();
                parameters.Append(fieldType).Append(' ').Append(paramName).Append(", ");

                bodyLines.Append($"command.{fieldName} = {paramName};").Append('\n');

            }
        }

        if (parameters.Length > 2)
        {
            parameters.Remove(parameters.Length - 2, 2);
        }

        var methodDeclaration = parameters.Length > 0
            ? $"public static void {methodName}(this ICommandBuffer commandBuffer, {parameters})"
            : $"public static void {methodName}(this ICommandBuffer commandBuffer)";


        var code = $$"""
			             {{methodDeclaration}}
			             {
			             	ref var command = ref commandBuffer.Create<{{structSymbol.Name}}>();
			             	{{bodyLines}}
			             }
			             """;

        return (code, namespaces);
    }

    // public static void SelectSkin(this ICommandBuffer commandBuffer, ESkinId skin)
    // {
    // 	ref var command = ref commandBuffer.Create<SelectSkinCommand>();
    // 	command.Skin = skin;
    // }
}
