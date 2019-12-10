using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests
{
    public class QueryPartTests
    {
        // Basic data container tests (does not test ComponentQuery actions, which are merely wrappers around QueryPart)

        class TestGroup : IEnumerable
        {
            public string Name;
            public List<TestGroup> Groups = new List<TestGroup>();
            public List<string> Components = new List<string>();
            public TestGroup(string name) => Name = name;

            public void Add(string comp) => Components.Add(comp);

            public void Add(TestGroup comp) => Groups.Add(comp);

            public IEnumerator GetEnumerator() => throw new NotImplementedException();

            void DumpRec(StringBuilder sb, int depth)
            {
                sb.AppendLine(new string(' ', depth * 2) + Name);
                foreach (var subgroup in Groups)
                    subgroup.DumpRec(sb, depth + 1);
                foreach (var component in Components)
                    sb.AppendLine(new string(' ', (depth + 1) * 2) + component);
            }

            public string Dump()
            {
                StringBuilder sb = new StringBuilder();
                DumpRec(sb, 0);
                return sb.ToString();
            }
        }

        static void Check(QueryContainer q, TestGroup t)
        {
            void CheckRec(QueryGroup group, TestGroup reference)
            {
                Assert.AreEqual(group.Name, reference.Name);
                List<QueryGroup> subGroups = q.GetSubGroups(group).ToList();
                Assert.AreEqual(subGroups.Count, reference.Groups.Count);
                foreach (var tuple in subGroups.Zip(reference.Groups, (a, b) => (a, b)))
                    CheckRec(tuple.Item1, tuple.Item2);
                var items = q.GetComponentsInQuery(group).ToList();
                Assert.AreEqual(items.Count, reference.Components.Count);
                foreach (var tuple in items.Zip(reference.Components, (a, b) => (a, b))) Assert.AreEqual(tuple.Item1.Component.TypeHandle.Identification, tuple.Item2);
            }

            try
            {
                CheckRec(q.RootGroup, t);

                Debug.Log($"Success, Expected:\n{t.Dump()}\n\nActual\n{q.ToString(false)}");
            }
            catch (Exception)
            {
                Debug.LogError($"Expected:\n{t.Dump()}\n\nActual\n{q}");
                throw;
            }
        }

        static TypeHandle T(string id) => new TypeHandle { Identification = id };

        static QueryComponent C(string id) => new QueryComponent(T(id));

        [Test]
        public void CreateQueryPartWorks2()
        {
            QueryContainer c = new QueryContainer("A");
            var gb = new QueryGroup("B");
            c.AddGroup(c.RootGroup, gb);

            c.AddComponent(gb, C("b1"));
            c.AddComponent(gb, C("b2"));
            c.AddComponent(gb, C("b3"));

            Check(c, new TestGroup("A")
            {
                new TestGroup("B"){"b1", "b2", "b3"}
            });

            c.AddComponent(gb, C("b4"));
            Check(c, new TestGroup("A")
            {
                new TestGroup("B"){"b1", "b2", "b3", "b4"}
            });

            var gc = new QueryGroup("C");
            c.AddGroup(c.RootGroup, gc);
            c.AddComponent(gc, C("c1"));
            c.AddComponent(gc, C("c2"));
            Check(c, new TestGroup("A")
            {
                new TestGroup("B"){"b1", "b2", "b3", "b4"},
                new TestGroup("C"){"c1", "c2"},
            });

            var gd = new QueryGroup("D");
            c.AddGroup(gc, gd);
            Check(c, new TestGroup("A")
            {
                new TestGroup("B"){"b1", "b2", "b3", "b4"},
                new TestGroup("C")
                {
                    new TestGroup("D"),
                    "c1", "c2"
                },
            });
            c.AddComponent(gd, C("d1"));
            c.AddComponent(gd, C("d2"));
            Check(c, new TestGroup("A")
            {
                new TestGroup("B") { "b1", "b2", "b3", "b4" },
                new TestGroup("C")
                {
                    new TestGroup("D") { "d1", "d2" },
                    "c1",
                    "c2"
                },
            });

            c.RemoveComponent(gc, T("c1"));
            Check(c, new TestGroup("A")
            {
                new TestGroup("B") { "b1", "b2", "b3", "b4" },
                new TestGroup("C")
                {
                    new TestGroup("D") { "d1", "d2" },
                    "c2"
                },
            });

            c.RemoveGroup(c.RootGroup, gc);
            Check(c, new TestGroup("A")
            {
                new TestGroup("B") { "b1", "b2", "b3", "b4" },
            });
        }

        [Test]
        public void ReorderLeafComponentQuery()
        {
            QueryContainer c = new QueryContainer("A");
            var gb = new QueryGroup("B");
            c.AddGroup(c.RootGroup, gb);

            c.AddComponent(gb, C("b1"));
            c.AddComponent(gb, C("b2"));

            var gc = new QueryGroup("C");
            c.AddGroup(c.RootGroup, gc);
            c.AddComponent(gc, T("c1"));
            c.AddComponent(gc, T("c2"));
            c.Dump();

            Check(c, new TestGroup("A")
            {
                new TestGroup("B"){"b1", "b2"},
                new TestGroup("C"){"c1", "c2"},
            });

            c.ReorderGroup(c.RootGroup, gb, 2);
            c.Dump();

            Check(c, new TestGroup("A")
            {
                new TestGroup("C"){"c1", "c2"},
                new TestGroup("B"){"b1", "b2"},
            });
        }

        [Test, Ignore("Not implemented yet")]
        public void ReorderNonLeafComponentQuery()
        {
            QueryContainer c = new QueryContainer("A");

            var gb = new QueryGroup("B");
            c.AddGroup(c.RootGroup, gb);
            c.AddComponent(gb, T("b1"));
            c.AddComponent(gb, T("b2"));

            var gc = new QueryGroup("C");
            c.AddGroup(gb, gc);
            c.AddComponent(gc, T("c1"));
            c.AddComponent(gc, T("c2"));

            var gd = new QueryGroup("D");
            c.AddGroup(c.RootGroup, gd);
            c.AddComponent(gd, T("d1"));
            c.AddComponent(gd, T("d2"));

            c.ReorderGroup(c.RootGroup, gb, 2);

            Check(c, new TestGroup("A")
            {
                new TestGroup("C"){"c1", "c2"},
                new TestGroup("B")
                {
                    new TestGroup("D"){"d1", "d2"},
                    "b1", "b2"
                },
            });
        }

        [Test]
        public void CreateQueryPartWorks()
        {
            QueryContainer query = new QueryContainer("A");
            query.AddComponent(query.RootGroup, C("1"));
            Assert.That(query.Components.Count, Is.EqualTo(1));
            Assert.That(query.GetComponentsInQuery(query.RootGroup).Count(), Is.EqualTo(1));
        }

        [Test]
        public void AddingSameComponentTwiceToQueryPartDoesNothing()
        {
            QueryContainer query = new QueryContainer("A");
            query.AddComponent(query.RootGroup, C("1"));
            query.AddComponent(query.RootGroup, C("2"));

            Assert.That(query.GetComponentsInQuery(query.RootGroup).Count(), Is.EqualTo(2));

            query.AddComponent(query.RootGroup, C("2"));

            Assert.That(query.GetComponentsInQuery(query.RootGroup).Count(), Is.EqualTo(2));
        }

        [Test]
        public void RemoveComponentDefinitionWorks()
        {
            var query = new QueryContainer("A");

            query.AddComponent(query.RootGroup, C("a1"));
            Assert.That(query.GetComponentsInQuery(query.RootGroup).Count(), Is.EqualTo(1));

            query.RemoveComponent(query.RootGroup, T("a1"));
            Assert.That(query.GetComponentsInQuery(query.RootGroup).Count(), Is.EqualTo(0));

            Assert.That(query.HasType(T("a1")), Is.False);
        }

        [Test]
        public void FindComponentDefinitionWorks()
        {
            var query = new QueryContainer("A");

            var queryComponent = C("a1");
            query.AddComponent(query.RootGroup, queryComponent);

            var foundPart = query.Find(queryComponent.Component, out _);
            Assert.IsNotNull(foundPart);
            Assert.That(foundPart.Component == queryComponent.Component);
        }

        [Test]
        public void UpdateComponentDefinitionWorks()
        {
            var query = new QueryContainer("A");

            var queryComponent = C("a1");
            query.AddComponent(query.RootGroup, queryComponent);

            var foundPart = query.Find(queryComponent.Component, out _);
            Assert.IsFalse(foundPart.Component.Subtract);
            Assert.IsFalse(foundPart.Component.IsShared);

            // Modify inner values
            queryComponent.Component.Subtract = true;
            queryComponent.Component.IsShared = true;

            foundPart = query.Find(queryComponent.Component, out _);
            Assert.IsTrue(foundPart.Component.Subtract);
            Assert.IsTrue(foundPart.Component.IsShared);
        }
    }
}
