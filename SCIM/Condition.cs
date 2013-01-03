using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SCIM
{
    public class Condition
    {
        public string ConType { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description{ get; set; }
        public List<string> PossibleValues { get; set; }
        public string GUID { get; set; }

        public Condition()
        {
            InitialiseVariables();
        }

        public Condition(XElement xmlblock)
        {
            Name = xmlblock.Element("Name").Value;
            Type = xmlblock.Element("Type").Value;
            Description = xmlblock.Element("Description").Value;
            GUID = xmlblock.Element("GUID").Value;
            List<string> possibleValues = new List<string>(xmlblock.Element("Possible_Values").Value.Split(',').Select(p => p.Trim()).ToList());
            PossibleValues = possibleValues;
        }

        private void InitialiseVariables()
        {
            PossibleValues = new List<string>();
        }

    }
}