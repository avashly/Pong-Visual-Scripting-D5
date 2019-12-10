using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine;
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Stencils
{
//    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, k_DefaultRngTitle)]
    [Serializable]
    public class RandomNodeModel : HighLevelNodeModel, IHasMainOutputPort
    {
        internal const string k_DefaultRngTitle = "Random Value";
        internal const string k_DefaultMethodName = "Random Float";

        // TODO: support these types in VS
        static readonly Type[] k_UnsupportedTypes =
        {
            typeof(bool2), typeof(bool3), typeof(bool4),
            typeof(int2), typeof(int3), typeof(int4),
            typeof(uint), typeof(uint2), typeof(uint3), typeof(uint4),
            typeof(double2), typeof(double3), typeof(double4)
        };

        public IPortModel OutputPort { get; private set; }

        public enum ParamVariant
        {
            NoParameters, Max, MinMax
        }

        ParamVariant m_Variant = ParamVariant.NoParameters;
        public ParamVariant Variant
        {
            get => m_Variant;
            set
            {
                m_Variant = value;
                m_RngMethod = null; // force resolve method again
            }
        }

        // NextFloat4(float4 min, float4 max) -> Float4
        string m_MethodBaseName;

        // allows setting the method without changing the kind of parameters
        // So you can switch from NextFloat4(max) to NextDouble(max)
        public string MethodBaseName
        {
            get => m_MethodBaseName;
            set
            {
                m_MethodBaseName = value;
                m_RngMethod = null; // force resolve method again
            }
        }

        MethodInfo m_RngMethod;
        public MethodInfo RngMethod => m_RngMethod ?? (m_RngMethod = ResolveRngMethod());

        static MethodInfo s_DefaultMethod;
        public static MethodInfo DefaultMethod => s_DefaultMethod ?? (s_DefaultMethod = RngByTitle[k_DefaultMethodName]);

        static List<MethodInfo> s_RngMethods;
        public static IReadOnlyList<MethodInfo> RngMethods => s_RngMethods ?? (s_RngMethods = GetRandomMethods());

        static List<string> s_BaseMethodNames;
        public static IReadOnlyList<string> BaseMethodNames => s_BaseMethodNames ?? (s_BaseMethodNames = RngMethods.Where(m => m.GetParameters().Length == 0).Select(GetMethodBase).ToList());

        static Dictionary<Type, List<MethodInfo>> s_RngByType;

        public static Dictionary<Type, List<MethodInfo>> RngByType => s_RngByType ?? (s_RngByType = RngMethods
                .GroupBy(m => m.ReturnType)
                .ToDictionary(g => g.Key, g => g.ToList()));

        static Dictionary<string, MethodInfo> s_RngByTitle;

        static IReadOnlyDictionary<string, MethodInfo> RngByTitle => s_RngByTitle ?? (s_RngByTitle = RngMethods.ToDictionary(MakeTitle));

        protected override void OnDefineNode()
        {
            OutputPort = AddDataOutputPort("output", RngMethod.ReturnType.GenerateTypeHandle(Stencil));
            foreach (var param in RngMethod.GetParameters())
            {
                AddDataInput(param.Name, param.ParameterType.GenerateTypeHandle(Stencil));
            }
        }

        MethodInfo ResolveRngMethod()
        {
            MethodInfo foundMethod = DefaultMethod;
            if (MethodBaseName == null) // node was created by title
            {
                var title = (string.IsNullOrEmpty(Title) || Title == k_DefaultRngTitle) ? k_DefaultMethodName : Title;
                if (RngByTitle.TryGetValue(title, out var method) && method != null)
                {
                    foundMethod = method;
                    ExtractBaseMethodAndVariantFromMethod(foundMethod);
                }
                else
                {
                    Debug.LogError($"Can't init {nameof(RandomNodeModel)} with title '{Title}': no corresponding method found");
                }
            }
            else
            {
                foundMethod = ResolveRngMethod(MethodBaseName, Variant);
                Title = MakeTitle(foundMethod);
            }
            return foundMethod;
        }

        void ExtractBaseMethodAndVariantFromMethod(MethodInfo method)
        {
            var paramCount = method.GetParameters().Length;
            Assert.IsTrue(paramCount >= 0 && paramCount <= 2, "unexpected rng parameters for method " + method.Name);

            if (paramCount == 0)
                Variant = ParamVariant.NoParameters;
            else if (paramCount == 1)
                Variant = ParamVariant.Max;
            else
                Variant = ParamVariant.MinMax;

            MethodBaseName = GetMethodBase(method);
        }

        static List<MethodInfo> GetRandomMethods()
        {
            return typeof(Unity.Mathematics.Random).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.ReturnType != typeof(void)
                    && !k_UnsupportedTypes.Contains(m.ReturnType)
                    && m.Name.StartsWith("Next"))
                .ToList();
        }

        public static string MakeTitle(MethodBase method)
        {
            string title = "Random " + GetMethodBase(method);
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                title = $"{title} ({string.Join(", ", parameters.Select(p => p.Name).ToArray())})";
            }

            return title;
        }

        static string GetMethodBase(MethodBase method)
        {
            return method.Name.Substring(4); // skip "Next" e.g. in NextFloat
        }

        public IPortModel GetPortForParameter(ParameterInfo param)
        {
            return InputsById[param.Name];
        }

        static MethodInfo ResolveRngMethod(string methodBaseName, ParamVariant variant)
        {
            string methodBaseTitle = "Random " + methodBaseName;
            string methodTitle = methodBaseTitle;

            if (variant == ParamVariant.Max)
                methodTitle += " (max)";
            else if (variant == ParamVariant.MinMax)
                methodTitle += " (min, max)";

            if (RngByTitle.TryGetValue(methodTitle, out var method))
                return method;
            if (RngByTitle.TryGetValue(methodBaseTitle, out var baseMethod))
                return baseMethod;
            Debug.LogWarning("error finding Rng method " + methodBaseName + ", falling back to " + MakeTitle(DefaultMethod));
            return DefaultMethod;
        }
    }

    [GraphtoolsExtensionMethods]
    public static class RandomNodeTranslator
    {
        static readonly string k_RngVariableName = "rng";

        public static IEnumerable<SyntaxNode> Build(
            this RoslynEcsTranslator translator,
            RandomNodeModel model,
            IPortModel portModel)
        {
            // var rng = new Unity.Mathematics.Random();
            var randomVariable = RoslynBuilder.DeclareLocalVariable(typeof(Unity.Mathematics.Random), k_RngVariableName, ObjectCreationExpression(
                QualifiedName(
                    QualifiedName(
                        IdentifierName(nameof(Unity)),
                        IdentifierName(nameof(Unity.Mathematics))),
                    IdentifierName(nameof(Unity.Mathematics.Random))))
                    .WithArgumentList(ArgumentList()));
            translator.context.PrependUniqueStatement(k_RngVariableName, randomVariable);
            // rng.InitState(SEED);
            var rngVariable = translator.context.GetCachedValue(k_RngVariableName, IdentifierName(k_RngVariableName), typeof(Unity.Mathematics.Random).GenerateTypeHandle(translator.Stencil));
            var initState = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        rngVariable,
                        IdentifierName(nameof(Unity.Mathematics.Random.InitState))))
                    .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(translator.context.GetRandomSeed())))));
            translator.context.AddStatement(initState);
            // calls randomFloat, defined as float randomFloat = rng.NextFloat();
            yield return CallNextRandom(rngVariable, translator, model);
        }

        static ExpressionSyntax CallNextRandom(ExpressionSyntax rngVariable, RoslynEcsTranslator translator, RandomNodeModel model)
        {
            // rng.NextFloat() / or rng.NextInt(min, max), etc. with min/max coming from ports
            var parameterPorts = model.RngMethod.GetParameters().Select(model.GetPortForParameter);
            translator.BuildArgumentList(parameterPorts, out var argumentList);
            return RoslynBuilder.MethodInvocation(model.RngMethod.Name, rngVariable, argumentList, null);
        }
    }
}
