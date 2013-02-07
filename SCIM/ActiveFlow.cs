using LibServiceInfo;
using SIPLib.SIP;

namespace SCIM
{
    class ActiveFlow
    {
        public Message OriginalRequest { get; set; }
        public Message LastRequest { get; set; }
        public Message LastResponse { get; set; }
        public Block LastBlock { get; set; }
    }
}
