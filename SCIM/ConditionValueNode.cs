using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SCIM
{
    public class ConditionValueNode : Node
    {
        public List<string> Values { get; set; }

        public ConditionValueNode(D3Node child) : base(child)
        {
            Values = new List<string>();
            Values = child.name.Split().ToList();
        }

        public ConditionValueNode()
            : base()
        {
            Values = new List<string>();
        }

    }
}