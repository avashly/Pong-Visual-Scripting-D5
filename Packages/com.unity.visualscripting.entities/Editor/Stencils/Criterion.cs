using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public class Criterion
    {
        [SerializeReference]
        IVariableModel m_Value;

        public TypeHandle ObjectType;
        public TypeMember Member;
        public BinaryOperatorKind Operator;

        public IVariableModel Value
        {
            get => m_Value;
            set => m_Value = value;
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        [SuppressMessage("ReSharper", "BaseObjectGetHashCodeCallInGetHashCode")]
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ ObjectType.GetHashCode();
                hashCode = (hashCode * 397) ^ Member.GetHashCode();
                hashCode = (hashCode * 397) ^ Operator.GetHashCode();
                if (m_Value != null)
                    hashCode = (hashCode * 397) ^ m_Value.GetHashCode();
                return hashCode;
            }
        }

        public Criterion Clone()
        {
            var clone = new Criterion
            {
                ObjectType = ObjectType,
                Member = Member,
                Operator = Operator
            };
            if (Value != null)
                throw new NotImplementedException("SERIALIZATION");
            else
                clone.Value = null;
            return clone;
        }
    }
}
