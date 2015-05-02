using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using LibServiceInfo;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;
using Mono.Options;

namespace SCIM
{
    class Server
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        private static readonly ILog IMLog = LogManager.GetLogger("SCIMLogger");
        private static Dictionary<String,List<ServiceFlow>> _chains = new Dictionary<String,List<ServiceFlow>>();
        private static Dictionary<String, ActiveFlow> _activeFlows = new Dictionary<String,ActiveFlow>();
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
                default:
                    Log.Info("Response for Request Type " + requestType + " is unhandled ");
                    Proxy pua = (Proxy)(e.UA);
                    CheckReceivedResponse(response, pua);
                    break;
            }
        }

        private static void CheckReceivedResponse(Message response, Proxy pua)
        {
            string callID = response.First("Call-ID").ToString();
            if (_activeFlows.ContainsKey(callID))
            {
                ActiveFlow af = _activeFlows[callID];
                Block lastBlock = af.LastBlock;
                if (lastBlock.NextBlocks.Count > 0)
                {
                    foreach (string code in lastBlock.NextBlocks.Keys)
                    {
                        if ((response.ResponseCode.ToString().Contains("183") && code.Contains("181")) || code.Contains(response.ResponseCode.ToString()))
                        {
                            Block NextBlock = lastBlock.NextBlocks[code];
                            Message newRequest = af.LastRequest;
                            Header CSeq = newRequest.First("CSeq");
                            CSeq.Number++;
                            newRequest.InsertHeader(CSeq);
                            RouteNewResponse(response, pua);
                            if (NextBlock.NextBlocks.Count > 0)
                            {
                                ContinueRoutingMessage(newRequest, pua, NextBlock);
                            }
                            
                        }
                    }

                }
                else
                {
                    RouteNewResponse(response, pua);  
                }
            }
            else
            {
                RouteNewResponse(response, pua);
            }
        }

        private static void RouteNewResponse(Message response, Proxy pua)
        {
            Message proxiedResponse = pua.CreateResponse(response.ResponseCode, response.ResponseText);
            pua.SendResponse(proxiedResponse);
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
                        CheckReceivedRequest(request, pua);
                        break;
                    }
                case "MESSAGE":
                    {
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        string contentType = request.First("Content-Type").ToString().ToUpper();
                        string to = request.First("To").ToString().ToUpper();
                        if (to.ToLower().Contains("scim")) ;
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
                Dictionary<String, List<ServiceFlow>> receivedchains = message.UnzipAndDeserialize<Dictionary<string, List<ServiceFlow>>>();
                foreach (var src in receivedchains)
                {
                    _chains[src.Key] = src.Value;
                    SaveData();
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
                    SaveData();
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

        private static void CheckReceivedRequest(Message request, Proxy pua)
        {
            string callID = request.First("Call-ID").ToString();
            if (_activeFlows.ContainsKey(callID))
            {
                ActiveFlow af = _activeFlows[callID];
                Block lastBlock = af.LastBlock;
                if (lastBlock.NextBlocks.Count > 0)
                {
                    ContinueRoutingMessage(request, pua, lastBlock);
                }
            }
            else
            {
                RouteNewMessage(request, pua);    
            }
        }

        private static void ContinueRoutingMessage(Message request, Proxy pua, Block block)
        {
            SIPURI to = request.Uri;
            string toID = to.User + "@" + to.Host;

            Address from = (Address)(request.First("From").Value);
            string fromID = from.Uri.User + "@" + from.Uri.Host;

            Address dest = new Address(to.ToString());
            Address temp_dest = CheckServiceBlock(request, block, toID, fromID);
            if (temp_dest != null)
            {
                dest = temp_dest;
            }
            if (dest.ToString().Contains("anonymous.invalid"))
            {
                Message proxiedMessage = pua.CreateResponse(403, "Forbidden");
                pua.SendResponse(proxiedMessage);
            }
            else
            {
                Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
                proxiedMessage.First("To").Value = dest;
                pua.SendRequest(proxiedMessage);
            }
        }

        private static void RouteNewMessage(Message request, Proxy pua)
        {
            
            SIPURI to = request.Uri;
            string toID = to.User + "@" + to.Host;

            Address from = (Address)(request.First("From").Value);
            string fromID = from.Uri.User + "@" + from.Uri.Host;

            List<ServiceFlow> toUserFlows = GetUserBlocks(toID);
            List<ServiceFlow> fromUserFlows = GetUserBlocks(fromID);

            Address dest = new Address(to.ToString());

            foreach (ServiceFlow serviceFlow in toUserFlows)
            {
                if (serviceFlow.Blocks.Count > 0)
                {
                    Block firstBlock = serviceFlow.Blocks[serviceFlow.FirstBlockGUID];
                    Address temp_dest = CheckServiceBlock(request, firstBlock, toID, fromID);
                    if (temp_dest != null)
                    {
                        dest = temp_dest;
                        break;
                    }
                }
                else continue;
            }

            Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            proxiedMessage.First("To").Value = dest;
            string callID = proxiedMessage.First("Call-ID").ToString();
            ActiveFlow af = _activeFlows[callID];
            af.LastRequest = proxiedMessage;
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
                case Block.BlockTypes.ConditionOption:
                    dest = CheckServiceBlock(request, firstBlock.NextBlocks.Values.First(), toId, fromId);
                    break;
                case Block.BlockTypes.SIPResponse:
                    if (firstBlock.NextBlocks.Count > 0)
                    {
                        dest = CheckServiceBlock(request, firstBlock.NextBlocks.Values.First(), toId, fromId);
                    }
                    else
                    {
                        dest = null;
                    }
                    break;
                default:
                    break;
            }
            return dest;
        }

        private static Address RouteService(Message request, Block Block, string toId, string fromId)
        {
            string callID = request.First("Call-ID").ToString();
            if (_activeFlows.ContainsKey(callID))
            {
                ActiveFlow af = _activeFlows[callID];
                af.LastRequest = request;
                af.LastBlock = Block;
            }
            else
            {
                ActiveFlow af = new ActiveFlow();
                af.OriginalRequest = request;
                af.LastRequest = request;
                af.LastBlock = Block;
               _activeFlows.Add(callID,af);
            }
            Address dest = new Address(Block.DestURI);
            return dest;
            
        }

        private static Address MatchCondition(Message request, Block firstBlock, string toId, string fromId)
        {
            Dictionary<string, Block> conditionValues = firstBlock.NextBlocks;
            // Look up what condition it is (pull out of condition manager)
            if (_context.ContainsKey(toId))
            {
                if (_context[toId].ContainsKey(firstBlock.ConditionType.ToLower()))
                {
                    string conditionOption = _context[toId][firstBlock.ConditionType.ToLower()];
                    if (conditionValues.ContainsKey(conditionOption))
                    {
                        return CheckServiceBlock(request, conditionValues[conditionOption], toId, fromId);
                    }
                }
            }
            // Hard coded contact matching for now
            if (firstBlock.ConditionType.ToLower() == "contact")
            {
                if (firstBlock.Name.ToLower() == "calling party")
                {
                    foreach (string key in conditionValues.Keys)
                    {
                        if (fromId.ToLower().Contains(key.ToLower()))
                        {
                            return CheckServiceBlock(request, conditionValues[key], toId, fromId);
                        }
                    }
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
            if (String.IsNullOrEmpty(text))
            {
                return new Dictionary<string, Dictionary<string, string>>();
            }
            else
            {
                return text.UnzipAndDeserialize<Dictionary<string, Dictionary<string, string>>>();
            }
        }

        private static Dictionary<string, List<ServiceFlow>> LoadChains(string name)
        {
            string text = System.IO.File.ReadAllText(name);
            Dictionary<string, List<ServiceFlow>> temp_chains = text.UnzipAndDeserialize<Dictionary<string, List<ServiceFlow>>>();
            if (temp_chains == null)
            {
                temp_chains = new Dictionary<string, List<ServiceFlow>>();
            }
            return temp_chains;
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: scim [-i IP ADDRESS]");
            Console.WriteLine("Starts the SCIM on IP_ADDRESS if specified or attempts to guess local IP");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        static void Main(string[] args)
        {
            bool show_help = false;
            string ip = null;
            var p = new OptionSet() {
            { "i=", "IP address to use", v => ip = v },
            { "h|help", "Show help and exit", (v) => show_help = v != null }
            };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Use --help for correct options");
                return;
            }

            if (show_help)
            {
                ShowHelp(p);
                return;
            }

            if (ip == null)
            {
                ip = Helpers.GetLocalIP();
            }
            TransportInfo localTransport = CreateTransport(ip, 9000);
            _app = new SIPApp(localTransport);
            Log.Info("Starting SCIM on IP " + ip);
            _app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            _app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            LoadData();
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            //SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            //Disable sending to SCSCF first
            SIPStack stack = CreateStack(_app);
            stack.Uri = new SIPURI("scim@open-ims.test");
            Log.Info("Press any key to exit");
            Console.ReadKey();
            SaveData();
        }

        private static void SaveData()
        {
            using (StreamWriter outfile = new StreamWriter("chains.dat"))
            {
                outfile.Write(_chains.SerializeAndZip());
            }
            using (StreamWriter outfile = new StreamWriter("context.dat"))
            {
                outfile.Write(_context.SerializeAndZip());
            }
        }
    }
}
