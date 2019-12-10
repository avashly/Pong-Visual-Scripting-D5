using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine;
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.ComponentEditor
{
    class StructModel : IEnumerable
    {
        List<FieldModel> m_Fields;

        string m_Name;
        string m_CodeName;

        public StructType Type;
        public ClassDeclarationSyntax MatchingProxy { get; }
        public StructDeclarationSyntax InitNode { get; }

        public List<FieldModel> Fields => m_Fields;

        public string Name
        {
            get => m_Name;
            set
            {
                m_Name = value;
                m_CodeName = TypeSystem.CodifyString(value);
            }
        }

        public StructModel(string name, StructType type, List<FieldModel> fields = null)
        {
            Name = name;
            Type = type;
            m_Fields = fields ?? new List<FieldModel>();
        }

        public StructModel(StructDeclarationSyntax decl, StructType structType, ClassDeclarationSyntax matchingProxy)
        {
            InitNode = decl;
            Name = decl.Identifier.Text;

            Type = structType;
            MatchingProxy = matchingProxy;
            m_Fields = decl.Members.OfType<FieldDeclarationSyntax>().Select(f => FieldModel.Parse(this, f))
                .Where(f => f != null).ToList();
        }

        public static StructType ParseType(SimpleBaseTypeSyntax syntax, IEnumerable<AttributeSyntax> attributes)
        {
            var fullString = syntax.Type.ToString();
            switch (fullString)
            {
                case nameof(IComponentData): return StructType.Component;
                case nameof(IBufferElementData):
                    if (attributes.Any(a => a.Name.ToString() == nameof(DotsEventAttribute)))
                        return StructType.Event;
                    return StructType.BufferElement;
                case nameof(ISharedComponentData): return StructType.SharedComponent;
                default:
                    return StructType.Unknown;
            }
        }

        static TypeSyntax ToBaseTypeSyntax(StructType type)
        {
            switch (type)
            {
                case StructType.Component:
                    return IdentifierName(nameof(IComponentData));
                case StructType.SharedComponent:
                    return IdentifierName(nameof(ISharedComponentData));
                case StructType.Event:
                case StructType.BufferElement:
                    return IdentifierName(nameof(IBufferElementData));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public StructDeclarationSyntax Generate()
        {
            var attributes = new List<AttributeSyntax>
            {
                Attribute(IdentifierName("Serializable")),
                Attribute(IdentifierName("ComponentEditor")),
            };
            if (Type == StructType.Event)
                attributes.Add(Attribute(IdentifierName("DotsEventAttribute")));

            var codeName = m_CodeName;
            var structDeclarationSyntax = StructDeclaration(codeName)
                .AddAttributeLists(AttributeList(SeparatedList(attributes)))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithMembers(List<MemberDeclarationSyntax>(Fields.Select(f => f.Generate())))
                .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(ToBaseTypeSyntax(Type)))));

            if (Type == StructType.SharedComponent)
                structDeclarationSyntax = structDeclarationSyntax.AddBaseListTypes(SimpleBaseType(
                    GenericName(
                        Identifier("IEquatable"))
                        .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(
                                IdentifierName(codeName))))))
                    .AddMembers(MakeEquatableEquals())
                    .AddMembers(MakeEquatableHashCode());

            return structDeclarationSyntax;
        }

        MemberDeclarationSyntax MakeEquatableHashCode()
        {
            var statements = new StatementSyntax[Fields.Count + 2]; // each field + 'int hash = 0;' + 'return hash;'
            statements[0] = RoslynBuilder.DeclareLocalVariable(typeof(int), "hash", LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

            for (var index = 0; index < Fields.Count; index++)
            {
                var field = Fields[index];

                // hash ^= field.GetHashCode();
                var fieldName = field.CodeName;
                var xorHashStatement = ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.ExclusiveOrAssignmentExpression,
                        IdentifierName("hash"),
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(fieldName),
                                IdentifierName("GetHashCode")))));
                if (field.Type.IsValueType)
                    statements[index + 1] = xorHashStatement;
                else // if(!ReferenceEquals(field, null)) hash ^= field.GetHashCode();
                    statements[index + 1] = IfStatement(
                        PrefixUnaryExpression(
                            SyntaxKind.LogicalNotExpression,
                            InvocationExpression(
                                IdentifierName("ReferenceEquals"))
                                .WithArgumentList(
                                ArgumentList(
                                    SeparatedList(
                                        new[]
                                        {
                                            Argument(
                                                IdentifierName(fieldName)),
                                            Argument(
                                                LiteralExpression(
                                                    SyntaxKind.NullLiteralExpression))
                                        })))), xorHashStatement);
            }

            statements[statements.Length - 1] = ReturnStatement(IdentifierName("hash"));
            var method = RoslynBuilder.DeclareMethod("GetHashCode", AccessibilityFlags.Public | AccessibilityFlags.Override, typeof(int))
                .WithBody(Block(statements));
            return method;
        }

        MemberDeclarationSyntax MakeEquatableEquals()
        {
            ExpressionSyntax equalsExpression;

            if (Fields.Count == 0)
                equalsExpression = LiteralExpression(SyntaxKind.TrueLiteralExpression);
            else
            {
                var fstExp = CompareOneField(Fields[0]);

                equalsExpression = Fields.Skip(1).Select(CompareOneField).Aggregate(fstExp,
                    (a, b) => BinaryExpression(SyntaxKind.LogicalAndExpression, a, b));
            }

            var method = RoslynBuilder.DeclareMethod("Equals", AccessibilityFlags.Public, typeof(bool))
                .AddParameterListParameters(Parameter(Identifier("other")).WithType(IdentifierName(m_CodeName)))
                .AddBodyStatements(ReturnStatement(equalsExpression));
            return method;

            BinaryExpressionSyntax CompareOneField(FieldModel f)
            {
                return BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    IdentifierName(f.CodeName),
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("other"),
                        IdentifierName(f.CodeName)));
            }
        }

        public void RemoveFieldAt(int i)
        {
            m_Fields.RemoveAt(i);
        }

        public FieldModel Add(Type type, string name)
        {
            var fieldModel = new FieldModel(this, type, name);
            m_Fields.Add(fieldModel);
            return fieldModel;
        }

        public IEnumerator GetEnumerator()
        {
            return m_Fields.GetEnumerator();
        }

        public ClassDeclarationSyntax GenerateProxy()
        {
            switch (Type)
            {
                case StructType.Unknown:
                case StructType.BufferElement:
                case StructType.Event:
                    return null;
            }

            var simpleBaseTypeSyntaxs = new[]
            {
                SimpleBaseType(IdentifierName(nameof(MonoBehaviour))),
                SimpleBaseType(IdentifierName(nameof(IConvertGameObjectToEntity))),
                SimpleBaseType(IdentifierName(nameof(IDeclareReferencedPrefabs))),
            };
            return ClassDeclaration(ProxyName())
                .WithBaseList(
                BaseList(
                    SeparatedList<BaseTypeSyntax>(simpleBaseTypeSyntaxs)))
                .WithAttributeLists(
                    SingletonList(
                        AttributeList(
                            SingletonSeparatedList(
                                Attribute(
                                    IdentifierName("AddComponentMenu"))
                                    .WithArgumentList(
                                    AttributeArgumentList(
                                        SingletonSeparatedList(
                                            AttributeArgument(
                                                LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression,
                                                    Literal($"Visual Scripting Components/{Name}"))))))))))
                .AddMembers(ProxyMembers().ToArray());
        }

        IEnumerable<MemberDeclarationSyntax> ProxyMembers()
        {
            var declareReferencedPrefabs = new List<StatementSyntax>();
            var initComponentExpressions = new List<ExpressionSyntax>();

            foreach (var field in Fields)
            {
                Type convertedType = GetConvertedType(field);

                // declare matching field in proxy
                FieldDeclarationSyntax fieldDeclarationSyntax = RoslynBuilder.DeclareField(field.Type, field.CodeName, AccessibilityFlags.Public);
                if (field.HideInInspector)
                    fieldDeclarationSyntax = fieldDeclarationSyntax.WithAttributeLists(SingletonList(AttributeList(SingletonSeparatedList(Attribute(IdentifierName("HideInInspector"))))));

                yield return fieldDeclarationSyntax;
                // init component field during conversion

                ExpressionSyntax assignment = field.Type != convertedType
                    ? MakeSpecialConversionAssignment(field, convertedType)
                    : AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(field.CodeName), IdentifierName(field.CodeName));
                initComponentExpressions.Add(assignment);

                // if it's a prefab, declare it
                if (field.Type == typeof(GameObject))
                    declareReferencedPrefabs.Add(ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("referencedPrefabs"),
                                IdentifierName("Add")))
                            .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(
                                        IdentifierName(field.CodeName)))))));
            }

            yield return RoslynBuilder.DeclareMethod("Convert", AccessibilityFlags.Public, typeof(void))
                .WithParameterList(ParameterList(SeparatedList(new[]
                {
                    Parameter(Identifier("entity")).WithType(typeof(Entity).ToTypeSyntax()),
                    Parameter(Identifier("dstManager")).WithType(typeof(EntityManager).ToTypeSyntax()),
                    Parameter(Identifier("conversionSystem")).WithType(typeof(GameObjectConversionSystem).ToTypeSyntax()),
                })))
                .WithBody(Block(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("dstManager"),
                            IdentifierName(Type == StructType.Component ? "AddComponentData" : "AddSharedComponentData")))
                        .WithArgumentList(
                        ArgumentList(
                            SeparatedList(
                                new[]
                                {
                                    Argument(IdentifierName("entity")),
                                    Argument(ObjectCreationExpression(
                                        IdentifierName(m_CodeName))
                                            .WithInitializer(
                                            InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                                                SeparatedList(initComponentExpressions)))
                                    )
                                }))))));

            yield return RoslynBuilder.DeclareMethod("DeclareReferencedPrefabs", AccessibilityFlags.Public, typeof(void))
                .WithParameterList(ParameterList(SeparatedList(new[]
                {
                    Parameter(Identifier("referencedPrefabs")).WithType(typeof(List<GameObject>).ToTypeSyntax()),
                })))
                .WithBody(Block(declareReferencedPrefabs));
        }

        static ExpressionSyntax MakeSpecialConversionAssignment(FieldModel field, Type convertedType)
        {
            Assert.AreEqual(field.Type, typeof(GameObject)); // only fancy type supported ATM
            Assert.AreEqual(convertedType, typeof(Entity)); // only fancy type supported ATM
            return AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(field.CodeName),
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("conversionSystem"),
                        IdentifierName("GetPrimaryEntity")))
                    .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                IdentifierName(field.CodeName))))));
        }

        public static Type GetSourceType(Type t)
        {
            return t == typeof(Entity) ? typeof(GameObject) : t;
        }

        public static Type GetConvertedType(FieldModel field)
        {
            return field.Type == typeof(GameObject) ? typeof(Entity) : field.Type;
        }

        public IEnumerable<MemberDeclarationSyntax> GenerateStructAndProxy()
        {
            yield return Generate();
            var proxy = GenerateProxy();
            if (proxy != null)
                yield return proxy;
        }

        public string ProxyName()
        {
            return MakeProxyName(m_CodeName);
        }

        public static string MakeProxyName(string name)
        {
            return $"{name}Proxy";
        }
    }
}
