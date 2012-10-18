using System.Collections.Generic;

namespace USPS.Code
{
    public class ServiceBlock
    {
        public string Name { get; set; }
        public string GlobalGUID { get; set; }
        public string InstanceGUID { get; set; }
        public Types BlockType { get; set; }

        public enum Types { Condition, Service, Terminating }
        public Dictionary<string, string> NextBlock { get; set; }
        
        public ServiceBlock()
        {
            NextBlock = new Dictionary<string, string>();
        }
    }
}