using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SCIM
{
    public class ServiceNode : Node
    {
        public Service Service { get; set; }

        public ServiceNode(D3Node child) : base(child)
        {
            Service = ServiceManager.ServiceList[child.global_guid];
        }

        public ServiceNode()
            : base()
        {
        }
        
    }
}