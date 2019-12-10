using System;
using UnityEditor.EditorCommon.Redux;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine.Assertions;

namespace Packages.VisualScripting.Editor.Redux.Actions
{
    public class AddCriteriaModelAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;

        public AddCriteriaModelAction(ICriteriaModelContainer criteriaModelContainer)
        {
            Assert.IsNotNull(criteriaModelContainer);

            CriteriaModelContainer = criteriaModelContainer;
        }
    }

    public class RemoveCriteriaModelAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;

        public RemoveCriteriaModelAction(ICriteriaModelContainer criteriaModelContainer,
                                         CriteriaModel criteriaModel)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
        }
    }

    public class RenameCriteriaModelAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly string Name;

        public RenameCriteriaModelAction(ICriteriaModelContainer criteriaModelContainer,
                                         CriteriaModel criteriaModel,
                                         string name)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            Name = name;
        }
    }

    public class MoveCriteriaModelAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly CriteriaModel TargetCriteriaModel;
        public readonly bool InsertAtEnd;

        public MoveCriteriaModelAction(ICriteriaModelContainer criteriaModelContainer,
                                       CriteriaModel criteriaModel,
                                       CriteriaModel targetCriteriaModel,
                                       bool insertAtEnd)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);
            Assert.IsNotNull(targetCriteriaModel);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            TargetCriteriaModel = targetCriteriaModel;
            InsertAtEnd = insertAtEnd;
        }
    }

    public class DuplicateCriteriaModelAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly ICriteriaModelContainer targetCriteriaModelContainer;
        public readonly CriteriaModel TargetCriteriaModel;
        public readonly bool InsertAtEnd;

        public DuplicateCriteriaModelAction(ICriteriaModelContainer criteriaModelContainer,
                                            CriteriaModel criteriaModel,
                                            ICriteriaModelContainer targetCriteriaModelContainer,
                                            CriteriaModel targetCriteriaModel,
                                            bool insertAtEnd)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);
            Assert.IsNotNull(targetCriteriaModelContainer);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            this.targetCriteriaModelContainer = targetCriteriaModelContainer;
            TargetCriteriaModel = targetCriteriaModel;
            InsertAtEnd = insertAtEnd;
        }
    }

    public class AddCriterionAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly TypeHandle TypeHandle;
        public readonly TypeMember TypeMember;
        public readonly BinaryOperatorKind OperatorKind;

        public AddCriterionAction(ICriteriaModelContainer criteriaModelContainer,
                                  CriteriaModel criteriaModel,
                                  TypeHandle typeHandle,
                                  TypeMember typeMember,
                                  BinaryOperatorKind operatorKind)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            TypeHandle = typeHandle;
            TypeMember = typeMember;
            OperatorKind = operatorKind;
        }
    }

    public class RemoveCriterionAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly Criterion Criterion;

        public RemoveCriterionAction(ICriteriaModelContainer criteriaModelContainer,
                                     CriteriaModel criteriaModel,
                                     Criterion criterion)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);
            Assert.IsNotNull(criterion);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            Criterion = criterion;
        }
    }

    public class ChangeCriterionAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly Criterion Criterion;
        public readonly TypeHandle TypeHandle;
        public readonly TypeMember TypeMember;
        public readonly BinaryOperatorKind OperatorKind;

        public ChangeCriterionAction(ICriteriaModelContainer criteriaModelContainer,
                                     CriteriaModel criteriaModel,
                                     Criterion criterion,
                                     TypeHandle typeHandle,
                                     TypeMember typeMember,
                                     BinaryOperatorKind operatorKind)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);
            Assert.IsNotNull(criterion);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            Criterion = criterion;
            TypeHandle = typeHandle;
            TypeMember = typeMember;
            OperatorKind = operatorKind;
        }
    }

    public class MoveCriterionAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly Criterion Criterion;
        public readonly Criterion TargetCriterion;
        public readonly bool InsertAtEnd;

        public MoveCriterionAction(ICriteriaModelContainer criteriaModelContainer,
                                   CriteriaModel criteriaModel,
                                   Criterion criterion,
                                   Criterion targetCriterion,
                                   bool insertAtEnd)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);
            Assert.IsNotNull(criterion);
            Assert.IsNotNull(targetCriterion);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            Criterion = criterion;
            TargetCriterion = targetCriterion;
            InsertAtEnd = insertAtEnd;
        }
    }

    public class DuplicateCriterionAction : IAction
    {
        public readonly ICriteriaModelContainer CriteriaModelContainer;
        public readonly CriteriaModel CriteriaModel;
        public readonly Criterion Criterion;
        public readonly ICriteriaModelContainer TargetCriteriaModelContainer;
        public readonly CriteriaModel TargetCriteriaModel;
        public readonly Criterion TargetCriterion;
        public readonly bool InsertAtEnd;

        public DuplicateCriterionAction(ICriteriaModelContainer criteriaModelContainer,
                                        CriteriaModel criteriaModel,
                                        Criterion criterion,
                                        ICriteriaModelContainer targetCriteriaModelContainer,
                                        CriteriaModel targetCriteriaModel,
                                        Criterion targetCriterion,
                                        bool insertAtEnd)
        {
            Assert.IsNotNull(criteriaModelContainer);
            Assert.IsNotNull(criteriaModel);
            Assert.IsNotNull(criterion);
            Assert.IsNotNull(targetCriteriaModelContainer);
            Assert.IsNotNull(targetCriteriaModel);

            CriteriaModelContainer = criteriaModelContainer;
            CriteriaModel = criteriaModel;
            Criterion = criterion;
            TargetCriteriaModelContainer = targetCriteriaModelContainer;
            TargetCriteriaModel = targetCriteriaModel;
            TargetCriterion = targetCriterion;
            InsertAtEnd = insertAtEnd;
        }
    }
}
