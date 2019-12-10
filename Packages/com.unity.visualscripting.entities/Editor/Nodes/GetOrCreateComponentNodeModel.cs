using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Components/Get or create")]
    [Serializable]
    public class GetOrCreateComponentNodeModel : HighLevelNodeModel, IHasEntityInputPort, IHasMainOutputPort
    {
        [TypeSearcher]
        public TypeHandle ComponentType;
        public bool CreateIfNeeded;

        public IPortModel EntityPort { get; private set; }

        public IPortModel OutputPort { get; private set; }

        protected override void OnDefineNode()
        {
            EntityPort = AddDataInput<Entity>("entity");
            var outputType = ComponentType.IsValid ? ComponentType : typeof(IComponentData).GenerateTypeHandle(Stencil);
            OutputPort = AddDataOutputPort("", outputType);
        }
    }

    [GraphtoolsExtensionMethods]
    public static class GetComponentTranslator
    {
        public static IEnumerable<SyntaxNode> BuildGetComponent(this RoslynEcsTranslator translator, GetOrCreateComponentNodeModel model, IPortModel portModel)
        {
            var entity = translator.BuildPort(model.EntityPort).First() as ExpressionSyntax;

            TypeSyntax componentType = TypeSystem.BuildTypeSyntax(model.ComponentType.Resolve(translator.Stencil));

            yield return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(nameof(EntityManager)),
                    GenericName(
                        Identifier(model.CreateIfNeeded ? nameof(EcsHelper.GetOrCreateComponentData) : nameof(EntityManager.GetComponentData)))
                        .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList(
                                componentType
                            )
                        )
                        )
                )
            )
                    .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            entity))));
        }
    }
}
