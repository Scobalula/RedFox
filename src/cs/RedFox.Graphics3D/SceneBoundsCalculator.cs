using System;
using System.Numerics;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Provides utility methods for computing world-space scene bounds.
    /// </summary>
    public static class SceneBoundsCalculator
    {
        /// <summary>
        /// Computes world-space bounds by aggregating <see cref="SceneNode.TryGetSceneBounds"/> results
        /// across all descendants. Meshes and skeleton bones contribute bounds; other node types are skipped.
        /// Nodes flagged with <see cref="SceneNodeFlags.NoDraw"/> are ignored.
        /// </summary>
        /// <param name="scene">The scene to analyze.</param>
        /// <param name="bounds">The computed bounds when successful; otherwise <see cref="SceneBounds.Invalid"/>.</param>
        /// <returns><see langword="true"/> when any bounds contributors were found; otherwise <see langword="false"/>.</returns>
        public static bool TryCompute(Scene scene, out SceneBounds bounds)
            => TryCompute(scene, out bounds, includeNode: null);

        /// <summary>
        /// Computes world-space bounds by aggregating <see cref="SceneNode.TryGetSceneBounds"/> results
        /// across all descendants. Meshes and skeleton bones contribute bounds; other node types are skipped.
        /// Nodes flagged with <see cref="SceneNodeFlags.NoDraw"/> are ignored.
        /// </summary>
        /// <param name="scene">The scene to analyze.</param>
        /// <param name="bounds">The computed bounds when successful; otherwise <see cref="SceneBounds.Invalid"/>.</param>
        /// <param name="includeNode">
        /// Optional predicate that controls whether a node contributes to bounds.
        /// Return <see langword="false"/> to exclude a node.
        /// </param>
        /// <returns><see langword="true"/> when any bounds contributors were found; otherwise <see langword="false"/>.</returns>
        public static bool TryCompute(Scene scene, out SceneBounds bounds, Func<SceneNode, bool>? includeNode)
        {
            ArgumentNullException.ThrowIfNull(scene);

            bool hasBounds = false;
            Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);

            foreach (SceneNode node in scene.EnumerateDescendants())
            {
                if (node.Flags.HasFlag(SceneNodeFlags.NoDraw))
                {
                    continue;
                }

                if (includeNode is not null && !includeNode(node))
                {
                    continue;
                }

                if (!node.TryGetSceneBounds(out SceneBounds nodeBounds))
                {
                    continue;
                }

                Expand(nodeBounds.Min, ref hasBounds, ref min, ref max);
                Expand(nodeBounds.Max, ref hasBounds, ref min, ref max);
            }

            bounds = hasBounds ? new SceneBounds(min, max) : SceneBounds.Invalid;
            return hasBounds;
        }

        private static void Expand(Vector3 point, ref bool hasBounds, ref Vector3 min, ref Vector3 max)
        {
            if (!hasBounds)
            {
                min = point;
                max = point;
                hasBounds = true;
                return;
            }

            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
    }
}
