using System;
using System.Collections.Generic;
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
        private static Dictionary<String,List<ServiceFlow>> _chains = new Dictionary<string, List<ServiceFlow>>();
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

        private static void RouteMessage(Message request, Proxy pua)
        {
            SIPURI to = request.Uri;
            Address from = (Address)(request.First("From").Value);
            //Retrieve both user's list of preferences from above value
            //Check from and to for any matches (check to's list for from, and from's list for to)
            string method = request.Method;
            //Check any invites for both parties

            //if found (such as voicemail redirect) do
            //Address dest = new Address("<sip:voicemail@open-ims.test>");
            //Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            //proxiedMessage.First("To").Value = dest;

            // If not found carry on as usual
            Address dest = new Address(to.ToString());
            Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            pua.SendRequest(proxiedMessage);
        }

        static void Main(string[] args)
        {
            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 9000);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            _app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            //SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            //Disable sending to SCSCF first
            SIPStack stack = CreateStack(_app);
            stack.Uri = new SIPURI("scim@open-ims.test");
            Console.ReadKey();
        }
    }
}
