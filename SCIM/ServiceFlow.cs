using System;
using System.Collections.Generic;
using System.Xml;

namespace SCIM
{
    public class ServiceFlow
    {

        public Dictionary<string, ServiceBlock> Blocks;
        public string FirstBlockGUID { get; set; }
        public string Name { get; set; }
        public D3Node RootNode { get; set; }

        public ServiceFlow()
        {
            Blocks = new Dictionary<string, ServiceBlock>();
        }
        public ServiceFlow(String name)
        {
            Init(name);
        }

        public ServiceFlow(Node rootNode, String name)
        {
            Init(name);
            FirstBlockGUID = rootNode.Children[0].InstanceGUID;
            Dictionary<string, ServiceBlock> blocks = new Dictionary<string, ServiceBlock>();
            CreateBlocks(blocks, rootNode);
            Blocks = blocks;
            RootNode = rootNode.d3Node;
        }

        private void Init(String name)
        {
            Blocks = new Dictionary<string, ServiceBlock>();
            Name = name;
        }

        private void CreateBlocks(Dictionary<string, ServiceBlock> blocks, Node node)
        {
            ServiceBlock block = new ServiceBlock(node);
            if (node.Children.Count > 0)
            {
                foreach (Node child in node.Children)
                {
                    switch (node.GetType().Name)
                    {
                        case "ServiceNode":
                        case "ConditionNode":
                            block.AddChild(child.Name, new ServiceBlock(child));
                            break;
                        case "ConditionValueNode":
                        case "SIPResponseNode":
                            block.AddChild(child.InstanceGUID, new ServiceBlock(child));
                            break;
                        default:
                            Console.WriteLine("Unkown node type" + child.GetType().Name);
                            break;
                    }
                }
                if (node.Name != "Start")
                {
                    blocks.Add(block.InstanceGUID, block);
                }
                // Double loop to maintain tree order
                foreach (Node child in node.Children)
                {
                    CreateBlocks(blocks, child);
                }
            }
        }
    }
}