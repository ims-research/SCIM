using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SCIM
{
    public class SIPResponseNode : Node
    {
       public List<string> Values { get; set; }

        public SIPResponseNode(D3Node child) : base(child)
        {
            Values = new List<string>();
            Values = child.value.Split().ToList();
        }

        public SIPResponseNode()
            : base()
        {
            Values = new List<string>();
        }
    }
}