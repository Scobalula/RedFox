# Codebase Audit (2026-09-26)

Read-only audit of `src/cs` (excluding `RedFox.Tests`). Paths are relative to `src/cs`. Line numbers are from the audit date and will drift.

Suggested order: native interop and security, then format data corruption (one format per pass, verified on sample files), then scene-graph core, then the mechanical cleanup pass.

## 1. Critical: crashes, memory corruption, security

### Native interop (Audio / Compression)
- [x] `RedFox.Audio.Flac/FlacCodec.cs:220-224` — frame arg is a struct pointer but `frame[0]` is dereferenced as a pointer (AV); field offsets wrong (+4 is sample_rate). Use `FlacFrameHeader*`: blocksize +0, channels +8, bits +16.
- [x] `RedFox.Audio.Flac/FlacInterop.cs:308-312` + `FlacCodec.cs:211` — `ReadAbort = 1` is actually END_OF_STREAM (ABORT = 2); read callback returns CONTINUE with 0 bytes at EOF, which spins forever. Fix enum, return EndOfStream.
- [x] `FlacCodec.cs:83-86` — callback delegates are locals and can be collected while native holds them; `:250` throws across native frames. Store in fields, record errors instead of throwing.
- [x] `FlacCodec.cs:235` — `(short)` truncates 24-bit/8-bit samples; `:183` `encodedSize*16` output bound too small. Scale by bits-per-sample, size from STREAMINFO.
- [x] `ZStandardInterop.cs:27`, `LZ4Interop.cs:44`, `OpusInterop.cs:52` — `string` return under LibraryImport frees static `const char*` (heap corruption on every error path). Return `nint`, use `Marshal.PtrToStringUTF8`.
- [x] `LZ4Interop.cs:20` — `LZ4_compress_fast` bound with 4 of 5 args. Bind `LZ4_compress_default`.
- [x] `LZ4FrameCodec.cs:23,61` — Compress writes block format with block bound (frame decoder can't read it); `:37-54` context never freed, create error ignored, `size_t` as `int`, partial frame treated as success.
- [x] `GDeflateInterop.cs:21` + `GDeflatePage.cs:10` — `size_t`/`out` declared `int` (native writes 8 bytes into 4). `GDeflateCodec.cs:45` leaks a decompressor per call; `:54-77` no bounds checks on untrusted tile count/offsets; `:81` result ignored.
- [x] `OodleCodec.cs:9,14` — SINTa params as `int`, 0 failure unchecked; static fn pointer/handle set per instance, never freed; `Flags` (`:18`) and `Dispose` (`:65`) throw NotImplementedException.
- [x] `OpusDecoder.cs:81`, `OpusEncoder.cs:205` — `frameSize*Channels` never checked against span length (native overflow through public API).

### Security / data integrity
- [x] `RedFox.GameExtraction/AssetExportContext.cs:328-345`, `AssetManager.cs:889-918` — path normalisation keeps `..`; ZIP entry `../../x.dll` writes outside the output root. Base handlers `Path.Combine(OutputDirectory, asset.Name)` unchecked. Reject `..`, verify final path is under root.
- [x] `RedFox.GameExtraction.Hashing/NameFile.cs:155-163` — checksum check is a no-op (hashes into the stored buffer, compares to zeros, throws on match). Read into `expected`, hash into `checksum`, throw on mismatch.
- [x] Untrusted sizes drive allocation/indexing: `Tiff/TiffIfdReader.cs:21-27,126`, `Jpeg/JpegDecoder.cs:231-233,246,276,327`, `Png/PngDecompressor.cs:16`.

### Crashes
- [x] `RedFox.GameExtraction.UI/ViewModels/MainWindowViewModel.cs:65` — `new PythonPluginHost()` throws without Python; app crashes on startup. `Dispose` (675) never disposes `Plugins`.
- [x] `RedFox.Graphics3D.D3D11/D3D11GraphicsDevice.cs:849-868` — no 2-component formats; any mesh with UVs throws. Add `(Float16,2)`, `(Float32,2)`.
- [x] `RedFox.Graphics3D/MeshAdjacency.cs:472` — `vert-- != uint.MaxValue` runs once with `uint.MaxValue`; crashes whenever epsilon > 0. Use `--vert`.
- [x] `RedFox.Graphics3D.Rendering/SceneRenderer.cs:683-713` — background loader only catches IO/NotSupported; any other decoder exception kills the process.
- [x] `IdTech/Md5MeshReader.cs:182-195`, `Md5AnimReader.cs:312-381` — `continue` paths don't consume the bad token; malformed input loops forever.
- [x] `RedFox.Graphics3D/SceneNode.cs:1852,1886` — `"/"` path throws on `segments[0]`, including `TryFindByPath`.

## 2. Wrong output / data corruption

### Graphics3D.Formats
- [x] `GLTransmissionFormat/GltfWriter.cs:551-554` — inverse bind matrices always Identity. Write `Invert(bone bind world)`.
- [x] `GltfReader.cs:813-835` / `GltfWriter.cs:750` — glTF times are seconds, RedFox uses frames; reader never converts or sets `Framerate`, writer writes frames as seconds.
- [x] `IdTech/Md5MeshWriter.cs:103`, `Md5AnimWriter.cs:153,219` — quaternion written without forcing W ≤ 0; reader rebuilds `w = -sqrt`. Negate when `W > 0`.
- [x] `Md5AnimReader.cs:120-198`, `Md5AnimWriter.cs:70-76` — md5anim values are parent-local but treated as object space; reader mixes conventions.
- [x] `SEAnim/SeanimTranslator.cs:172-174` — `frameCount` = max frame, not +1; 256 keys wraps byte index. Use `maxFrame + 1`.
- [x] `ActorX/PsaWriter.cs:105` — `TrackTime` written as seconds; format expects frame count.
- [x] `IwEngine/XAnimWriter.cs:19-49` — local/relative curves are written as world.
- [x] `IwEngine/XAnimWriter.cs:19-49` — `NUMFRAMES` counts distinct keyed frames instead of the highest frame index plus one.
- [x] `KaydaraFbx/FbxGeometryMapper.cs:225-256,407-437` — per-polygon-vertex normals/UVs merged onto control points (hard edges and seams lost). Split vertices on unique attribute tuples, then duplicate skin and morph data.
- [x] `FbxGeometryMapper.cs:101,279,643-657` — tangent XYZ uses stride 3 and handedness reads/writes separate `TangentsW` data.
- [x] `KaydaraFbx/FbxAsciiTokenizer.cs:334-340` — backslashes remain literal; serializer writes and reads quotes as `&quot;`.
- [x] `FbxSceneMapper.cs:1599-1605` — meshes under bones/meshes re-rooted; `:1659` bakes geometric transform into the node so children inherit it.
- [x] `FbxSceneMapper.cs:2143` — export writes `Emissive`, import reads `EmissiveColor`; `:1768` writes a one-component `ColorRGB`.
- [x] `FbxDocumentSerializer.cs:1828` — raw `'R'` properties emitted to ASCII as unescaped UTF-8; `:136` mutates caller's document during `Write`.
- [x] `MayaAscii/MayaAsciiWriter.cs:970` vs `881-898` — bind rotation into `.jo` and absolute into `.r` (double rotation); `:859` ignores framerate (always 24 fps).
- [x] `WavefrontObj/ObjReader.cs:580-604` — `v x y z r g b` throws; `:512` missing vt/vn defaults to 0 not -1; `:455` `stackalloc int[64]` throws on >64-vertex faces.
- [x] `StudioMDL/SmdReader.cs:249` — material names starting with a digit break parsing; `:482` culture-sensitive `int.TryParse`.
- [x] `SEModel/SemodelTranslator.cs:318,428` — mesh without UVs loses its material; `:378` vs `XModelWriter.cs:156` disagree on colour range.
- [x] `SEModel/SemodelTranslator.cs:280,438` write names as ASCII, read UTF-8; `SeanimTranslator.cs:343` decodes Latin-1.
- [x] `Cast/CastModelTranslator.cs:39` — every model exports every skeleton in the selection; `CastTranslator.cs:52` hardcodes `UpAxis = "z"`; `:106` `CurrentCultureIgnoreCase`.
- [x] `BiovisionHierarchy/BvhWriter.cs` — requires a single root bone; skeletons with multiple roots now fail (previously the synthetic container acted as root). Decide whether to emit the `Skeleton` as the root joint.

### Graphics3D core
- [x] `SceneNode.cs:618,627` — `EnumerateDescendants<T>`/`GetDescendants<T>` match exact type only; `GetDescendants<Camera>()` misses subclasses (FBX export drops cameras). `FirstOfType<T>` mixes both. Use `OfType` everywhere.
- [x] `MeshOptimizer.cs:142,288` — `LruState.ActiveFaceCount` never filled; scores go NaN.
- [x] `MeshOptimizer.cs:158,229` — invalid faces never emitted; remap tail stays 0.
- [x] `SceneNode.cs:1649,1692` — `AddNode<T>(string name)` checks duplicates before renaming.
- [x] `SceneNode.cs:177-183` — `MoveTo(null)` defaults to PreserveWorld but only detaches.
- [x] `SceneNode.cs:3051-3082` vs `3351` — world pose rigid but world matrices include scale; inconsistent under scaled parents.
- [x] `SceneMerger.cs:236-239` — `Attach` via `AddNode` always rejects case-insensitive duplicates; ignores transform mode.
- [x] `FileSystemImageLoader.cs:433` — loads `FilePath` instead of `EffectiveFilePath`; relative textures never load.
- [x] `Light.cs:185-189` — getter returns world position, setter writes local.
- [x] `Material.cs:353-354` — numeric key `"0"` stored as `"Texture0"`; lookups/disconnect by `"0"` fail; next-free slot can collide.
- [x] `IO/SceneTranslator.cs:219-221` — `CreateReadContext` mutates caller's shared options (race).
- [x] `Solvers/TwoBoneIKSolver.cs:139` — mid-bone world rotation omits `rootRotDelta`; `CCDIKSolver.cs:126` same, weight compounds per iteration.
- [x] `SkeletonAnimationSampler.cs:149,166` — additive branch ignores `resolved`.
- [x] `AnimationPlayer.cs:228` — live transforms never reset before sampling (additive/partial/IK accumulate); solvers get raw delta.
- [x] `Buffers/PackedVector/Float3PK.cs:97` — tiny values not rejected; shift wraps at 32.
- [x] `FpsCamera.cs:590-599` — dolly discarded by `Normalize(move)`.

### Imaging
- [x] `Jpeg/JpegIdct.cs:71,143` — zero-AC shortcuts use the corrected `<<2`/`>>5` fixed-point scale.
- [x] `Tiff/TiffIfdReader.cs:81,83` — inline SHORT/BYTE values now respect TIFF byte order.
- [x] `Exr/ExrCompression.cs:95,113` (+165-189) — RLE control-byte signs match OpenEXR.
- [x] `Exr/ExrWriter.cs:218-220` — PXR24/B44 input is channel-major; `SmallestSize` uses lossless ZIP compression.
- [x] `Jpeg/JpegDecoder.cs:492-496,373-393` — single-component baseline scans walk the component block grid.
- [x] `Tiff/TiffImageTranslator.cs:71,127` — RowsPerStrip values are bounded by image height before conversion.
- [x] `Bmp/BmpImageTranslator.cs` — indexed alpha is opaque; V4/V5 masks are read from the header; absent alpha masks stay zero; channel scaling avoids negative shifts.
- [x] `Dds/DdsLegacyFormatMapper.cs:98` — legacy RGB24 pixels expand to B8G8R8X8; `Dds/DdsPitchCalculator.cs:23-25` BC `LinearSize` includes block rows.
- [x] `Jpeg/JpegDecoder.cs:954-960` — CMYK/YCCK conversion uses all four channels; 2-component and non-8-bit JPEGs are rejected.
- [x] `Jpeg/JpegEncoder.cs:566`, `Tga/TgaHeader.cs:88` — dimensions are range-checked; TGA/JPEG metadata reads support non-seekable streams.
- [x] `Exr/ExrHuffmanBitReader.cs:80-93` — peeks fill only the requested bits and stay within the 64-bit buffer.
- [x] `RedFox.Imaging/BlockColorOperations.cs:78,147` — BC4/BC5 SNORM endpoints are compared as signed bytes.
- [x] `R11G11B10FloatCodec.cs:89,107,117-162` — subnormal scaling, finite clamping, and 10-bit infinity encoding are corrected.
- [x] `BC6HCodec.cs:737-756` — unrepresentable swapped deltas are rejected to preserve endpoint/index correspondence.
- [x] `ImageTranslatorManager.cs:86-99` — read and write selection honors CanRead and CanWrite.
- [x] `RedFox.Imaging.Vulkan/VulkanBcContext.cs:659-682` — dispatch passes are split into at most 65535 groups with correct StartBlockId values.
- [x] `VulkanBcContext.cs:92` — failed initialization disposes the context; failed pipeline creation releases layouts.

### Audio / IO / Patterns
- [x] `ImaAdpcmCodec.cs:126` — each block is written at the next block-aligned offset.
- [x] `ImaAdpcmCodec.cs:182-186` — stereo data follows WAV IMA's channel chunks; unsupported channel counts are rejected and encoded block tails are cleared.
- [x] `MsAdpcmCodec.cs:46-48` — stereo sample counts, high-first nibble order, and interleaved channel headers match WAV MS-ADPCM layout.
- [x] `MurMur3Hash.cs:66-70` — hashes only the requested array segment and carries incomplete words across streaming updates.
- [x] `ZStandardCodec.cs:61` — unknown frame sizes return -1 and invalid sizes throw; `DeflateCodec.cs:30,66` pins empty spans safely.
- [x] `BytePattern.cs:27-48` — wildcard and malformed byte pairs retain their token boundaries.
- [x] `VirtualDirectory.cs:275` — recursive enumeration starts from the selected directory; virtual file and directory hashes use ordinal case-insensitive comparison.
- [x] `SpanReader.cs:475-497` / `MemoryReader.cs:514-537` — Seek validates before assignment and uses standard end-relative offsets; positional read argument order is consistent.
- [x] `BinaryReaderExtensions.cs:30,69,105` — structure reads fill buffers despite legal short reads.
- [x] `ProcessReader.cs:185`, `SpanReader.cs:96` — array byte counts are checked for overflow.

### Rendering
- [x] `SceneRenderResources.cs:10` — render handles are isolated by graphics device and released with that device's renderer.
- [x] `AvaloniaOpenGlRendererControl.cs:277-280,315-357` — scene subscriptions and resource release follow the active `ViewportController.Scene ?? Scene`.
- [x] `Handles/MaterialRenderHandle.cs:219-233` — `HasDiffuseMap` is cleared first and enabled only after a valid texture binds.
- [x] `Handles/MeshRenderHandle.cs:388-453`, `MeshGpuBufferBinding.cs:149` — bindings detect replaced source buffers and upload the new data.
- [x] `AvaloniaCameraInputAdapter.cs:34-39` — handlers use Bubble routing once.
- [x] `SkeletonAnimationCurveViewer.cs:671-672` — brush updates preserve existing property bindings.
- [x] `SceneRenderer.cs:440` — the renderer releases its resources without disposing the injected graphics device.
- [x] `Handles/MeshRenderHandle.cs:483-521` — explicit material index ranges select each material's indices; layered meshes use their corresponding UV layer.

### GameExtraction / Plugins / Zenith
- [x] `RedFox.GameExtraction.Template/ModelHandler.cs:89`, `AnimationHandler.cs:91` — handlers export the stored `Scene`; `Template.Cli/Program.cs` displays the read result payload.
- [x] `NameTableManager.cs:189` — table names are matched with `StringComparison.OrdinalIgnoreCase`.
- [x] `AssetExportContext.cs:39,207,349` — export options default to an empty dictionary; `PreserveDirectoryStructure=false` resolves files at the output root.
- [x] `AssetHandlers/ModelHandler.cs:47` — model paths preserve dotted names without splitting at the first period.
- [x] `RedFox.Zenith/DataStorages/LocalDataStorage.cs:57` vs `:33` — *(N/A: project not in repo.)*
- [x] `RedFox.GameExtraction.HashBuilder/Program.cs` — `--algorithm` and `--compress` now match the usage text; compression is opt-in.
- [x] `RedFox.Plugins/PluginManager.cs:123-129` — failed initialization raises `Unloading` and tears down the host.
- [x] `RedFox.Plugins.Python/PythonResolver.cs` — stdout and stderr are drained concurrently, timeout is enforced, and common-location probing includes Python 3.14.
- [x] `PythonPluginHost.cs:118-120` — unload removes only the plugin's own module from `sys.modules`.
- [x] `MainWindowViewModel.cs:883-970` — process discovery runs on a worker task; each operation clears `_currentCts` only when it still owns it.
- [x] `ZipAssetSourceReader.cs` + `AssetManager.cs` — entries are staged before mount, cancellation disposes the archive, and ZIP access is synchronized.
- [x] `Template.Avalonia/Program.cs` — setting groups use "Export" and each option is declared once.
- [x] `MainWindowViewModel.cs` — preview reference count displays the read result's direct reference count.

## 3. Performance
- [x] `SceneNode.cs:2034-2066` — built-in nodes remap cloned references in one pass; legacy `Swap` overrides keep their pairwise fallback.
- [x] `SceneNode.cs:1759-1769` — `Update` visits children without allocating a closure per node.
- [x] Mesh and bounds batches reuse world matrices, and solvers share combined pose reads; direct getters still compose live transforms recursively because `Transform` setters have no cache invalidation signal.
- [x] `SceneNode.cs:576` — descendant enumeration uses one iterative traversal instead of nested recursive iterators.
- [x] `SceneNode.cs:1624` — sibling-name counts are indexed once a parent has eight children; the small-child path avoids a LINQ closure.
- [x] `SceneNode.GetBestParent` — SMD and BVH animation output precomputes exported parents once instead of rebuilding a candidate set per bone and frame.
- [x] SMD/BVH animated world transforms are memoized for each frame; MD5 already writes local transforms directly.
- [x] Mesh baking plus OBJ, SEModel, and XModel exports precompute inverse-transpose skin matrices once per bone and share them across vertices.
- [x] Mesh baking and bulk exporters pass precomputed inverse-transpose transforms to `GetVertexNormal`; the convenience overload retains its per-influence fallback for individual queries.
- [x] `MeshOptimizer.cs:203` — reuse two LRU cache arrays instead of allocating one per face.
- [x] `PackedBuffer.Get` reads components directly for every built-in packed vector type; custom implementations retain the compatible full-vector fallback.
- [x] `IwEngine/XModelReader.cs` — object/material group counts are accumulated once rather than recounted for every output mesh.
- [x] XModel and SMD imports weld exact duplicate vertices after building each mesh, preserving all vertex attributes and rewriting face indices.
- [x] `XModelWriter` and `MayaAsciiWriter` reuse one skin-weight list while writing vertices.
- [x] `FbxSkinningMapper.cs:86` — FBX influences use one flat buffer with per-vertex ranges instead of a dictionary and one list per vertex.
- [x] `BvhReader.cs:241` builds motion-value error text only when parsing fails.
- [x] `GltfReader.cs:696-726` — parent-node lookups use a cached parent index and each skin builds its joint set once.
- [x] `ExrPizCompression.cs` rents the large Huffman codebook and decoded-word workspaces for each chunk.
- [x] `ExrB44Compression.cs` reuses stack buffers and scalar min/max for each 4×4 block.
- [x] `ExrLoader.cs` — channel names are matched without allocating an uppercase string.
- [x] JPEG Huffman decoding uses an 8-bit prefix table for short codes and retains the bitwise fallback for longer codes.
- [x] JPEG entropy input reads exposable `MemoryStream`s directly and uses a pooled read buffer for `FileStream`s and non-exposable `MemoryStream`s; other streams keep the byte-read fallback.
- [x] JPEG YCbCr SIMD paths widen contiguous bytes with unaligned vector loads instead of scalar gathers.
- [x] `Dds/DdsLoader.cs:34-38` — stream loading reads from the `MemoryStream` buffer directly and avoids the full-file `ToArray` copy.
- [x] `FlacCodec.cs:347` — encoder callbacks append bytes without allocating a temporary array.
- [x] `LzoCodec.cs:41` rents the 128 KB match table from `ArrayPool<int>` and returns it after compression.
- [x] `R11G11B10FloatCodec` uses `ScaleB` instead of `Math.Pow` per channel.
- [x] `SceneRenderer` resolves scene render handles once per frame and reuses them across render phases, removing repeated CWT lookups.
- [x] `AvaloniaOpenGlRendererControl` requests another frame only while animation or input remains active, and input changes request a frame.
- [x] `AvaloniaOpenGlRendererControl` caches default-framebuffer size and sample queries until its handle or expected dimensions change.
- [x] `MeshRenderHandle` reuses the heap matrix buffer for skins with more than 128 bones instead of allocating it on every update.
- [x] `SceneRenderer` reuses its `RenderFrameContext` and service dictionary across frames.
- [x] `MeshRenderHandle` skips skin and morph GPU uploads while their values are unchanged; shader skinning draws per material/range, not per bone.
- [x] Rendering review: OpenGL queries are limited to setup and texture uploads; D3D11 reuses constant buffers and staging arrays per shader slot, updating contents per draw.
- [x] GameExtraction: `AssetManager` compacts removed assets in one pass; `AssetRowViewModel.Name`/`Size` are cached; ZIP mount progress is reported in batches.
- [x] GameExtraction filter changes are debounced.
- [x] GameExtraction DataGrids use compiled bindings with explicit item types on text columns and the Explorer icon template.

## 4. Cleanup / dead code / duplication
- [x] Reviewed the 74 `SceneNode` `Action`/predicate wrappers: retain them as a public convenience API because they execute immediately while the `IEnumerable` forms are deferred. They are repetitive, but removing them would break downstream callers and eliminate the eager traversal form.
- [x] `SceneMerger` shares `SceneNode`'s duplicate search, unique-name generation, and reference redirection instead of maintaining duplicate helpers.
- [x] Reviewed `SceneMerger.MergeNode` and `SceneNode.AttachInto`; retain their parallel strategy handling because their cycle validation, recursive scope, match-type, and staging-root semantics differ.
- [x] Name comparison: `SceneNode` defaults and `SceneMergeOptions.NameComparison` use `OrdinalIgnoreCase`; explicit `StringComparison` overloads retain caller control.
- [x] Removed unused `SceneRenderType` and the BC7 TryMode02 pipeline that was created and destroyed but never dispatched. Several original candidates are live, including `Scene.RootNode`, `FaceNormalDot`, `FbxArrayFactory`, `AttachNullNodeAttributes`, `OodleInterop`, `IValueManipulator` and its interpolators, `JpegIdct.TransformSse2`, `JpegEncoderOptions.OptimizeHuffmanTables`, and the OpenGL compute program.
- [x] Reviewed public API candidates with no in-repo callers: removed `FbxSceneMapper.FindBindPoseArmature` and `FbxSkinningMapper.GetArmatureBindWorldMatrix` (the latter only returned identity); retained `SceneNodeFlags.Disabled`, `AnimationHelper`, `NameList` / `NameListService`, and the render pipeline/pass abstractions as public API surface. `ClearAndStateResetPass` is used by `SceneRenderer`.
- [x] Unused-value hacks: removed the unused glTF selection and MD5 bone-map parameters, the BVH/FBX discard statements, and the EXR discard; the reported `fuck` variable is absent.
- [x] `AssetExportContext` reuses `AssetManager`'s relative-path validation and combination methods.
- [x] OBJ, SEModel, XModel, glTF, and CAST reference paths share `FilePathResolver` for rooted, relative, and data URI references.
- [x] Portable texture references reuse `Texture.GetPortableFilePath` across glTF, SEModel, OBJ, and CAST; the shared method writes forward-slash paths.
- [x] `AssetManager.NormalizeVirtualPath` and `NormalizeRelativeOutputDirectory` share rooted-path, segment splitting, and dot-segment validation.
- [x] BC1/BC2/BC3 share their four-color RGB block encode/decode operations through `BlockColorOperations`; BC1's transparent three-color decode remains format-specific, and BC2/BC3 retain their own alpha encodings.
- [x] JPEG and PNG share RGBA8 extraction for direct RGBA/BGRA data and decoded pixel codecs in `Rgba8PixelConverter`.
- [x] Reviewed Euler conversion: FBX's closed-form seed and refinement and BVH's arbitrary channel-order search with continuity handling solve different input and stability requirements, so remain separate.
- [x] Reviewed GL/D3D11 light logic: the shader implementations are tied to their respective shader languages, while command lists apply the equivalent data through different GPU APIs; no shared state abstraction is warranted for the small duplicated transform logic.
- [x] `AssetManager` now shares the export event, skip, progress, and error pipeline for fresh reads and pre-read results; only obtaining the result differs.
- [x] SMD and BVH writers share bind-pose and animated relative-transform reconstruction in `SkeletonExportTransforms`.
- [x] Removed unused `Md5AnimWriter.ComputeAnimWorldTransform`, which had no callers beyond itself.
- [x] `SpanReader` and `MemoryReader` share null-terminator scanning in `ReaderOperations` while preserving their existing end-of-buffer behavior.
- [x] `ProcessReader` / `ProcessWriter` share the process-memory backend, validation, process discovery, and module lookup; the remaining read and write members stay separate because their operations differ.
- [x] PSA and PSK share ActorX bone record reading and writing in `ActorXBinary`; each writer still supplies its format-specific chunk ID.
- [x] `CastSkeletonTranslator.Read` uses `name` when it creates the skeleton node.
- [x] Reviewed remaining implementation helpers declared `public static` in Formats: glTF JSON parse/write entry points, `BvhReader` / `BvhWriter` operations, and `BvhRotation` are exposed format APIs; retained those public members. SMD reader/writer, Maya writer, and FBX tokenizer implementation helpers are private, with `ParseTriangles` internal to the test assembly.
- [x] `IwEngine/XAssetWriter.cs` — CallOfFile 2026.9.26.2 documents that `BinaryTokenWriter(Stream)` leaves the stream open; write directly to the destination and remove the staging buffer.
- [x] `MayaAsciiTranslator.cs:58`, `SceneTranslator.cs:219` — Maya writes from a per-call options copy; read-context creation retains the supplied options without mutation.
- [x] Library code writing to `Console.WriteLine` (`OpenGlSilkPresenterFactory.cs:72`, `AvaloniaOpenGlRendererControl.cs:221`) — removed both graphics library diagnostic writes.
- [x] UI project references unused AvaloniaEdit 11.4.1 and Silk.NET.OpenGLES — removed both references.

## 5. CLAUDE.md violations
- [ ] Split statements/arguments: ~500+ lines (worst: `FbxSceneMapper`, `D3D11GraphicsDevice`, `ObjReader`, `SmdWriter`, `SceneRenderer`, camera classes). Collapsed multiline signatures/calls in `ObjReader`, `ObjWriter`, `OpenGlGraphicsDevice`, `FbxSceneMapper`, `SceneTraversal`, GPU render handles/bindings, D3D11 resource/pipeline types, `GpuBufferData`, `D3D11UniformValue`, and ActorX records/readers/writers; collapsed `FbxSceneMapper` continuations, `ObjReader` ternaries, D3D11 null guards, camera projection/movement expressions, and SMD writer conditionals/call. Also collapsed remaining signatures in process memory, sample, UI, material descriptor, animation sampling, and asset-directory code; collapsed multiline glTF constructor/call and FBX/SMD exception arguments. Focused scans now find no multiline invocation argument lists in the named worst files. A Roslyn trivia-only pass then collapsed 166 safe argument/parameter lists across 78 files; lists with comments, directives, block lambdas, initializers, collection expressions, or parse diagnostics were skipped.
- [ ] Primary constructors not used: ~60 classes. Converted the asset event argument types (`SourceEventArgs`, `AssetOperationEventArgs`, `AssetReadCompletedEventArgs`, `AssetExportEventArgs`, `AssetExportCompletedEventArgs`, `AssetOperationFailedEventArgs`), `ProcessSelectionResult`, `FrameStatsReporter`, `MaterialTypePipelineCacheEntry`, `AssetDirectoryNode`, `TiffEncodedPixelData`, `TiffLzwDecoder`, BCn bit readers/writers/codecs, simple pixel codecs, single-constructor plugin/D3D11 exceptions where constructor work was property/field initialization or base forwarding, `SceneChangedEventArgs`, `CameraView`, `SceneBounds`, `FbxNode`, `FbxAsciiTokenizer`, `AvaloniaRenderFrameEventArgs`, D3D11 shader reflection layout/result types, `MaterialTextureBinding`, `Light`, `ProcessModuleInfo`, `BytePatternScanBufferSet`, `MeshSampleSceneContext`, and the imaging test `TestConverterEngine`.
- [ ] Member order: ~40 files (worst `SceneNode.cs`, `OpenGlGraphicsDevice.cs`, `GltfReader/Writer`). Moved the `MainWindowViewModel` constructor after its public properties and events, moved `OpenGlGraphicsDevice` constructors after its public properties and all private texture helpers after public methods, placed `GltfReader`'s private joint lookup after its public methods, moved all `GltfWriter` private helpers after its public methods, moved `SceneNode`'s filter, name-index, reparent, duplicate-resolution, and ancestor-lookup helpers after its public methods, moved all `FbxSceneMapper` private helpers after its public methods, moved `Scene.ResolveSkeletonBoneRoot`, `SkeletonAnimationSampler` curve helpers, `MeshTangentFrame` generation helpers, `SceneRenderer` traversal and anti-aliasing helpers, and D3D11 texture creation helpers after public methods, and moved `RenderPass.ExecutePass` after its public methods.
- [x] Renamed `SceneNode.DisposeCore`, `MeshTangentFrame.GenerateCore`, `AnimationHelper`, `WriteCore`, `EncodeBlockCore`, `ReleaseCore`, `ExecuteCore`, and `UpdateCore` to descriptive names.
- [x] Renamed `D3D11Helpers` and `D3D11Helpers.cs` to `D3D11Support` and `D3D11Support.cs`.
- [x] Split `MayaUnits.cs` into one file per public enum; extract `MeshMergeBucket`, the byte-reader delegates, `HexBytesViewportControl`, plugin persistence, asset-directory builder state, and sample types into separate files; remove the nested per-device resource holder.
- [x] Replaced API defaults in `DataBuffer<T>`, `Animation`, `AnimationTrack`, `GltfReader`/`Writer`, `MayaAsciiWriter`, `PythonResolver`, and the other listed APIs with overloads; kept caller-info defaults where the compiler must supply the caller value.
- [x] Local functions in `AnimationTrack`, `PskReader`, `VulkanBcContext`, and `TiffCompressor` are private class methods with explicit inputs.
- [x] Abbreviations: expanded the SMD/MD5 float formatter to `FormatFloat()`, TIFF compressor/decompressor names, Vulkan command buffers, parser and mesh indices, image buffers and pixel widths, source/destination spans, and translator extensions. The audited short identifiers (`idx`, `kf`, `ext`, `cmd`, `buf`, `src`/`dst`, `le`, `bpp`, `cts`) no longer occur as code identifiers.
- [x] Reviewed the reported block-scoped namespaces: `CLAUDE.md` does not require file-scoped namespaces, so this is not a guideline violation and no namespace edits are needed.
- [x] Removed duplicate `<summary>` elements in `Material`, `SmdReader`, and `SmdWriter`; moved `BvhTranslator` paragraphs inside its summary; corrected the `SceneNodeFlags.Selected` description.
- [x] Added XML documentation for the Oodle enum members and `ProgressDialogViewModel`; Zenith sources are not present in this repository.
- [x] Avalonia: `MainWindowViewModel` uses framework-neutral observable collections and `SynchronizationContext`, exposes preview content as `object` with a separate visibility property, and leaves bitmap loading to the view. The `SkeletonAnimationCurveViewer` is a compositional `UserControl` with bindable properties and does not need a separate view model. Replaced the hand-rolled Samples `RelayCommand` with CommunityToolkit.Mvvm and removed the cited explanatory UI copy and redundant `MainWindow` tooltip.
- [x] Migrated `Template.Cli`, `HashBuilder`, and Samples output to Spectre.Console; error output retains stderr routing and uses red styling. Removed the two stray `Console.WriteLine("Executing")` messages from template handlers.

## 6. Verbose docs and comments
Roughly 600 multi-line/multi-sentence summaries, 110 `<remarks>` blocks (about a dozen say "Initializes a new instance…"), 700 inline comments including ~60 section banners. Target: one short sentence per summary, no narrating remarks, no banners.

Most affected files: `SceneNode.cs`, `Transform.cs`, `Material.cs`, `AnimationCurve.cs`, `CCDIKSolver.cs`, `SmdWriter.cs`, `SmdReader.cs`, `MayaAsciiWriter.cs`, `GltfReader.cs`, `GltfWriter.cs`, `FbxSceneMapper.cs`, `Md5AnimWriter.cs`, `JpegEncoder.cs`, `JpegIdct.cs`, `JpegDecoder.cs`, `ExrLoader.cs`, `FastLZCodec.cs`, `TextTokenReader.cs`, `OpusEncoder.cs`, `OpusApplication.cs`, `StreamPointer{T}.cs`, `PluginsService.cs`, `PythonPluginHost.cs`, `OpenGlContext.cs`, `RenderFrameContext.cs`.

Representative replacements:

| Location | Replacement |
|---|---|
| `Scene.cs:9-14` | "Represents the root node of a 3D scene." |
| `SceneNode.cs:162-169` (MoveTo) | "Moves this node under a new parent, resolving duplicates in the subtree." |
| `SceneNode.cs:2006-2008` (Clone) | "Creates a detached deep copy of this node hierarchy." |
| `Transform.cs:8-22` | `<remarks>Unset values are derived from the parent; world = Local * ParentWorld.</remarks>` |
| `OrbitCamera.cs:169-171` | "Represents a camera orbiting a target point." |
| `BuiltInSceneFormats.cs:20-23` | Delete. |
| `GltfTranslator.cs:3-17` | "Reads and writes glTF 2.0 scenes." |
| `SmdTranslator.cs:5-33` | "Reads and writes Valve SMD files." |
| `MayaAsciiWriteOptions.cs:92-96` | "Gets or sets the up axis." |
| `Md5AnimReader.cs:8-19` | "Reads MD5 animation files." |
| `Png/PngImageTranslator.cs:14-18` | "Reads and writes PNG images." |
| `Tga/TgaRleDecoder.cs:6-11` | "Decodes TGA run-length encoded pixel data." |
| `FastLZCodec.cs:17-27` | "Managed FastLZ level 1 codec." |
| `TextTokenReader.cs:14-30` | "Reads whitespace-delimited tokens from a character span." |
| `OpusApplication.cs:18-33` | Terse per-member, e.g. "Low-latency mode." |
| `PluginsService.cs:6-17` | "Discovers plugin scripts and persists their auto-load state." |
| `OpenGlContext.cs:8-15` | "Wraps an active GL context." (delete remarks) |
| `RenderFrameContext.cs:8-10` | "Per-frame state shared by render passes." |
| Banners (`// ---- Build skeleton ----`, `// ── Connection list ──`, file-history headers in `SkeletonAnimationSampler.cs`/`SkeletonAnimationTrack.cs`) | Delete. |
