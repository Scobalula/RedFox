using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    [DebuggerDisplay("Name: {Name}")]
    public class FBXNode
    {
        /// <summary>
        /// Gets or Sets the name of the FBX Node.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or Sets the properties.
        /// </summary>
        public List<FBXProperty> Properties { get; set; }

        public List<FBXNode> Children { get; set; }

        public FBXNode(string name)
        {
            Name = name;
            Properties = new();
            Children = new();
        }

        public FBXNode(string name, int nodeCount)
        {
            Name = name;
            Properties = new(nodeCount);
            Children = new();
        }
    }
}
