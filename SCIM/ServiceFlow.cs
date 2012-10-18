using System.Collections.Generic;

namespace USPS.Code
{
    public class ServiceFlow
    {

        public Dictionary<string, ServiceBlock> Blocks;
        public ServiceFlow()
        {
            Blocks = new Dictionary<string, ServiceBlock>();
        }
    }
}