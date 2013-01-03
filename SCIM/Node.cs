using System;
using System.Collections.Generic;

namespace SCIM
{
    public class Node : Object
    {
        public List<Node> Children { get; set; }
        public string Name { get; set; }
        public string GlobalGUID { get; set; }
        public string InstanceGUID { get; set; }

        public Types NodeType { get; set; }
        public enum Types { Service, Condition, ConditionOption, SIPResponse }
        public D3Node d3Node { get; set; }

        public Node()
        {
            Children = new List<Node>();
        }

        public Node(D3Node node)
        {
            d3Node = node;
            Children = new List<Node>();
            Name = node.name;
            GlobalGUID = node.global_guid;
            InstanceGUID = node.instance_guid;

            foreach (D3Node child in node.children)
            {
                switch (child.type)
                {
                    case "Service":
                        Children.Add(new ServiceNode(child));
                        break;
                    case "Condition":
                        Children.Add(new ConditionNode(child));
                        break;
                    case "ConditionValue":
                        Children.Add(new ConditionValueNode(child));
                        break;
                    case "ServiceValue":
                        Children.Add(new SIPResponseNode(child));
                        break;
                    default:
                        Console.WriteLine("Unkown node type" + child.type);
                        break;

                }
            }
        }
    }
}