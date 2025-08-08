using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.KaydaraFBX.Document
{
    public class FBXDocument
    {
        /// <summary>
        /// Gets or Sets the file version.
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// Gets or Sets the nodes stored in the document.
        /// </summary>
        public List<FBXNode> Nodes { get; set; }

        public FBXDocument(long version)
        {
            Version = version;
            Nodes = new();
        }
    }
}
