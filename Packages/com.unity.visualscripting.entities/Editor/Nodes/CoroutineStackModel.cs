using System;
using System.Collections.Generic;
using Packages.VisualScripting.Editor.Stencils;
using System.Linq;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public class CoroutineStackModel : LoopStackModel, IIteratorStackModel
    {
        public override Type MatchingStackedNodeType => typeof(CoroutineNodeModel);
        public override bool AllowChangesToModel => false;
        public IReadOnlyList<CriteriaModel> CriteriaModels => null;
        public ComponentQueryDeclarationModel ComponentQueryDeclarationModel => null;
        public VariableDeclarationModel ItemVariableDeclarationModel => null;

        public override List<TitleComponent> BuildTitle()
        {
            var connection = InputPort.ConnectionPortModels.FirstOrDefault(p => p.NodeModel is CoroutineNodeModel);
            var coroutineType = ((CoroutineNodeModel)connection?.NodeModel)?.CoroutineType ?? TypeHandle.Unknown;

            var title = InputPort.Connected && !coroutineType.Equals(TypeHandle.Unknown)
                ? $"On {VseUtility.GetTitle(coroutineType.Resolve(Stencil))}"
                : "On Coroutine";

            return new List<TitleComponent>
            {
                new TitleComponent
                {
                    titleComponentType = TitleComponentType.String,
                    titleObject = title
                }
            };
        }

        public override void OnConnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            base.OnConnection(selfConnectedPortModel, otherConnectedPortModel);
            ((VSGraphModel)GraphModel).LastChanges.ChangedElements.Add(this);
        }

        public override void OnDisconnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            base.OnDisconnection(selfConnectedPortModel, otherConnectedPortModel);
            ((VSGraphModel)GraphModel).LastChanges.ChangedElements.Add(this);
        }

        public bool AddTakenName(string proposedName)
        {
            throw new NotImplementedException();
        }

        public void AddCriteriaModelNoUndo(CriteriaModel criteriaModel)
        {
            throw new NotImplementedException();
        }

        public void InsertCriteriaModelNoUndo(int index, CriteriaModel criteriaModel)
        {
            throw new NotImplementedException();
        }

        public void RemoveCriteriaModelNoUndo(CriteriaModel criteriaModel)
        {
            throw new NotImplementedException();
        }

        public int IndexOfCriteriaModel(CriteriaModel criteriaModel)
        {
            throw new NotImplementedException();
        }
    }
}
