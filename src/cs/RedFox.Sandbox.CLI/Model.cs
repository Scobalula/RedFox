using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Sandbox.CLI
{
    public class Model2
    {
        public string FilePath { get; set; } 
        public string ParentBoneTag { get; set; }

        public ModelType Type { get; set; } = ModelType.Attachment;

        public Model2(string filePath)
        {
            FilePath = filePath;
            ParentBoneTag = string.Empty;
        }

        public Model2(string filePath, ModelType type)
        {
            FilePath = filePath;
            Type = type;
            ParentBoneTag = string.Empty;
        }
    }
}
