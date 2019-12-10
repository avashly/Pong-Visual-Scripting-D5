using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Translators
{
    public class CriteriaTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void TestSingleCriteriaModel([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var query = CreateComponentQuery(graphModel, typeof(DummyFloat3Component));
                var onUpdateEntities = CreateOnUpdateAndConnectQuery(graphModel, query);

                // Add criteria
                var criterion = GetDummyFloat3ValueCriterion();
                var criteria = new CriteriaModel();
                criteria.UniqueNameProvider = onUpdateEntities;
                criteria.Name = "cFloat3";
                criteria.GraphModel = graphModel;
                criteria.AddCriterionNoUndo(graphModel, criterion);
                onUpdateEntities.AddCriteriaModelNoUndo(criteria);

                AddSharedComponentIfCriteriaMatch(graphModel, onUpdateEntities);
            },
                (manager, entityIndex, e) =>
                {
                    var x = entityIndex % 2 == 0 ? 0f : 25f;
                    manager.AddComponentData(e, new DummyFloat3Component {Value = new float3 { x = x }});
                },
                (manager, entityIndex, e) =>
                {
                    var x = manager.GetComponentData<DummyFloat3Component>(e).Value.x;
                    if (x > 10f)
                        Assert.That(!manager.HasComponent<DummySharedComponent>(e));
                    else
                        Assert.That(manager.HasComponent<DummySharedComponent>(e));
                });
        }

        [Test]
        public void TestComplexTypeEqualityCriteriaModel([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var query = CreateComponentQuery(graphModel, typeof(DummyFloat3Component));
                var onUpdateEntities = CreateOnUpdateAndConnectQuery(graphModel, query);

                // Add criteria
                var criterion = GetDummyFloat3Criterion(BinaryOperatorKind.Equals);
                var criteria = new CriteriaModel();
                criteria.Name = "cFloat3";
                criteria.AddCriterionNoUndo(graphModel, criterion);
                onUpdateEntities.AddCriteriaModelNoUndo(criteria);

                AddSharedComponentIfCriteriaMatch(graphModel, onUpdateEntities);
            },
                (manager, entityIndex, e) =>
                {
                    var v = entityIndex % 2 == 0 ? 0f : 20f;
                    manager.AddComponentData(e, new DummyFloat3Component { Value = new float3(v) });
                },
                (manager, entityIndex, e) =>
                {
                    var x = manager.GetComponentData<DummyFloat3Component>(e).Value.x;
                    if (x > 10f)
                        Assert.That(manager.HasComponent<DummySharedComponent>(e));
                    else
                        Assert.That(!manager.HasComponent<DummySharedComponent>(e));
                });
        }

        [Test]
        public void TestComplexTypeComparisonCriteriaModel([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var query = CreateComponentQuery(graphModel, typeof(DummyFloat3Component));
                var onUpdateEntities = CreateOnUpdateAndConnectQuery(graphModel, query);

                // Add criteria
                var criterion = GetDummyFloat3Criterion(BinaryOperatorKind.GreaterThan);
                var criteria = new CriteriaModel();
                criteria.Name = "cFloat3";
                criteria.AddCriterionNoUndo(graphModel, criterion);
                onUpdateEntities.AddCriteriaModelNoUndo(criteria);

                AddSharedComponentIfCriteriaMatch(graphModel, onUpdateEntities);
            },
                (manager, entityIndex, e) =>
                {
                    var v = entityIndex % 2 == 0 ? 0f : 10f;
                    manager.AddComponentData(e, new DummyFloat3Component { Value = new float3(v) });
                },
                (manager, entityIndex, e) =>
                {
                    var x = manager.GetComponentData<DummyFloat3Component>(e).Value.x;
                    if (x > 10f)
                        Assert.That(manager.HasComponent<DummySharedComponent>(e));
                    else
                        Assert.That(!manager.HasComponent<DummySharedComponent>(e));
                });
        }

        [Test]
        public void TestSingleCriteriaModel_ManyCriterion([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var query = CreateComponentQuery(graphModel, typeof(DummyFloat3Component), typeof(DummyBoolComponent));
                var onUpdateEntities = CreateOnUpdateAndConnectQuery(graphModel, query);

                // Add criteria
                var f3Criterion = GetDummyFloat3ValueCriterion();
                var boolCriterion = GetDummyBoolCriterion();
                var criteria = new CriteriaModel();
                criteria.UniqueNameProvider = onUpdateEntities;
                criteria.GraphModel = graphModel;
                criteria.Name = "criteria";
                criteria.AddCriterionNoUndo(graphModel, f3Criterion);
                criteria.AddCriterionNoUndo(graphModel, boolCriterion);
                onUpdateEntities.AddCriteriaModelNoUndo(criteria);

                AddSharedComponentIfCriteriaMatch(graphModel, onUpdateEntities);
            },
                (manager, entityIndex, e) =>
                {
                    var x = entityIndex % 2 == 0 ? 0f : 25f;
                    manager.AddComponentData(e, new DummyFloat3Component { Value = new float3 { x = x }});

                    var b = entityIndex % 4 == 0;
                    manager.AddComponentData(e, new DummyBoolComponent { Value = b });
                },
                (manager, entityIndex, e) =>
                {
                    var x = manager.GetComponentData<DummyFloat3Component>(e).Value.x;
                    var b = manager.GetComponentData<DummyBoolComponent>(e).Value;

                    if (x < 10f && b)
                        Assert.That(manager.HasComponent<DummySharedComponent>(e));
                    else
                        Assert.That(!manager.HasComponent<DummySharedComponent>(e));
                });
        }

        [Test]
        public void TestMultipleCriteriaModel([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var query = CreateComponentQuery(graphModel, typeof(DummyFloat3Component), typeof(DummyBoolComponent));
                var onUpdateEntities = CreateOnUpdateAndConnectQuery(graphModel, query);

                // Add criteria
                var f3Criterion = GetDummyFloat3ValueCriterion();
                var cFloat3 = new CriteriaModel();
                cFloat3.UniqueNameProvider = onUpdateEntities;
                cFloat3.GraphModel = graphModel;
                cFloat3.Name = "cFloat3";
                cFloat3.AddCriterionNoUndo(graphModel, f3Criterion);
                onUpdateEntities.AddCriteriaModelNoUndo(cFloat3);

                var boolCriterion = GetDummyBoolCriterion();
                var cBool = new CriteriaModel();
                cBool.UniqueNameProvider = onUpdateEntities;
                cBool.Name = "cBool";
                cBool.GraphModel = graphModel;
                cBool.AddCriterionNoUndo(graphModel, boolCriterion);
                onUpdateEntities.AddCriteriaModelNoUndo(cBool);

                AddSharedComponentIfCriteriaMatch(graphModel, onUpdateEntities);
            },
                (manager, entityIndex, e) =>
                {
                    var x = entityIndex % 2 == 0 ? 0f : 25f;
                    manager.AddComponentData(e, new DummyFloat3Component { Value = new float3 { x = x }});

                    var b = entityIndex % 3 == 0;
                    manager.AddComponentData(e, new DummyBoolComponent { Value = b });
                },
                (manager, entityIndex, e) =>
                {
                    var x = manager.GetComponentData<DummyFloat3Component>(e).Value.x;
                    var b = manager.GetComponentData<DummyBoolComponent>(e).Value;

                    if (x < 10f || b)
                        Assert.That(manager.HasComponent<DummySharedComponent>(e));
                    else
                        Assert.That(!manager.HasComponent<DummySharedComponent>(e));
                });
        }

        static ComponentQueryDeclarationModel CreateComponentQuery(VSGraphModel graphModel, params Type[] types)
        {
            var query = graphModel.CreateComponentQuery("g");

            foreach (var type in types)
            {
                var handle = type.GenerateTypeHandle(graphModel.Stencil);
                query.AddComponent(graphModel.Stencil, handle, ComponentDefinitionFlags.None);
            }

            return query;
        }

        static OnUpdateEntitiesNodeModel CreateOnUpdateAndConnectQuery(VSGraphModel graphModel, IVariableDeclarationModel query)
        {
            var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
            var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
            graphModel.CreateEdge(onUpdateEntities.InstancePort, queryInstance.OutputPort);

            return onUpdateEntities;
        }

        static void AddSharedComponentIfCriteriaMatch(VSGraphModel graphModel, FunctionModel onUpdateEntities)
        {
            var entityInstance = graphModel.CreateVariableNode(
                onUpdateEntities.FunctionParameterModels.Single(
                    p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                    ),
                Vector2.zero);
            var addComponent = onUpdateEntities.CreateStackedNode<AddComponentNodeModel>("add");
            addComponent.ComponentType = typeof(DummySharedComponent).GenerateTypeHandle(graphModel.Stencil);
            graphModel.CreateEdge(addComponent.EntityPort, entityInstance.OutputPort);
            addComponent.DefineNode();
        }

        Criterion GetDummyFloat3Criterion(BinaryOperatorKind kind)
        {
            var typeMember = new TypeMember(typeof(int).GenerateTypeHandle(Stencil), new List<string> { "Value" });

            Float3ConstantModel value = new Float3ConstantModel();
            value.value = new float3(20f);

            return new Criterion
            {
                ObjectType = typeof(DummyFloat3Component).GenerateTypeHandle(Stencil),
                Member = typeMember,
                Operator = kind,
                Value = value
            };
        }

        Criterion GetDummyFloat3ValueCriterion()
        {
            var typeMember = new TypeMember(typeof(int).GenerateTypeHandle(Stencil), new List<string>
            {
                "Value",
                "x"
            });

            var value = new IntConstantModel();
            value.value = 20;

            return new Criterion
            {
                ObjectType = typeof(DummyFloat3Component).GenerateTypeHandle(Stencil),
                Member = typeMember,
                Operator = BinaryOperatorKind.LessThan,
                Value = value
            };
        }

        Criterion GetDummyBoolCriterion()
        {
            var typeMember = new TypeMember(typeof(bool).GenerateTypeHandle(Stencil), new List<string> { "Value" });

            var value = new BooleanConstantNodeModel();
            value.value = true;

            return new Criterion
            {
                ObjectType = typeof(DummyBoolComponent).GenerateTypeHandle(Stencil),
                Member = typeMember,
                Operator = BinaryOperatorKind.Equals,
                Value = value
            };
        }
    }
}
