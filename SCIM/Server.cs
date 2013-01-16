using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using LibServiceInfo;
using SIPLib.SIP;
using SIPLib.Utils;
using SIPLib.src.SIP;
using log4net;

namespace SCIM
{
    class Server
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        private static readonly ILog IMLog = LogManager.GetLogger("SCIMLogger");
        private static Dictionary<String,List<ServiceFlow>> _chains;
        private static Dictionary<string, Dictionary<string, string>> _context = new Dictionary<string, Dictionary<string, string>>();

        

        private static SIPApp _app;
        public static SIPStack CreateStack(SIPApp app, string proxyIp = null, int proxyPort = -1)
        {
            SIPStack myStack = new SIPStack(app);
            if (proxyIp != null)
            {
                myStack.ProxyHost = proxyIp;
                myStack.ProxyPort = (proxyPort == -1) ? 5060 : proxyPort;
            }
            return myStack;
        }

        public static TransportInfo CreateTransport(string listenIp, int listenPort)
        {
            return new TransportInfo(IPAddress.Parse(listenIp), listenPort, System.Net.Sockets.ProtocolType.Udp);
        }

        static void AppResponseRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Response Received:" + e.Message);
            Message response = e.Message;
            string requestType = response.First("CSeq").ToString().Trim().Split()[1].ToUpper();
            switch (requestType)
            {
                case "INVITE":
                case "REGISTER":
                case "BYE":
                default:
                    Log.Info("Response for Request Type " + requestType + " is unhandled ");
                    break;
            }
        }

        static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Message request = e.Message;
            Log.Info("Request Received:" + e.Message);
            switch (request.Method.ToUpper())
            {
                case "INVITE":
                    {
                        Proxy pua = (Proxy)(e.UA);
                        RouteMessage(request, pua);
                        break;
                    }
                case "MESSAGE":
                    {
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        string contentType = request.First("Content-Type").ToString().ToUpper();
                        string to = request.First("To").ToString().ToUpper();
                        if (to.Contains("SCIM"))
                        {
                            ProcessSIPMessage(contentType, e.Message.Body);
                        }
                        break;
                    }
            }
        }

        private static void ProcessSIPMessage(string type, string message)
        {
            if (type.Equals("APPLICATION/SERV_DESC+XML"))
            {
                string[] lines = message.Split('\n');
                Dictionary<String, List<ServiceFlow>> receivedchains = message.Deserialize<Dictionary<string, List<ServiceFlow>>>();
                foreach (var src in receivedchains)
                {
                    _chains[src.Key] = src.Value;
                }
            }// Fix to use proper application/pidf+xml
            else if (type.Equals("TEXT/PLAIN"))
            {
                string[] lines = message.Split('\n');
                //alice@open-ims.test:open
                foreach (string line in lines)
                {
                    string[] parts = line.Split(':');
                    string user = parts[0];
                    string contextType = parts[1];
                    string value = parts[2];
                    if (!_context.ContainsKey(user)) _context[user] = new Dictionary<string, string>();
                    _context[user][contextType] = value;
                }
            }
            else Log.Error("Unhandled Message type of " + type);
        }

        private static Dictionary<string,string> GetUserContext(string id)
        {
            return _context.ContainsKey(id) ? _context[id] : new Dictionary<string, string>();
        }

        private static List<ServiceFlow> GetUserBlocks(string id)
        {
            return _chains.ContainsKey(id) ? _chains[id] : new List<ServiceFlow>();
        }

        private static void RouteMessage(Message request, Proxy pua)
        {
            
            SIPURI to = request.Uri;
            string toID = to.User + "@" + to.Host;

            Address from = (Address)(request.First("From").Value);
            string fromID = from.Uri.User + "@" + from.Uri.Host;

            Dictionary<string, string> toUserContext = GetUserContext(toID);
            Dictionary<string, string> fromUserContext = GetUserContext(fromID);
            List<ServiceFlow> toUserFlows = GetUserBlocks(toID);
            List<ServiceFlow> fromUserFlows = GetUserBlocks(fromID);

            Address dest = new Address(to.ToString());

            foreach (ServiceFlow serviceFlow in toUserFlows)
            {
                Block firstBlock = serviceFlow.Blocks[serviceFlow.FirstBlockGUID];
                if (CheckServiceBlock(request, firstBlock, toID, fromID, toUserContext, fromUserContext, out dest))
                {
                    break;
                }
            }

            //Retrieve both user's list of preferences from above value
            //Check from and to for any matches (check to's list for from, and from's list for to)
            string method = request.Method;
            //Check any invites for both parties

            //if found (such as voicemail redirect) do
            //Address dest = new Address("<sip:voicemail@open-ims.test>");
            //Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            //proxiedMessage.First("To").Value = dest;

            // If not found carry on as usual
            //Address dest = new Address(to.ToString());
            //Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            //pua.SendRequest(proxiedMessage);

            Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            proxiedMessage.First("To").Value = dest;
            pua.SendRequest(proxiedMessage);
            
        }

        private static Address CheckServiceBlock(Message request, Block firstBlock, string toId, string fromId)
        {
            Address dest = null;
            bool matched = false;
            switch (firstBlock.BlockType)
            {
                case Block.BlockTypes.Condition:
                    dest = MatchCondition(request, firstBlock, toId, fromId);
                    break;
                case Block.BlockTypes.Service:
                    dest = RouteService(request, firstBlock, toId, fromId);
                    break;
                default:
                    break;
            }
            return dest;
        }

        private static Address RouteService(Message request, Block firstBlock, string toId, string fromId)
        {
            Address dest = null;
            Service service = _services[firstBlock.GlobalGUID];
            return new Address(service.ServiceInformation["Server_URI"]);
        }

        private static Address MatchCondition(Message request, Block firstBlock, string toId, string fromId)
        {
            Address dest = null;
            Dictionary<string, Block> conditionValues = firstBlock.NextBlocks;
            // Look up what condition it is (pull out of condition manager)
            Condition condition = _conditions[firstBlock.GlobalGUID];
            if (_context[toId].ContainsKey(condition.Type.ToLower()))
            {
                string conditionOption = _context[toId][condition.Name.ToLower()];
                if (conditionValues.ContainsKey(conditionOption))
                {
                    return CheckServiceBlock(request, conditionValues[conditionOption], toId, fromId);
                }
            }
            return new Address(request.Uri.ToString());
        }

        private static void LoadData()
        {
            _chains  = File.Exists("chains.dat") ? LoadChains("chains.dat") : new Dictionary<string, List<ServiceFlow>>();
            _context = File.Exists("context.dat") ? LoadContexts("context.dat") : new Dictionary<string, Dictionary<string,string>> ();
        }

        private static Dictionary<string, Dictionary<string, string>> LoadContexts(string name)
        {
            string text = System.IO.File.ReadAllText(name);
            return text.Deserialize<Dictionary<string, Dictionary<string, string>>>();
        }

        private static Dictionary<string, List<ServiceFlow>> LoadChains(string name)
        {
            string text = System.IO.File.ReadAllText(name);
            return text.Deserialize<Dictionary<string, List<ServiceFlow>>>();
        }

        static void Main(string[] args)
        {

            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 9000);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            _app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            LoadData();
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            //SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            //Disable sending to SCSCF first
            SIPStack stack = CreateStack(_app);
            stack.Uri = new SIPURI("scim@open-ims.test");
            Console.ReadKey();
            SaveData();
        }

        private static void SaveData()
        {
            using (StreamWriter outfile = new StreamWriter("chains.dat"))
            {
                outfile.Write(_chains.Serialize());
            }
            using (StreamWriter outfile = new StreamWriter("context.dat"))
            {
                outfile.Write(_context.Serialize());
            }
        }
    }
}
