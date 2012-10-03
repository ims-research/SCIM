using System;
using System.Net;
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
            Log.Info("Request Received:" + e.Message);
            Message request = e.Message;
            Proxy pua = (Proxy)(e.UA);
            Address dest = new Address("<sip:voicemail@open-ims.test>");
            Message proxiedMessage = pua.CreateRequest(e.Message.Method, dest, true,true);
            proxiedMessage.First("To").Value = dest;
            pua.SendRequest(proxiedMessage);
            /*switch (request.Method.ToUpper())
            {
                case "MESSAGE":
                    {
                        IMLog.Info(request.First("From") + " says " + request.Body);
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        break;
                    }
                case "INVITE":
                case "ACK":
                case "BYE":
                case "CANCEL":
                case "OPTIONS":
                case "REFER":
                case "SUBSCRIBE":
                case "NOTIFY":
                case "PUBLISH":
                case "INFO":
                default:
                    {
                        Log.Info("Request with method " + request.Method.ToUpper() + " is unhandled");
                        break;
                    }
            }*/
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
