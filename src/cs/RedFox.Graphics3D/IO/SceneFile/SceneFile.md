# RedFox Scene (`.rfscene`) — version 1.0

RedFox Scene is a little-endian binary format. Every value is written in a fixed order, so you read a file by walking it from start to end.

## Primitive encodings

| Name | Encoding |
| --- | --- |
| `bool` | 1 byte, 0 or 1 |
| `i32` / `u32` / `i64` / `u64` / `f32` / `f64` | Standard little-endian |
| `u128` | Low `u64`, then high `u64` |
| `string` | 7-bit encoded length (.NET `BinaryWriter.Write(string)`), then that many UTF-8 bytes |
| `vec2` / `vec3` / `vec4` | 2, 3 or 4 `f32` values |
| `quat` | 4 `f32` values: X, Y, Z, W |
| `mat4` | 16 `f32` values, row-major (`System.Numerics.Matrix4x4`) |
| `optional<T>` | `bool` present, then `T` if present |
| `array<T>` | `i32` count, then `count` × `T` |
| `strings` | `i32` count, then `count` × `string` |
| `node` | `i32` index into the node table; -1 means null |
| `nodes` | `i32` count, then `count` × `node` |
| `curve?` | `bool` present, then a `curve` if present |

### `buffer`

| Field | Type |
| --- | --- |
| Type | `u8` (`SceneBufferType`). 0 means null, and nothing else follows. |
| ElementCount | `i32` |
| ValueCount | `i32` |
| ComponentCount | `i32` |
| Data | ElementCount × ValueCount × ComponentCount components, element-major, then value, then component |

`SceneBufferType` values: 1 Int8, 2 UInt8, 3 Int16, 4 UInt16, 5 Int32, 6 UInt32, 7 Int64, 8 UInt64, 9 Half, 10 Single, 11 Double. Packed vector buffers are written unpacked as Single.

### `attribute`

An attribute is a `u8` tag followed by its value. The tags are:

| Tag | Value |
| --- | --- |
| 1 | `bool` |
| 2 | `i32` |
| 3 | `u32` |
| 4 | `i64` |
| 5 | `u64` |
| 6 | `u128` |
| 7 | `f32` |
| 8 | `f64` |
| 9 | `string` |
| 10 | `vec2` |
| 11 | `vec3` |
| 12 | `vec4` |
| 13 | `quat` |
| 14 | `mat4` |

Values of any other type are written as their `ToString()` with tag 9.

## File layout

```
u8[4]   Magic "RFSC"
u16     Major version (1)
u16     Minor version (0)
i32     Node count
Node table: per node
    string  Type name
    i32     Parent index (-1 = scene root)
Scene record
Node records, in node-table order
```

A **record** is an `i32` byte size followed by that many bytes of fields.

- Readers must seek to the end of each record after reading the fields they know.
- New fields are only ever appended to the end of a record, together with a minor version bump. Older readers stay compatible because they skip what they don't read.
- A different major version is not compatible.
- Every record starts with the **SceneNode** fields. A reader can load any node type it doesn't recognise as a plain group: read the SceneNode fields, then skip the rest of the record.
- Nodes are listed parent-first, so a node's parent always comes earlier in the table.

## Record fields

Each type's fields come after the fields of its base type.

