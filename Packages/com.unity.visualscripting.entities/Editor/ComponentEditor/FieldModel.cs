using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.ComponentEditor
{
    class FieldModel
    {
        public bool HideInInspector;
        string m_Name;
        public Type Type { get; set; }

        public string Name
        {
            get => m_Name;
            set
            {
                m_Name = value;
                CodeName = TypeSystem.CodifyString(Name);
            }
        }

        StructModel Struct { get; }

        public string CodeName { get; private set; }

        public FieldModel(StructModel owner, Type type, string name)
        {
            Assert.IsNotNull(type);
            Struct = owner;
            Type = type;
            Name = name;
        }

        public static FieldModel Parse(StructModel structModel, FieldDeclarationSyntax arg)
        {
            var typeString = arg.Declaration.Type.ToString();
            var token = ParseToken(typeString);

            bool convert = false;
            bool hideInInspector = false;
            foreach (var attribute in arg.AttributeLists.SelectMany(a => a.Attributes))
            {
                switch (attribute.Name.ToString())
                {
                    case "Convert": convert = true; break;
                    case "HideInInspector": hideInInspector = true; break;
                }
            }
            var type = TypeSyntaxFactory.KindToType(token.Kind()) ?? GetTypeWithHint(typeString, convert);
            return type == null ? null : new FieldModel(structModel, type, arg.Declaration.Variables.Single().Identifier.Text){HideInInspector = hideInInspector};
        }

        static Type GetTypeWithHint(string typeString, bool convert)
        {
            var componentType = ComponentEditor.ComponentTypeCache.TryGetValue(typeString, out var t) ? t : Type.GetType(typeString);
            return convert ? StructModel.GetSourceType(componentType) : componentType;
        }

        public FieldDeclarationSyntax Generate()
        {
            var convertedType = StructModel.GetConvertedType(this);
            var fieldDeclarationSyntax = FieldDeclaration(
                VariableDeclaration(
                    convertedType?.ToTypeSyntax() ?? PredefinedType(Token(SyntaxKind.IntKeyword)),
                    SingletonSeparatedList(VariableDeclarator(CodeName))))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)));
            if (HideInInspector)
                fieldDeclarationSyntax = fieldDeclarationSyntax.WithAttributeLists(SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("HideInInspector"))))));
            return fieldDeclarationSyntax;
        }

        public override string ToString()
        {
            return $"{nameof(Type)}: {Type}, {nameof(Name)}: {Name}";
        }

        public void RemoveFromStruct()
        {
            var structFields = Struct.Fields;
            var i = structFields.IndexOf(this);
            structFields.RemoveAt(i);
        }
    }
}
