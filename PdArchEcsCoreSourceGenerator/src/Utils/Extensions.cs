namespace EcsCodeGen.Utils;

using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public static class Extensions
{
    public static bool HasAttribute<T>(this MemberInfo type)
        where T : Attribute
        => type.GetCustomAttribute<T>() != null;

    public static string FirstCharToLower(this string source) =>
        string.Concat(source[..1].ToLowerInvariant(), source.AsSpan(1));

    public static string FirstCharToUpper(this string source) =>
        string.Concat(source[..1].ToUpperInvariant(), source.AsSpan(1));

    public static string FromDotNetTypeToCSharpType(this string dotNetTypeName, bool isNull = false)
    {
        var cstype = "";
        var nullable = isNull ? "?" : "";
        var prefix = "System.";
        var typeName = dotNetTypeName.StartsWith(prefix) ? dotNetTypeName[prefix.Length..] : dotNetTypeName;

        cstype = typeName switch
        {
            "Boolean" => "bool",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "Char" => "char",
            "Decimal" => "decimal",
            "Double" => "double",
            "Single" => "float",
            "Int32" => "int",
            "UInt32" => "uint",
            "Int64" => "long",
            "UInt64" => "ulong",
            "Object" => "object",
            "Int16" => "short",
            "UInt16" => "ushort",
            "String" => "string",
            _ => typeName,
        };
        return $"{cstype}{nullable}";
    }

    public static string FormatCode(this string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var formattedRoot = root.NormalizeWhitespace();

        return formattedRoot.ToFullString();
    }
}
