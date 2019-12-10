using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace Packages.VisualScripting.Editor.Stencils
{
    public static class GraphModelEcsExtensions
    {
        public static ComponentQueryDeclarationModel CreateComponentQuery(this VSGraphModel graphModel, string queryName)
        {
            var field = VariableDeclarationModel.CreateDeclarationNoUndoRecord<ComponentQueryDeclarationModel>(queryName,
                typeof(EntityQuery).GenerateTypeHandle(graphModel.Stencil),
                true,
                graphModel,
                VariableType.ComponentQueryField,
                ModifierFlags.ReadOnly,
                null,
                VariableFlags.None);
            graphModel.VariableDeclarations.Add(field);
            return field;
        }

        static ComponentQueryDeclarationModel CreateComponentQuery(this VSGraphModel graphModel, string queryName, IEnumerable<TypeHandle> componentTypes)
        {
            Stencil stencil = graphModel.Stencil;

            string uniqueName = graphModel.GetUniqueName(queryName);

            ComponentQueryDeclarationModel field = graphModel.CreateComponentQuery(uniqueName);
            if (field != null)
            {
                foreach (TypeHandle typeHandle in componentTypes)
                {
                    field.AddComponent(stencil, typeHandle, ComponentDefinitionFlags.None);
                }

                if (componentTypes.Any())
                    field.ExpandOnCreateUI = true;
            }

            return field;
        }

        public static ComponentQueryDeclarationModel CreateQueryFromGameObject(this VSGraphModel graphModel, GameObject gameObject)
        {
            EcsStencil stencil = graphModel.Stencil as EcsStencil;

            string queryName = gameObject.name + " Query";

            List<TypeHandle> componentTypes = stencil.GetEcsComponentsForGameObject(gameObject);

            AddConvertToEntityComponentIfNeeded(gameObject);

            return graphModel.CreateComponentQuery(queryName, componentTypes);
        }

        static void AddConvertToEntityComponentIfNeeded(GameObject gameObject)
        {
            if (gameObject.GetComponent<ConvertToEntity>() == null)
            {
                var comp = gameObject.AddComponent<ConvertToEntity>();
                comp.ConversionMode = ConvertToEntity.Mode.ConvertAndDestroy; // TODO Drop-1 find the proper way to deal with conversion
            }
        }

        static List<TypeHandle> GetEcsComponentsForGameObject(this EcsStencil stencil, GameObject go)
        {
            using (World w = new World("Conversion world"))
            {
                Entity e = GameObjectConversionUtility.ConvertGameObjectHierarchy(go, w);
                List<TypeHandle> result = new List<TypeHandle>();
                using (NativeArray<ComponentType> componentTypes = w.EntityManager.GetComponentTypes(e))
                {
                    result.AddRange(componentTypes
                        .Select(t => t.GetManagedType())
                        .Where(t => t != typeof(LinkedEntityGroup)) // ignore LinkedEntityGroup - GameObject hierarchy
                        .Select(t => t.GenerateTypeHandle(stencil)));
                }
                return result;
            }
        }
    }
}
