using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Components/Get Singleton")]
    [Serializable]
    public class GetSingletonNodeModel : HighLevelNodeModel
    {
        [TypeSearcher(typeof(GetSingletonFilter))]
        public TypeHandle ComponentType = TypeHandle.Unknown;

        protected override void OnDefineNode()
        {
            AddDataOutputPort(ComponentType.Name(Stencil), ComponentType);
        }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public static class GetSingletonTranslator
    {
        public static IEnumerable<SyntaxNode> Build(
            this RoslynEcsTranslator translator,
            GetSingletonNodeModel model)
        {
            if (model.ComponentType == TypeHandle.Unknown)
                yield break;

            var singletonType = model.ComponentType.Resolve(translator.Stencil);
            var typeArguments = TypeArgumentList(SingletonSeparatedList(TypeSystem.BuildTypeSyntax(singletonType)));

            var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            var methodInfo = typeof(EntityQuery).GetMethod(nameof(EntityQuery.GetSingleton), bindingFlags)
                ?.GetGenericMethodDefinition()
                    .MakeGenericMethod(singletonType);


            var method = RoslynBuilder.MethodInvocation(
                nameof(EntityQuery.GetSingleton),
                methodInfo,
                InvocationExpression(
                    IdentifierName("GetEntityQuery"))
                    .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(TypeOfExpression(singletonType.ToTypeSyntax()))))),
                new List<ArgumentSyntax>(),
                typeArguments) as ExpressionSyntax;

            yield return method;
        }
    }

    public class GetSingletonFilter : ISearcherFilter
    {
        public SearcherFilter GetFilter(INodeModel model)
        {
            return new SearcherFilter(SearcherContext.Type)
                .WithComponentData(model.GraphModel.Stencil)
                .WithSharedComponentData(model.GraphModel.Stencil);
        }
    }
}
