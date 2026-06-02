using System;
using System.Collections.Generic;
using System.Linq;
using RedFox.Graphics2D.IO;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a 3D scene. The scene is itself the root <see cref="SceneNode"/> of its graph;
    /// its direct children are the top-level nodes of the scene. Because <see cref="Scene"/> derives
    /// from <see cref="SceneNode"/>, the full node API (adding, enumerating, finding, and traversing
    /// nodes) is available directly on the scene without going through a separate root node.
    /// </summary>
    public class Scene : SceneNode
    {
        private const float DefaultAnimationFrameRate = 30.0f;

        /// <summary>
        /// Occurs when the scene graph changes.
        /// </summary>
        public event EventHandler<SceneChangedEventArgs>? Changed;

        /// <summary>
        /// Gets the scene graph version. This value increments when nodes are added, removed, or cleared.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>
        /// Gets the root node of the scene. The scene is its own root node, so this returns the scene
        /// itself. The property is retained for readability and backward compatibility; new code can use
        /// the inherited <see cref="SceneNode"/> members (for example <see cref="SceneNode.AddNode{T}(T)"/>)
        /// directly on the scene.
        /// </summary>
        public SceneNode RootNode => this;

        /// <summary>
        /// Gets the scene-owned grid rendering settings.
        /// </summary>
        public Grid Grid { get; }

        /// <summary>
        /// Gets the scene-owned skybox rendering settings.
        /// </summary>
        public Skybox Skybox { get; }

        /// <summary>
        /// Gets the image translator registry shared by textures in this scene.
        /// Register image translators here so texture nodes can load images through a common scene-owned pipeline.
        /// </summary>
        public ImageTranslatorManager ImageTranslators { get; } = new();

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
        public Scene(string name) : base(name)
        {
            Grid = new Grid();
            Skybox = new Skybox();
            SetScene(this);
        }

        /// <summary>
        /// Initializes a new instance with a default name.
        /// </summary>
        public Scene() : this("Untitled Scene")
        {

        }

        /// <summary>
        /// Commits the children of <paramref name="source"/> into this scene's root, resolving
        /// duplicates according to <paramref name="options"/>.
        /// </summary>
        /// <param name="source">The node whose children are committed into this scene.</param>
        /// <param name="options">The options controlling duplicate detection and resolution.</param>
        public void Merge(SceneNode source, SceneMergeOptions options)
            => SceneMerger.MergeChildren(this, source, options);

        /// <summary>
        /// Removes all nodes of the specified type from the scene.
        /// </summary>
        /// <typeparam name="T">The node type to remove.</typeparam>
        public void RemoveAll<T>() where T : SceneNode
        {
            foreach (T node in GetDescendants<T>())
            {
                node.MoveTo(null);
            }
        }

        /// <summary>
        /// Updates the animation players owned by this scene. Invoked by the inherited
        /// <see cref="SceneNode.Update(float)"/> before the scene graph is traversed.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        protected override void OnUpdate(float deltaTime)
        {
            if (!IsAnimationPaused && deltaTime > 0.0f && AnimationPlayers.Count > 0)
            {
                foreach (AnimationPlayer player in AnimationPlayers)
                {
                    player.Update(deltaTime, AnimationSampleType.DeltaTime);
                }
            }
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

        internal void NotifyChanged(SceneChangeKind kind, SceneNode? node)
        {
            Version++;
            Changed?.Invoke(this, new SceneChangedEventArgs(kind, node, Version));
        }

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
            node.MoveTo(scene);
            return scene;
        }
    }
}
