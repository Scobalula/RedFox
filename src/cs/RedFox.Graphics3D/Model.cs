using RedFox.Graphics3D.Skeletal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// A class to hold a 3D Model.
    /// </summary>
    public class Model : Graphics3DObject
    {
        /// <summary>
        /// Get or Sets the skeleton this model uses.
        /// </summary>
        public Skeleton? Skeleton { get; set; }

        /// <summary>
        /// Gets or Sets the meshes stored within this model.
        /// </summary>
        public List<Mesh> Meshes { get; set; }

        /// <summary>
        /// Gets or Sets the materials stored within this model.
        /// </summary>
        public List<Material> Materials { get; set; }

        public Model() : base()
        {
            Meshes = [];
            Materials = [];
        }

        public Model(string name) : base(name)
        {
            Meshes = [];
            Materials = [];
        }

        public Model(string name, Skeleton? skeleton) : base(name)
        {
            Skeleton = skeleton;
            Meshes = [];
            Materials = [];
        }

        /// <summary>
        /// Assigns the bone indices based off their index within the table. 
        /// </summary>
        public void AssignSkeletonBoneIndices()
        {
            // Skeleton?.AssignBoneIndices();
        }

        public int GetVertexCount()
        {
            var result = 0;

            foreach (var mesh in Meshes)
            {
                result += mesh.Positions.Count;
            }

            return result;
        }

        public int GetFaceCount()
        {
            var result = 0;

            foreach (var mesh in Meshes)
            {
                result += mesh.Faces.Count;
            }

            return result;
        }
    }
}
