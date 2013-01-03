using System.Collections.Generic;

namespace SCIM
{
    public class D3Node
    {
        public string name { get; set; }
        public string value { get; set; }
        public string type { get; set; }
        public string global_guid { get; set; }
        public string instance_guid { get; set; }
        public string depth { get; set; }
        public string x { get; set; }
        public string y { get; set; }
        public string id { get; set; }
        public string x0 { get; set; }
        public string y0 { get; set; }
        public List<D3Node> children { get; set; }

    }
}