| Type (base) | Fields |
| --- | --- |
| **SceneNode** | `string` Name, `i32` Flags, `u128` UserId, `optional<vec3>` BindLocalPosition, `optional<quat>` BindLocalRotation, `optional<vec3>` BindWorldPosition, `optional<quat>` BindWorldRotation, `optional<vec3>` BindScale, `optional<string>` UserData, `i32` attribute count, then count × (`string` key, `attribute`) |
| **Scene** (SceneNode) | `i32` UpAxis (0 Y, 1 X, 2 Z) |
| **Group**, **MeshGroup**, **MaterialGroup**, **Skeleton** (SceneNode) | No additional fields |
| **SkeletonBone** (SceneNode) | `bool` ShowSkeletonBone, `bool` RenderBoneOnTop, `f32` BoneAxisSize, `f32` BoneAxisScaleFromParent, `f32` BoneAxisMaxSize, `f32` BoneLineWidth, `vec4` BoneAxisXColor, `vec4` BoneAxisYColor, `vec4` BoneAxisZColor, `vec4` BoneConnectionColor, `bool` UseBoneNameHashColor, `f32` BoneNameColorSaturation, `f32` BoneNameColorValue |
| **Mesh** (SceneNode) | `buffer` Positions, `buffer` Normals, `buffer` Tangents, `buffer` BiTangents, `buffer` ColorLayers, `buffer` UVLayers, `buffer` FaceIndices, `optional<nodes>` Materials, `optional<array<(i32 start, i32 count)>>` MaterialIndexRanges, `optional<skin>` Skin, `optional<morph>` Morph |
| **Material** (SceneNode) | Eleven `optional<string>` map names: Diffuse, Specular, Metallic, Gloss, Roughness, Normal, Emissive, EmissiveMask, AmbientOcclusion, Cavity, Anisotropy. Nine `optional<vec4>` colors: Diffuse, Specular, Metallic, Gloss, Roughness, Emissive, AmbientOcclusion, Cavity, Anisotropy. Then `optional<f32>` Shininess, `optional<f32>` SpecularStrength, `optional<f32>` Metallic, `optional<f32>` Roughness, `bool` DoubleSided, `bool` SpecularMaskInDiffuseAlpha, `bool` UseSpecularColorMap, `optional<string>` Type, `i32` binding count, then count × (`node` Texture, `i32` Slot, `string` SamplerUniform) |
| **Texture** (SceneNode) | `string` FilePath, `optional<string>` ResolvedFilePath |
| **SceneReference** (SceneNode) | `string` FilePath, `optional<string>` ResolvedFilePath |
| **Light** (SceneNode) | `vec3` Color, `f32` Intensity, `bool` Enabled |
| **Camera** (SceneNode) | `vec3` Position, `vec3` Target, `vec3` Up, `i32` Projection, `f32` FieldOfView, `f32` OrthographicSize, `f32` NearPlane, `f32` FarPlane, `f32` AspectRatio, `f32` LookSensitivity, `f32` ZoomSensitivity, `f32` PanSensitivity, `f32` MoveSpeed, `f32` BoostMultiplier, `bool` InvertX, `bool` InvertY |
| **Hair** (SceneNode) | `array<i32>` StrandSegments, `buffer` Particles, `node` Material |
| **Constraint** (SceneNode) | `node` ConstrainedNode, `node` SourceNode, `f32` Weight |
| **OrientConstraint** (Constraint) | `quat` RotationOffset, `bool` SkipX, `bool` SkipY, `bool` SkipZ |
| **ParentConstraint** (Constraint) | `vec3` TranslationOffset, `quat` RotationOffset |
| **PointConstraint** (Constraint) | `vec3` TranslationOffset, `bool` SkipX, `bool` SkipY, `bool` SkipZ |
| **ScaleConstraint** (Constraint) | `vec3` ScaleOffset, `bool` SkipX, `bool` SkipY, `bool` SkipZ |
| **IKHandle** (SceneNode) | `node` StartNode, `node` EndNode, `node` TargetNode, `vec3` TargetOffset, `node` PoleVectorNode, `node` PoleNode, `bool` UseTargetRotation, `f32` Weight |
| **Animation** (SceneNode) | `f32` Framerate, `optional` Actions: `i32` count, then count × (`string` Name, `string` Type, `i32` keyframe count, then keyframe count × (`f32` Frame, `optional<string>` Value, the value's `ToString()`)) |
| **SkeletonAnimation** (Animation) | `i32` TransformType, `i32` TransformSpace, `i32` track count, then count × SkeletonAnimationTrack fields |
| **MorphAnimation** (Animation) | `i32` track count, then count × MorphAnimationTrack fields |

## Nested data

Tracks are written inline inside their animation. They have no node-table entry and no record size.

| Type (base) | Fields |
| --- | --- |
| **AnimationTrack** (SceneNode) | `curve?` Translation, `curve?` Rotation, `curve?` Scale, `i32` custom curve count, then count × (`string` name, `curve`) |
| **SkeletonAnimationTrack** (AnimationTrack) | `i32` TransformSpace, `i32` TransformType |
| **MorphAnimationTrack** (SceneNode) | `curve?` Weight |
| **curve** | `buffer` Keys (times), `buffer` Values, `i32` TransformSpace, `i32` TransformType, `f32` BlendWeight |
| **skin** | `optional<string>` Name, `i32` SkinningMode, `nodes` Bones, `buffer` BoneIndices, `buffer` BoneWeights, `optional<array<mat4>>` InverseBindMatrices |
| **morph** | `optional<string>` Name, `strings` TargetNames, `array<f32>` Weights, `buffer` DeltaPositions, `buffer` DeltaNormals, `buffer` DeltaTangents |

## Not stored

- `LiveTransform`: runtime pose state.
- Image data: textures store paths only.
- `Material.Version`: an in-memory change counter.
- Runtime nodes (`AnimationPlayer`, `AnimationSampler*`) and any other node type without a registered type name are left out together with their subtree.
- Camera subclasses are stored as `Camera`.
