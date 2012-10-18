using System;
using System.Collections.Generic;
using System.Net;
using SIPLib.SIP;
using SIPLib.Utils;
using SIPLib.src.SIP;
using USPS.Code;
using log4net;

namespace SCIM
{
    class Server
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        private static readonly ILog IMLog = LogManager.GetLogger("SCIMLogger");

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
                List<List<ServiceFlow>> receivedchains = new List<List<ServiceFlow>>();
                foreach (string line in lines)
                {
                    receivedchains.Add(message.Deserialize<List<ServiceFlow>>());
                }
            }
            else if (type.Equals(""))
            {
                
            }
            else Log.Error("Unhandled Message type of ");
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
