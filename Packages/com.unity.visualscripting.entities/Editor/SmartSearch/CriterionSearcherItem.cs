using System;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Model;

namespace UnityEditor.VisualScripting.SmartSearch
{
    class CriterionSearcherItem : SearcherItem
    {
        public BinaryOperatorKind Operator { get; }

        public CriterionSearcherItem(BinaryOperatorKind binaryOperatorKind)
            : base(binaryOperatorKind.ToString())
        {
            Operator = binaryOperatorKind;
        }
    }
}
