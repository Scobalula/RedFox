namespace RedFox.Graphics3D.IO
{
    /// <summary>
    /// Provides configuration settings for scene translation and manager-side merge behavior.
    /// </summary>
    public class SceneTranslatorOptions
    {
        /// <summary>
        /// Gets or Sets if raw vertex data (positions, normals, UVs) should be written to the scene file, not taking the bind matrices into account.
        /// </summary>
        public bool WriteRawVertices { get; set; }


    }
}
