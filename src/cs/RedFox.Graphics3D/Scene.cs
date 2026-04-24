using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a 3D scene containing a root node and all its descendants.
    /// </summary>
    public class Scene : IUpdatable
    {
        private const float DefaultAnimationFrameRate = 30.0f;

        /// <summary>
        /// Gets or sets the name of the scene.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the root node of the scene.
        /// </summary>
        public SceneRoot RootNode { get; internal set; }

        /// <summary>
        /// Gets the animation players owned by this scene.
        /// Use <see cref="CreateAnimationPlayers"/> to rebuild this list from scene content.
        /// </summary>
        public List<AnimationPlayer> AnimationPlayers { get; } = [];

        /// <summary>
        /// Gets or sets the scene up-axis used for renderer-side axis conversion.
        /// </summary>
        public SceneUpAxis UpAxis { get; set; } = SceneUpAxis.Y;

        /// <summary>
        /// Gets or sets the front-face winding used for scene rendering.
        /// </summary>
        public FaceWinding FaceWinding { get; set; } = FaceWinding.CounterClockwise;

        /// <summary>
        /// Gets or sets a value indicating whether animation playback is paused.
        /// When <see langword="true"/>, animation players are not updated but the scene graph still updates.
        /// </summary>
        public bool IsAnimationPaused { get; set; }

        /// <summary>
        /// Initializes a new instance with the specified name.
        /// </summary>
        /// <param name="name">The scene name.</param>
        public Scene(string name)
        {
            Name = name;
            RootNode = new SceneRoot(this);
        }

        /// <summary>
        /// Initializes a new instance with a default name.
        /// </summary>
        public Scene() : this("Untitled Scene")
        {

        }

        /// <summary>
        /// Updates the scene and all nodes.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        public void Update(float deltaTime)
        {
            if (!IsAnimationPaused && deltaTime > 0.0f && AnimationPlayers.Count > 0)
            {
                foreach (AnimationPlayer player in AnimationPlayers)
                {
                    player.Update(deltaTime, AnimationSampleType.DeltaTime);
                }
            }

            RootNode.Update(deltaTime);
        }

        /// <summary>
        /// Rebuilds <see cref="AnimationPlayers"/> by creating one player per discovered
        /// <see cref="SkeletonAnimation"/> in the scene. Each animation is bound to bone hierarchies
        /// found within the scene, with bones matched by name (case-sensitive).
        /// </summary>
        /// <returns>The rebuilt animation player list.</returns>
        public IReadOnlyList<AnimationPlayer> CreateAnimationPlayers()
        {
            AnimationPlayers.Clear();

            // Find all bone root nodes (SkeletonBone with no SkeletonBone parent).
            List<SkeletonBone> boneRoots = [];
            foreach (SkeletonBone bone in EnumerateDescendants<SkeletonBone>())
            {
                if (bone.Parent is SkeletonBone)
                {
                    continue;
                }

                boneRoots.Add(bone);
            }

            int playerIndex = 0;
            foreach (SkeletonAnimation animation in EnumerateDescendants<SkeletonAnimation>())
            {
                SkeletonBone? boneRoot = ResolveSkeletonBoneRoot(animation, boneRoots);
                if (boneRoot is null)
                {
                    continue;
                }

                float frameRate = float.IsFinite(animation.Framerate) && animation.Framerate > 0.0f
                    ? animation.Framerate
                    : DefaultAnimationFrameRate;

                string playerName = string.IsNullOrWhiteSpace(animation.Name)
                    ? $"SkeletalPlayer_{playerIndex}"
                    : $"{animation.Name}_Player";
                string samplerName = string.IsNullOrWhiteSpace(animation.Name)
                    ? $"SkeletalSampler_{playerIndex}"
                    : $"{animation.Name}_Sampler";

                AnimationPlayer player = new(playerName)
                {
                    FrameRate = frameRate
                };

                SkeletonAnimationSampler sampler = new(samplerName, animation, boneRoot)
                {
                    FrameRate = frameRate
                };

                player.WithSubLayer(sampler, AnimationBlendMode.Override, 1.0f);
                AnimationPlayers.Add(player);
                playerIndex++;
            }

            return AnimationPlayers;
        }

        private static SkeletonBone? ResolveSkeletonBoneRoot(SkeletonAnimation animation, IReadOnlyList<SkeletonBone> boneRoots)
        {
            if (boneRoots.Count == 0)
            {
                return null;
            }

            if (boneRoots.Count == 1)
            {
                return boneRoots[0];
            }

            SkeletonBone? bestRoot = null;
            int bestMatchCount = 0;

            foreach (SkeletonBone root in boneRoots)
            {
                int matchCount = 0;
                foreach (SkeletonAnimationTrack track in animation.Tracks)
                {
                    if (root.EnumerateHierarchy<SkeletonBone>().Any(b => b.Name.Equals(track.Name)))
                    {
                        matchCount++;
                    }
                }

                if (matchCount > bestMatchCount)
                {
                    bestMatchCount = matchCount;
                    bestRoot = root;
                }
            }

            return bestMatchCount > 0 ? bestRoot : null;
        }

        /// <summary>
        /// Removes a node from the scene.
        /// </summary>
        /// <param name="node">The node to remove.</param>
        /// <returns>True if the node was removed; otherwise false.</returns>
        public bool RemoveNode(SceneNode node) => RootNode.RemoveNode(node);

        /// <summary>
        /// Removes a node by name from the scene.
        /// </summary>
        /// <param name="name">The name of the node to remove.</param>
        /// <returns>True if the node was removed; otherwise false.</returns>
        public bool RemoveNode(string name) => RootNode.RemoveNode(name);

        /// <summary>
        /// Removes all nodes from the scene.
        /// </summary>
        public void ClearNodes() => RootNode.ClearNodes();

        /// <summary>
        /// Enumerates direct children of the root node.
        /// </summary>
        /// <returns>An enumerable of child nodes.</returns>
        public IEnumerable<SceneNode> EnumerateChildren() => RootNode.EnumerateChildren();

        /// <summary>
        /// Enumerates direct children of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags child nodes must contain to be returned.</param>
        /// <returns>An enumerable of child nodes.</returns>
        public IEnumerable<SceneNode> EnumerateChildren(SceneNodeFlags filter) => RootNode.EnumerateChildren(filter);

        /// <summary>
        /// Enumerates direct children of the root node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An enumerable of child nodes.</returns>
        public IEnumerable<T> EnumerateChildren<T>() where T : SceneNode => RootNode.EnumerateChildren<T>();

        /// <summary>
        /// Enumerates direct children of the root node of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags child nodes must contain to be returned.</param>
        /// <returns>An enumerable of child nodes.</returns>
        public IEnumerable<T> EnumerateChildren<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.EnumerateChildren<T>(filter);

        /// <summary>
        /// Enumerates all descendants of the root node.
        /// </summary>
        /// <returns>An enumerable of descendant nodes.</returns>
        public IEnumerable<SceneNode> EnumerateDescendants() => RootNode.EnumerateDescendants();

        /// <summary>
        /// Enumerates all descendants of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        /// <returns>An enumerable of descendant nodes.</returns>
        public IEnumerable<SceneNode> EnumerateDescendants(SceneNodeFlags filter) => RootNode.EnumerateDescendants(filter);

        /// <summary>
        /// Enumerates all descendants of the root node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An enumerable of descendant nodes.</returns>
        public IEnumerable<T> EnumerateDescendants<T>() where T : SceneNode => RootNode.EnumerateDescendants<T>();

        /// <summary>
        /// Enumerates all descendants of the root node of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        /// <returns>An enumerable of descendant nodes.</returns>
        public IEnumerable<T> EnumerateDescendants<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.EnumerateDescendants<T>(filter);

        /// <summary>
        /// Enumerates ancestors of the root node.
        /// </summary>
        /// <returns>An enumerable of ancestor nodes.</returns>
        public IEnumerable<SceneNode> EnumerateAncestors() => RootNode.EnumerateAncestors();

        /// <summary>
        /// Enumerates ancestors of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        /// <returns>An enumerable of ancestor nodes.</returns>
        public IEnumerable<SceneNode> EnumerateAncestors(SceneNodeFlags filter) => RootNode.EnumerateAncestors(filter);

        /// <summary>
        /// Enumerates ancestors of the root node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An enumerable of ancestor nodes.</returns>
        public IEnumerable<T> EnumerateAncestors<T>() where T : SceneNode => RootNode.EnumerateAncestors<T>();

        /// <summary>
        /// Enumerates ancestors of the root node of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        /// <returns>An enumerable of ancestor nodes.</returns>
        public IEnumerable<T> EnumerateAncestors<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.EnumerateAncestors<T>(filter);

        /// <summary>
        /// Gets all descendants of the root node.
        /// </summary>
        /// <returns>An array of descendant nodes.</returns>
        public SceneNode[] GetDescendants() => RootNode.GetDescendants();

        /// <summary>
        /// Gets all descendants of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        /// <returns>An array of descendant nodes.</returns>
        public SceneNode[] GetDescendants(SceneNodeFlags filter) => RootNode.GetDescendants(filter);

        /// <summary>
        /// Gets all descendants of the root node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An array of descendant nodes.</returns>
        public T[] GetDescendants<T>() where T : SceneNode => RootNode.GetDescendants<T>();

        /// <summary>
        /// Gets all descendants of the root node of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        /// <returns>An array of descendant nodes.</returns>
        public T[] GetDescendants<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.GetDescendants<T>(filter);

        /// <summary>
        /// Gets all ancestors of the root node.
        /// </summary>
        /// <returns>An array of ancestor nodes.</returns>
        public SceneNode[] GetAncestors() => RootNode.GetAncestors();

        /// <summary>
        /// Gets all ancestors of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        /// <returns>An array of ancestor nodes.</returns>
        public SceneNode[] GetAncestors(SceneNodeFlags filter) => RootNode.GetAncestors(filter);

        /// <summary>
        /// Gets all ancestors of the root node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An array of ancestor nodes.</returns>
        public T[] GetAncestors<T>() where T : SceneNode => RootNode.GetAncestors<T>();

        /// <summary>
        /// Gets all ancestors of the root node of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        /// <returns>An array of ancestor nodes.</returns>
        public T[] GetAncestors<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.GetAncestors<T>(filter);

        /// <summary>
        /// Finds a node by its path.
        /// </summary>
        /// <param name="path">The path to the node.</param>
        /// <returns>The found node.</returns>
        public SceneNode FindByPath(string path) => RootNode.FindByPath(path);

        /// <summary>
        /// Finds a node by its path and requires the final node to match the provided filter.
        /// </summary>
        /// <param name="path">The path to the node.</param>
        /// <param name="filter">The flags the final node must contain.</param>
        /// <returns>The found node.</returns>
        public SceneNode FindByPath(string path, SceneNodeFlags filter) => RootNode.FindByPath(path, filter);

        /// <summary>
        /// Attempts to find a node by its path.
        /// </summary>
        /// <param name="path">The path to the node.</param>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if the node was found; otherwise false.</returns>
        public bool TryFindByPath(string path, out SceneNode? node) => RootNode.TryFindByPath(path, out node);

        /// <summary>
        /// Attempts to find a node by its path and requires the final node to match the provided filter.
        /// </summary>
        /// <param name="path">The path to the node.</param>
        /// <param name="filter">The flags the final node must contain.</param>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if the node was found; otherwise false.</returns>
        public bool TryFindByPath(string path, SceneNodeFlags filter, out SceneNode? node) => RootNode.TryFindByPath(path, filter, out node);

        /// <summary>
        /// Enumerates siblings of the root node.
        /// </summary>
        /// <returns>An enumerable of sibling nodes.</returns>
        public IEnumerable<SceneNode> EnumerateSiblings() => RootNode.EnumerateSiblings();

        /// <summary>
        /// Enumerates siblings of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags sibling nodes must contain to be returned.</param>
        /// <returns>An enumerable of sibling nodes.</returns>
        public IEnumerable<SceneNode> EnumerateSiblings(SceneNodeFlags filter) => RootNode.EnumerateSiblings(filter);

        /// <summary>
        /// Gets all siblings of the root node.
        /// </summary>
        /// <returns>An array of sibling nodes.</returns>
        public SceneNode[] GetSiblings() => RootNode.GetSiblings();

        /// <summary>
        /// Gets all siblings of the root node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags sibling nodes must contain to be returned.</param>
        /// <returns>An array of sibling nodes.</returns>
        public SceneNode[] GetSiblings(SceneNodeFlags filter) => RootNode.GetSiblings(filter);

        /// <summary>
        /// Traverses all nodes in the scene.
        /// </summary>
        /// <param name="action">The action to perform on each node.</param>
        public void Traverse(Action<SceneNode> action) => RootNode.Traverse(action);

        /// <summary>
        /// Traverses all nodes in the scene that match the provided filter.
        /// </summary>
        /// <param name="action">The action to perform on each node.</param>
        /// <param name="filter">The flags nodes must contain to be visited.</param>
        public void Traverse(Action<SceneNode> action, SceneNodeFlags filter) => RootNode.Traverse(action, filter);

        /// <summary>
        /// Gets the first node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <returns>The first matching node.</returns>
        public T FirstOfType<T>() where T : SceneNode => RootNode.FirstOfType<T>();

        /// <summary>
        /// Gets the first node of the specified type that matches the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="filter">The flags the matching node must contain.</param>
        /// <returns>The first matching node.</returns>
        public T FirstOfType<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.FirstOfType<T>(filter);

        /// <summary>
        /// Attempts to get the first node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <returns>The first matching node, or null if not found.</returns>
        public T? TryGetFirstOfType<T>() where T : SceneNode => RootNode.TryGetFirstOfType<T>();

        /// <summary>
        /// Attempts to get the first node of the specified type that matches the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="filter">The flags the matching node must contain.</param>
        /// <returns>The first matching node, or null if not found.</returns>
        public T? TryGetFirstOfType<T>(SceneNodeFlags filter) where T : SceneNode => RootNode.TryGetFirstOfType<T>(filter);

        /// <summary>
        /// Attempts to get the first node of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if the node was found; otherwise false.</returns>
        public bool TryGetFirstOfType<T>(out T? node) where T : SceneNode => RootNode.TryGetFirstOfType<T>(out node);

        /// <summary>
        /// Attempts to get the first node of the specified type that matches the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="filter">The flags the matching node must contain.</param>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if the node was found; otherwise false.</returns>
        public bool TryGetFirstOfType<T>(SceneNodeFlags filter, out T? node) where T : SceneNode => RootNode.TryGetFirstOfType<T>(filter, out node);

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>
        /// Creates a new scene from a node.
        /// </summary>
        /// <param name="node">The node to use as the root of the new scene.</param>
        /// <returns>A new scene with the specified node.</returns>
        public static Scene CreateFromNode(SceneNode node)
        {
            var scene = new Scene(node.Name);
            node.MoveTo(scene.RootNode);
            return scene;
        }
    }
}
