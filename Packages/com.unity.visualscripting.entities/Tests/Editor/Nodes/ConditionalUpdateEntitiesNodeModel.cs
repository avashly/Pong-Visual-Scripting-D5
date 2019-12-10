using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Model.Translators;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    [Serializable]
    public class ConditionalUpdateEntitiesNodeModel : OnEntitiesEventBaseNodeModel
    {
        public bool EnableStackExecution;

        public override UpdateMode Mode => UpdateMode.OnUpdate;
        protected override string MakeTitle()
        {
            return nameof(ConditionalUpdateEntitiesNodeModel);
        }
    }

    [GraphtoolsExtensionMethods]
    public static class ConditionalUpdateEntitiesNodeModelTranslator
    {
        public static IEnumerable<SyntaxNode> BuildOnKeyPressEcs(this RoslynEcsTranslator translator,
            ConditionalUpdateEntitiesNodeModel model, IPortModel portModel)
        {
            return translator.BuildOnEntitiesEventBase(model, MakeConstraint(model));
        }

        public static ExpressionSyntax MakeConstraint(ConditionalUpdateEntitiesNodeModel model)
        {
            return model.EnableStackExecution ? LiteralExpression(SyntaxKind.TrueLiteralExpression) : LiteralExpression(SyntaxKind.FalseLiteralExpression);
        }
    }
}
