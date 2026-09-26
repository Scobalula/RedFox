# Known Issues

## Cast.NET: `ModelNode.Skeleton` throws on models without a skeleton

`CastNode.TryGetFirstChild<T>` casts `Children[0]` to `T` without checking its type. On a model whose first child is a mesh, `ModelNode.Skeleton` throws `InvalidCastException`.

RedFox works around this in `CastModelTranslator` with `EnumerateChildrenOfType<SkeletonNode>().FirstOrDefault()`. The real fix belongs in Cast.NET: `TryGetFirstChild<T>` should return the first child that is a `T`.
