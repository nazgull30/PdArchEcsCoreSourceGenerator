namespace EcsCodeGen.Installer;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using EcsCodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdArchEcsCore.InstallerGenerator.Attributes;
using PdArchEcsCore.InstallerGenerator.Enums;

[Generator]
public class GameEcsSystemsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax classDecl &&
                                        classDecl.AttributeLists.Count > 0,
                transform: (context, _) => (context.Node as ClassDeclarationSyntax, context.SemanticModel))
            .Where(pair => Utilities.HasAttribute(nameof(InstallAttribute), pair.Item1, pair.Item2))
            .Collect();

        context.RegisterSourceOutput(structDeclarations, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context,
        ImmutableArray<(ClassDeclarationSyntax, SemanticModel)> classes)
    {
        var sortedClasses = classes.Sort(Comparison).ToList();

        var namespaces = new HashSet<string>();
        var namespacesSb = new StringBuilder();

        foreach (var sortedClass in sortedClasses)
        {
            var classSymbol = sortedClass.Item2.GetDeclaredSymbol(sortedClass.Item1);
            var ns = classSymbol.ContainingNamespace.ToDisplayString();
            namespaces.Add(ns);
        }
        foreach (var ns in namespaces)
        {
            namespacesSb.Append("using ").Append(ns).Append(";\n");
        }

        var urgent = sortedClasses.FindAll(tuple => Match((int)ExecutionPriority.Urgent, tuple));
        var urgentRegisterLines = new StringBuilder();
        urgent.ForEach(t =>
        {
            var lines = RegisterSystemTemplate.Generate(t.Item1, t.Item2);
            urgentRegisterLines.Append(lines).Append("\n");
            ;
        });

        var high = sortedClasses.FindAll(tuple => Match((int)ExecutionPriority.High, tuple));
        var highRegisterLines = new StringBuilder();
        high.ForEach(t =>
        {
            var lines = RegisterSystemTemplate.Generate(t.Item1, t.Item2);
            highRegisterLines.Append(lines).Append("\n");
            ;
        });

        var normal = sortedClasses.FindAll(tuple => Match((int)ExecutionPriority.Normal, tuple));
        var normalRegisterLines = new StringBuilder();
        normal.ForEach(t =>
        {
            var lines = RegisterSystemTemplate.Generate(t.Item1, t.Item2);
            normalRegisterLines.Append(lines).Append("\n");
            ;
        });

        var low = sortedClasses.FindAll(tuple => Match((int)ExecutionPriority.Low, tuple));
        var lowRegisterLines = new StringBuilder();
        low.ForEach(t =>
        {
            var lines = RegisterSystemTemplate.Generate(t.Item1, t.Item2);
            lowRegisterLines.Append(lines).Append("\n");
            ;
        });

        var none = sortedClasses.FindAll(tuple => Match((int)ExecutionPriority.None, tuple));
        var noneRegisterLines = new StringBuilder();
        none.ForEach(t =>
        {
            var lines = RegisterSystemTemplate.Generate(t.Item1, t.Item2);
            noneRegisterLines.Append(lines).Append("\n");
        });

        var code = @$"""

using VContainer;

{namespacesSb}

                     namespace Ecs.Installers {{
                     	public static class GameEcsSystems {{
                     		public static void Install(IContainerBuilder builder, bool isDebug = true){{
                     			Urgent(builder, isDebug);
                     			High(builder, isDebug);
                     			Normal(builder, isDebug);
                     			Low(builder, isDebug);
                     			None(builder, isDebug);
                     		}}

                     		private static void Urgent(IContainerBuilder builder, bool isDebug)
                     		{{
                     			{urgentRegisterLines}
                     		}}

                     		private static void High(IContainerBuilder builder, bool isDebug)
                     		{{
                     			{highRegisterLines}
                     		}}

                     		private static void Normal(IContainerBuilder builder, bool isDebug)
                     		{{
                     			{normalRegisterLines}
                     		}}

                     		private static void Low(IContainerBuilder builder, bool isDebug)
                     		{{
                     			{lowRegisterLines}
                     		}}

                     		private static void None(IContainerBuilder builder, bool isDebug)
                     		{{
                     			{noneRegisterLines}
                     		}}

                     	}}
                     }}
                     """;

        var formattedCode = code.FormatCode();

        context.AddSource($"EcsCodeGen.Installer/GameEcsSystems.g.cs", formattedCode);
    }

    public static int GetOrder(ISymbol classSymbol)
    {
        var installAttribute = classSymbol.GetAttributes().ToList().Find(a => a.AttributeClass?.Name == nameof(InstallAttribute));
        var order = (int)installAttribute.ConstructorArguments[1].Value;
        return order;
    }

    private static int GetExecutionPriority(ISymbol classSymbol)
    {
        var installAttribute = classSymbol.GetAttributes().ToList().Find(a => a.AttributeClass?.Name == nameof(InstallAttribute));
        var priority = (int)installAttribute.ConstructorArguments[0].Value;
        return priority;
    }

    private static int Comparison((ClassDeclarationSyntax, SemanticModel) x, (ClassDeclarationSyntax, SemanticModel) y)
    {
        var classSymbol1 = x.Item2.GetDeclaredSymbol(x.Item1);
        if (classSymbol1 == null)
            throw new ArgumentException("classSymbol1 is null");

        var classSymbol2 = y.Item2.GetDeclaredSymbol(y.Item1);
        if (classSymbol2 == null)
            throw new ArgumentException("classSymbol2 is null");

        var order1 = GetOrder(classSymbol1);
        var order2 = GetOrder(classSymbol2);
        return order1.CompareTo(order2);
    }

    private static bool Match(int priority, (ClassDeclarationSyntax, SemanticModel) tuple)
    {
        var classSymbol = tuple.Item2.GetDeclaredSymbol(tuple.Item1);
        return GetExecutionPriority(classSymbol) == priority;
    }
}
