using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SCIM
{
    public class ConditionNode : Node
    {
        private Condition Condition { get; set; }

        public ConditionNode(D3Node child) : base(child)
        {
            Condition = ServiceManager.ConditionList[child.global_guid];
        }

        public ConditionNode()
            : base()
        {
        }
    }
}