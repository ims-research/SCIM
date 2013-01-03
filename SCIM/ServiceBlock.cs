using System;
using System.Collections.Generic;

namespace SCIM
{
    public class ServiceBlock
    {
        public string Name { get; set; }
        public string GlobalGUID { get; set; }
        public string InstanceGUID { get; set; }

        public Dictionary<string, ServiceBlock> NextBlocks { get; set; }
        public ServiceBlock ParentBlock { get; set; }
        public BlockTypes BlockType { get; set; }


        public enum BlockTypes
        {
            Service,
            Condition,
            ConditionOption,
            SIPResponse
        }

        public ServiceBlock()
        {
            NextBlocks = new Dictionary<string, ServiceBlock>();
        }

        public void AddChild(string key, ServiceBlock block)
        {
            NextBlocks.Add(key, block);
        }

        public ServiceBlock(Node node)
        {
            NextBlocks = new Dictionary<string, ServiceBlock>();
            Name = node.Name;
            GlobalGUID = node.GlobalGUID;
            InstanceGUID = node.InstanceGUID;
            switch (node.GetType().Name)
            {
                case "ServiceNode":
                    BlockType = BlockTypes.Service;
                    break;
                case "ConditionNode":
                    BlockType = BlockTypes.Condition;
                    break;
                case "ConditionValueNode":
                    BlockType = BlockTypes.ConditionOption;
                    break;
                case "SIPResponseNode":
                    BlockType = BlockTypes.SIPResponse;
                    break;
                default:
                    Console.WriteLine("Unkown node type" + node.GetType().Name);
                    break;
            }
        }
    }
}