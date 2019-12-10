using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace UnityEditor.VisualScripting.ComponentEditor
{
    class FileModel
    {
        Microsoft.CodeAnalysis.SyntaxTree Tree { get; }

        public List<StructModel> Structs { get; }

        internal FileModel(Microsoft.CodeAnalysis.SyntaxTree tree, ParseOptions options)
        {
            Tree = tree;
            var structs = tree.GetCompilationUnitRoot().DescendantNodes(n => !(n is StructDeclarationSyntax)).OfType<StructDeclarationSyntax>();
            var classes = tree.GetCompilationUnitRoot().DescendantNodes(n => !(n is ClassDeclarationSyntax)).OfType<ClassDeclarationSyntax>();
            Structs = new List<StructModel>();
            foreach (var s in structs)
            {
                var matchingProxy = classes.SingleOrDefault(c => c.Identifier.Text == StructModel.MakeProxyName(s.Identifier.Text));
                var model = ParseStruct(s, matchingProxy);
                if (model != null)
                    Structs.Add(model);
            }

            if (Structs.Count > 1 && options == ParseOptions.DisallowMultipleStructs)
                throw new InvalidOperationException("File contains multiple component definitions");
        }

        static StructModel ParseStruct(StructDeclarationSyntax s, ClassDeclarationSyntax matchingProxy)
        {
            bool isObsolete = false;
            bool isEditable = false;

            var allAttributes = s.AttributeLists.SelectMany(al => al.Attributes);
            foreach (var attribute in allAttributes)
            {
                isObsolete |= attribute.Name.ToString() == "Obsolete";
                isEditable |= attribute.Name.ToString() == "ComponentEditor";
            }

            if (isObsolete || !isEditable)
                return null;

            if (s.BaseList != null)
                foreach (var baseTypeSyntax in s.BaseList.Types.OfType<SimpleBaseTypeSyntax>())
                {
                    var t = StructModel.ParseType(baseTypeSyntax, s.AttributeLists.SelectMany(l => l.Attributes));
                    if (t != StructType.Unknown)
                        return new StructModel(s, t, matchingProxy);
                }

            return null;
        }

        public enum ParseOptions
        {
            AllowMultipleStructs,
            DisallowMultipleStructs,
        }

        public static FileModel Parse(string text, ParseOptions options)
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            return new FileModel(tree, options);
        }

        public string Generate()
        {
            IEnumerable<MemberDeclarationSyntax> CollectionSelector(StructModel s)
            {
                if (s.InitNode == null)
                    yield break;
                yield return s.InitNode;
                if (s.MatchingProxy != null)
                    yield return s.MatchingProxy;
            }

            CompilationUnitSyntax replaced = Tree.GetCompilationUnitRoot().ReplaceNodes(
                Structs.SelectMany(CollectionSelector),
                (initial, updated) =>
                {
                    if (initial is StructDeclarationSyntax structDecl)
                    {
                        var str = Structs.Single(s => s.InitNode == structDecl);
                        return str.Generate().NormalizeWhitespace();
                    }

                    if (initial is ClassDeclarationSyntax proxyDecl)
                    {
                        var str = Structs.Single(s => s.MatchingProxy == proxyDecl);
                        return str.GenerateProxy();
                    }

                    throw new InvalidOperationException();
                });

            replaced = replaced.AddMembers(Structs
                .Where(s => s.InitNode != null && s.MatchingProxy == null)
                .Select(s => (MemberDeclarationSyntax)s.GenerateProxy())
                .Where(proxy => proxy != null)
                .ToArray());
            replaced = replaced.AddMembers(Structs
                .Where(s => s.InitNode == null)
                .SelectMany(s => s.GenerateStructAndProxy()).ToArray());
            var adHocWorkspace = new AdhocWorkspace();

            var options = adHocWorkspace.Options
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, true)
                .WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, false)
                .WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, true)
            ;

            return Formatter.Format(replaced.SyntaxTree.GetCompilationUnitRoot(), adHocWorkspace, options).GetText().ToString();
        }

        public static CompilationUnitSyntax MakeCompilationUnit()
        {
            return SyntaxFactory.CompilationUnit()
                .WithUsings(
                    SyntaxFactory.List(
                        new[]
                        {
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.IdentifierName("System")),
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("System"),
                                    SyntaxFactory.IdentifierName("ComponentModel"))),
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("Unity"),
                                    SyntaxFactory.IdentifierName("Entities"))),
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.IdentifierName("UnityEngine")),
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.QualifiedName(
                                        SyntaxFactory.IdentifierName("VisualScripting"),
                                        SyntaxFactory.IdentifierName("Entities")),
                                    SyntaxFactory.IdentifierName("Runtime"))),
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.QualifiedName(
                                        SyntaxFactory.IdentifierName("System"),
                                        SyntaxFactory.IdentifierName("Collections")),
                                    SyntaxFactory.IdentifierName("Generic")))
                        }));
        }
    }
}
