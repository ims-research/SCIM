using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SCIM
{
    public class Service
    {
        public Dictionary<string, string> ServiceInformation { get; set; }
        public Dictionary<string, string> ServiceConfig { get; set; }
        public Dictionary<string, string> SIPHeaders { get; set; }
        public Dictionary<string, string> SIPResponses { get; set; }
        public Dictionary<string, string> Capabilities { get; set; }

        public Service()
        {
            InitialiseVariables();
        }

        public Service(string filename)
        {
            InitialiseVariables();
            ParseXMLFile(filename);
        }

        public void ParseXMLFile(string filename)
        {
            ParseXML(XDocument.Load(filename));
        }

        public void ParseXMLString(string XML)
        {
            ParseXML(XDocument.Parse(XML));
        }

        public void ParseXML(XDocument doc)
        {
            foreach (XElement block in doc.Elements("Service"))
            {
                foreach (XElement element in block.Elements("Service_Information").Elements())
                {
                    ServiceInformation[element.Name.ToString()] = element.Value;
                }
                foreach (XElement element in block.Elements("Service_Config").Elements())
                {
                    ServiceConfig[element.Name.ToString()] = element.Value;
                }
                foreach (XElement element in block.Elements("SIP_Headers").Elements())
                {
                    SIPHeaders[element.Name.ToString()] = element.Value;
                }
                foreach (XElement element in block.Elements("SIP_Responses").Elements())
                {
                    SIPResponses[element.Name.ToString()] = element.Value;
                }
                foreach (XElement element in block.Elements("Capabalities").Elements())
                {
                    Capabilities[element.Name.ToString()] = element.Value;
                }
            }
        }

        private void InitialiseVariables()
        {
            ServiceInformation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ServiceConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SIPHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SIPResponses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Capabilities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}