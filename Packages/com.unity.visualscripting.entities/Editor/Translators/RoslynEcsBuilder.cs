using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    [PublicAPI]
    static class RoslynEcsBuilder
    {
        internal static QualifiedNameSyntax StaticConstant(ITypeMetadata type, string identifier)
        {
            return QualifiedName(
                IdentifierName(type.Name),
                IdentifierName(identifier));
        }

        internal static StructDeclarationSyntax DeclareComponent(string componentName, Type componentType,
            IEnumerable<MemberDeclarationSyntax> members = null)
        {
            return StructDeclaration(componentName)
                .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
                .WithBaseList(
                    BaseList(
                        SingletonSeparatedList<BaseTypeSyntax>(
                            SimpleBaseType(TypeSystem.BuildTypeSyntax(componentType)))))
                .WithMembers(List(members));
        }
    }
}
