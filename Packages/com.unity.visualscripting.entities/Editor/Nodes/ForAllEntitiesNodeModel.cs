using System;
using System.Linq;
using Unity.Entities;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;

namespace Packages.VisualScripting.Editor.Stencils
{
    [Serializable]
    public class ForAllEntitiesNodeModel : LoopNodeModel
    {
        const string k_Title = "For All Entities";

        public override bool IsInsertLoop => true;
        public override LoopConnectionType LoopConnectionType => LoopConnectionType.LoopStack;

        public override string InsertLoopNodeTitle => k_Title;
        public override Type MatchingStackType => typeof(ForAllEntitiesStackModel);

        public ComponentQueryDeclarationModel ComponentQueryDeclarationModel { get; private set; }

        static readonly string k_InputPortId = ForAllEntitiesStackModel.DefaultCollectionName;

        protected override void OnDefineNode()
        {
            base.OnDefineNode();

            var typeHandle = typeof(EntityQuery).GenerateTypeHandle(Stencil);
            InputPort = AddDataInput(k_InputPortId, typeHandle);
        }

        public override void OnConnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            if (selfConnectedPortModel != null)
            {
                var output = selfConnectedPortModel.Direction == Direction.Input
                    ? OutputPort.ConnectionPortModels.FirstOrDefault()?.NodeModel
                    : otherConnectedPortModel?.NodeModel;

                if (selfConnectedPortModel.Direction == Direction.Input && selfConnectedPortModel.UniqueId == k_InputPortId)
                    ComponentQueryDeclarationModel = OnEntitiesEventBaseNodeModel.GetConnectedEntityQuery(otherConnectedPortModel);

                if (output is ForAllEntitiesStackModel foreachStack)
                {
                    foreachStack.OnConnection(foreachStack.InputPort, OutputPort);

                    ((VSGraphModel)GraphModel).LastChanges.ChangedElements.Add(foreachStack);
                }
            }
        }
    }
}
