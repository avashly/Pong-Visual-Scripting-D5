using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Translators;

namespace UnityEditor.VisualScriptingTests.Extensions
{
    class ReflexionExtensionMethodsTests
    {
        [Test]
        public void TestRoslynEcsBuilderExtensionMethods()
        {
            TestExtensionMethodsAreSameFastAndSlow(mode =>
                ModelUtility.ExtensionMethodCache<RoslynEcsTranslator>.FindMatchingExtensionMethods(RoslynTranslator.FilterMethods, RoslynTranslator.KeySelector, mode));
        }

        static void TestExtensionMethodsAreSameFastAndSlow(Func<ModelUtility.VisitMode, Dictionary<Type, MethodInfo>> getMethodsForMode)
        {
            var foundMethodsSlow = getMethodsForMode(ModelUtility.VisitMode.EveryMethod);
            var foundMethodsFast = getMethodsForMode(ModelUtility.VisitMode.OnlyClassesWithAttribute);
            foreach (var kp in foundMethodsSlow)
            {
                var k = kp.Key;
                var v = kp.Value;
                Assert.That(foundMethodsFast.ContainsKey(k), Is.True, $"No method found for {k.FullName}");
                Assert.That(foundMethodsFast[k], Is.EqualTo(v));
            }
            Assert.That(foundMethodsSlow.Count, Is.EqualTo(foundMethodsFast.Count));
        }
    }
}
