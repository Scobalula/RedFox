using RedFox.Graphics3D.Skeletal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D
{
    public class Graphics3DScene
    {
        /// <summary>
        /// Gets or Sets any objects read by the translator.
        /// </summary>
        public List<Graphics3DObject> Objects { get; set; }

        /// <summary>
        /// Gets or Sets the scale to apply on export if supported by the translator.
        /// </summary>
        public float Scale { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Graphics3DTranslatorIO"/> class.
        /// </summary>
        public Graphics3DScene()
        {
            Objects = new();
            Scale = 1.0f;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Graphics3DTranslatorIO"/> class.
        /// </summary>
        public Graphics3DScene(Graphics3DObject obj)
        {
            Objects = new();
            Scale = 1.0f;

            Objects.Add(obj);
        }

        public T? GetFirstInstance<T>() where T : Graphics3DObject
        {
            var type = typeof(T);
            return (T?)Objects.FirstOrDefault(x => x.GetType() == type);
        }

        /// <summary>
        /// Attempts to get the first skeleton, if none provided, tries to aquire it from the animation or models provided.
        /// </summary>
        /// <param name="skeleton">The resulting skeleton.</param>
        /// <returns>True if found, otherwise false.</returns>
        public bool TryGetFirstSkeleton([NotNullWhen(true)] out Skeleton? skeleton)
        {
            skeleton = GetFirstInstance<Skeleton>();
            if (skeleton == null)
                skeleton = GetFirstInstance<Model>()?.Skeleton;
            if (skeleton == null)
                skeleton = GetFirstInstance<SkeletonAnimation>()?.Skeleton;
            return skeleton != null;
        }

        public IEnumerable<T> EnumerateObjectsOfType<T>() where T : Graphics3DObject
        {
            var type = typeof(T);

            foreach (var obj in Objects)
            {
                if (obj.GetType() == type)
                {
                    yield return (T)obj;
                }
            }
        }

        public Graphics3DObject AddObject(Graphics3DObject obj)
        {
            Objects.Add(obj);
            return obj;
        }

        public T AddObject<T>() where T : Graphics3DObject, new()
        {
            var obj = new T();
            Objects.Add(obj);
            return obj;
        }

        public T AddObject<T>(string name) where T : Graphics3DObject, new()
        {
            var obj = new T()
            {
                Name = name
            };
            Objects.Add(obj);
            return obj;
        }

        public Graphics3DScene Clear()
        {
            Objects.Clear();
            return this;
        }
    }
}
