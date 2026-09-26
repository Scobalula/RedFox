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
- [ ] `Jpeg/JpegIdct.cs:71,143` — zero-AC shortcuts use `<<12`/`>>18` instead of `<<2`/`>>5`; wrong on every non-AVX2 CPU.
- [ ] `Tiff/TiffIfdReader.cs:81,83` — inline SHORT/BYTE masked instead of `ReadUInt16Inline`; big-endian TIFFs read as 0.
- [ ] `Exr/ExrCompression.cs:95,113` (+165-189) — RLE sign convention inverted vs OpenEXR.
- [ ] `Exr/ExrWriter.cs:218-220` — PXR24/B44 get scanline-interleaved input instead of channel-major (and `SmallestSize` picks PXR24).
- [ ] `Jpeg/JpegDecoder.cs:492-496,373-393` — single-component scans walk the MCU grid, corrupting subsampled images.
- [ ] `Tiff/TiffImageTranslator.cs:71,127` — RowsPerStrip `0xFFFFFFFF` cast to -1.
- [ ] `Bmp/BmpImageTranslator.cs` — indexed alpha from palette reserved byte (370-373); V4/V5 masks read from stream (62-67); `aMask = ~(r|g|b)` for 40-byte header (73); negative shift in `ScaleChannel` (382).
- [ ] `Dds/DdsLegacyFormatMapper.cs:98` — 24-bpp mapped to 32-bpp format; `Dds/DdsPitchCalculator.cs:23-25` BC `LinearSize` is one row pitch.
- [ ] `Jpeg/JpegDecoder.cs:954-960` — CMYK treated as YCbCr, K dropped; 2-component crashes; 12-bit not rejected.
- [ ] `Jpeg/JpegEncoder.cs:566`, `Tga/TgaHeader.cs:88` — silent ushort truncation; `Tga/TgaImageTranslator.cs:45`, `Jpeg/JpegImageTranslator.cs:74` seek non-seekable streams.
- [ ] `Exr/ExrHuffmanBitReader.cs:80-93` — 58-bit peek can overflow the 64-bit buffer.
- [ ] `RedFox.Imaging/BlockColorOperations.cs:78,147` — BC4/BC5 SNORM endpoints compared unsigned.
- [ ] `R11G11B10FloatCodec.cs:89,107,117-162` — denormals 16× small; no max-finite clamp; float10 Inf wrong.
- [ ] `BC6HCodec.cs:737-756` — swapped delta clamped; endpoint/index mismatch.
- [ ] `ImageTranslatorManager.cs:86-99` — selection ignores CanRead/CanWrite.
- [ ] `RedFox.Imaging.Vulkan/VulkanBcContext.cs:659-682` — single dispatch with `StartBlockId = 0`; exceeds 65535 groups at 1024² for BC7 modes 1/3/7. Batch.
- [ ] `VulkanBcContext.cs:92` — `Initialize()` false leaks instance/device; `:356,375` failed pipeline creation leaks layouts.

### Audio / IO / Patterns
- [ ] `ImaAdpcmCodec.cs:126` — second block offset is blockAlign²; only one block encoded.
- [ ] `ImaAdpcmCodec.cs:182-186` — stereo layout wrong (WAV IMA interleaves 4-byte chunks); >2 channels decoded as stereo; mono tail uninitialised.
- [ ] `MsAdpcmCodec.cs:46-48` — stereo samples-per-block doubled; `:151,158,196-200` nibble order low-first; `:172-182` stereo header layout wrong.
- [ ] `MurMur3Hash.cs:66-70` — ignores `ibStart`/`cbSize`, mixes tail per chunk; streaming hashes wrong.
- [ ] `ZStandardCodec.cs:61` — UNKNOWN/ERROR content size returned as -1/-2; `DeflateCodec.cs:30,66` `&span[0]` throws on empty input.
- [ ] `BytePattern.cs:27-48` — single `?` or bad hex pair misaligns the pattern.
- [ ] `VirtualDirectory.cs:275` — recursion into `this` instead of `directory`; `:335` / `VirtualFile.cs:98` `GetHashCode` uses culture `ToLower` vs ordinal `Equals`.
- [ ] `SpanReader.cs:475-497` / `MemoryReader.cs:514-537` — Seek assigns before validating; `End` uses `Length - offset`. `SpanReader.cs:65` vs `MemoryReader.cs:76` argument order differs.
- [ ] `BinaryReaderExtensions.cs:30,69,105` — legal short reads throw.
- [ ] `ProcessReader.cs:185`, `SpanReader.cs:96` — `count*SizeOf` overflow.

### Rendering
- [ ] `SceneRenderResources.cs:10` — static `ConditionalWeakTable` keyed by scene only; handles reused across devices/contexts, never disposed. Make per-device.
- [ ] `AvaloniaOpenGlRendererControl.cs:277-280,315-357` — renders `ViewportController.Scene ?? Scene` but only releases/subscribes `Scene`.
- [ ] `Handles/MaterialRenderHandle.cs:219-233` — `HasDiffuseMap=1` without a GPU texture; samples previous material's texture.
- [ ] `Handles/MeshRenderHandle.cs:388-453`, `MeshGpuBufferBinding.cs:149` — replaced buffers never re-uploaded.
- [ ] `AvaloniaCameraInputAdapter.cs:34-39` — handlers registered Tunnel|Bubble; events processed twice (explains 0.25 zoom sensitivity).
- [ ] `SkeletonAnimationCurveViewer.cs:671-672` — local writes replace brush bindings.
- [ ] `SceneRenderer.cs:440` — disposes an injected device it doesn't own.
- [ ] `Handles/MeshRenderHandle.cs:483-521` — whole mesh drawn once per material, no index ranges.

### GameExtraction / Plugins / Zenith
- [ ] `RedFox.GameExtraction.Template/ModelHandler.cs:89`, `AnimationHandler.cs:91` — store `Scene` but export `GetData<byte[]>()`; every export throws. Same in `Template.Cli/Program.cs:106`.
- [ ] `NameTableManager.cs:189` — inverted `!string.Equals`.
- [ ] `AssetExportContext.cs:39,207,349` — null `ExportOptions` throws; `:116-118` `PreserveDirectoryStructure=false` does nothing.
- [ ] `AssetHandlers/ModelHandler.cs:47` — `Split('.')[0]` collapses dotted names.
- [ ] `RedFox.Zenith/DataStorages/LocalDataStorage.cs:57` vs `:33` — keys written Base64, read raw. *(N/A: project not in repo.)*
- [ ] `RedFox.GameExtraction.HashBuilder/Program.cs:10-11` vs `34-39` — usage text and parser disagree.
- [ ] `RedFox.Plugins/PluginManager.cs:123-129` — failed initialize never runs `Unloading`.
- [ ] `RedFox.Plugins.Python/PythonResolver.cs:207-208` — `ReadToEnd` before `WaitForExit` (timeout ineffective), stderr unread (deadlock); `:172` misses Python 3.14.
- [ ] `PythonPluginHost.cs:118-120` — evicts stdlib modules matching plugin names.
- [ ] `MainWindowViewModel.cs:883-970` — process scan runs on the UI thread; `:967,1059,1145` finally disposes shared `_currentCts`.
- [ ] `ZipAssetSourceReader.cs:92` + `AssetManager.cs:434-438` — entries added before validation; stale on cancel. `AssetManager`/`ZipArchive` not thread-safe.
- [ ] `Template.Avalonia/Program.cs:93-112` — group named "Fuck"; duplicate `ExportReferences` / `PreserveDirectoryStructure` entries.
- [ ] `MainWindowViewModel.cs:1231` — `ReferenceCountDisplay` always "0".

## 3. Performance
- [ ] `SceneNode.cs:2034-2038` — `Clone` O(n²) via per-pair `Swap`.
- [ ] `SceneNode.cs:1749` — closure per node per frame in `Update`.
- [ ] `SceneNode.cs:3077,3351` — world pose/matrix recomputed recursively per call (per bone per frame in skinning, solvers, bounds).
- [ ] `SceneNode.cs:557` — nested recursive `yield`; `:1624` `AddNode` linear scan.
- [ ] `SceneNode.GetBestParent` builds a HashSet per call; Smd/Bvh/Fbx writers call it per bone per frame (O(frames·bones²)). Precompute parent arrays.
- [ ] Smd/Bvh/Md5 writers recompute animated world transforms per bone, frame and ancestor. One top-down pass per frame.
- [ ] `Mesh.cs:199` matrix inverse per influence per vertex; `MeshOptimizer.cs:203` per-face allocation; `PackedBuffer.Get` unpacks the full vector per component.
- [ ] `IwEngine/XModelReader.cs:117-184` — no vertex welding, O(n²) group count; `SmdReader` 3 vertices per triangle.
- [ ] Per-vertex `List` allocations: `XModelWriter.cs:221`, `MayaAsciiWriter.cs:584`, `FbxSkinningMapper.cs:86`; `BvhReader.cs:241` string per value; `GltfReader.cs:245,528,696-726` O(n²) lookups.
- [ ] Exr: PIZ ~640 KB per chunk, B44 arrays + LINQ per 4×4 block, `ToUpperInvariant` per row, string switch per pixel.
- [ ] JPEG Huffman bit-at-a-time with `Stream.ReadByte`; fake SIMD colour conversion.
- [ ] `Dds/DdsLoader.cs:34-38,65` — payload copied 3×.
- [ ] `FlacCodec.cs:237` — `BitConverter.GetBytes` per sample into `List<byte>`; `LzoCodec.cs:41` 128 KB table per call; `R11G11B10FloatCodec` `Math.Pow` per channel.
- [ ] Rendering: per-frame `glGet`s and unconditional `RequestNextFrameRendering`; per-frame `Dictionary`/context allocation; 4 CWT lookups per node per frame; D3D11 constant buffers rebuilt per draw; skin/morph uploaded twice; one draw per bone.
- [ ] GameExtraction: `RemoveAt` loop in `AssetManager.cs:1011-1021`; no filter debounce; `AssetRowViewModel.Name`/`Size` recomputed per sort; progress per ZIP entry; `x:CompileBindings="False"` on DataGrids.

## 4. Cleanup / dead code / duplication
- [ ] `SceneNode.cs` — 256 public members, 74 `Action`/predicate wrappers (~2000 lines). Keep `IEnumerable` forms.
- [ ] `SceneMerger.cs` duplicates `AttachInto`, `FindDuplicateInScope`, `MakeUniqueName`, `RedirectReferences`.
- [ ] Name comparison: `CurrentCultureIgnoreCase` vs case-sensitive `CurrentCulture` across `SceneNode`. Standardise on `OrdinalIgnoreCase`.
- [ ] Dead: `SceneRenderType`, `SceneNodeFlags.Disabled`, `Scene.RootNode`, `AnimationHelper`, `IValueManipulator` + interpolators, `FaceNormalDot`, `FbxArrayFactory`, `AttachNullNodeAttributes`, `FbxSceneMapper.FindBindPoseArmature`, `FbxSkinningMapper.GetArmatureBindWorldMatrix`, `OodleInterop`, `NameList`, `NameListService`, `HardwareLicenseVerifier`, render pipeline/pass layer, compute skinning path (shaders + `GlComputeProgram`), `_bc7TryMode02Pipeline`, `JpegIdct.TransformSse2`, `JpegEncoderOptions.OptimizeHuffmanTables`.
- [ ] Unused-value hacks: `_ = selection` (`GltfWriter.cs:251`), `_ = Options` (Bvh reader/writer), `_ = stream` (`FbxTranslator.cs:54`), `_ = boneIndexMap`, `_ = attributeType` (`ExrLoader.cs:150`); variable `fuck` in `GDeflateCodec.cs:52`.
- [ ] Duplicated: Bvh/Fbx hill-climb Euler solvers (use closed form); Smd/Bvh/Md5 relative-transform code; Psa/Psk bone IO; `ResolveTexturePath`; `SpanReader`/`MemoryReader`; `ProcessReader`/`ProcessWriter`; BC codec pixel code; JPEG/PNG pixel encoders; GL/D3D11 light logic; two GameExtraction export pipelines; three path normalisers.
- [ ] `CastSkeletonTranslator.Read` — once unused `name` parameter; confirm behaviour now that it creates a `Skeleton`.
- [ ] ~90 internal steps declared `public static` in Formats (e.g. `SmdReader.TryParseInt`, `GltfJsonParser.Parse*`).
- [ ] `IwEngine/XAssetWriter.cs` — delete the buffer once CallOfFile's leave-open stream behaviour is published.
- [ ] `MayaAsciiTranslator.cs:58`, `SceneTranslator.cs:219` — mutate shared options.
- [ ] Library code writing to `Console.WriteLine` (`OpenGlSilkPresenterFactory.cs:72`, `AvaloniaOpenGlRendererControl.cs:221`).
- [ ] UI project references unused AvaloniaEdit 11.4.1 and Silk.NET.OpenGLES.

## 5. CLAUDE.md violations
- [ ] Split statements/arguments: ~500+ lines (worst: `FbxSceneMapper`, `D3D11GraphicsDevice`, `ObjReader`, `SmdWriter`, `SceneRenderer`, camera classes).
- [ ] Primary constructors not used: ~60 classes.
- [ ] Member order: ~40 files (worst `SceneNode.cs`, `MainWindowViewModel.cs`, `OpenGlGraphicsDevice.cs`, `GltfReader/Writer`).
- [ ] "Core"/"Helper" names: `SceneNode.DisposeCore`, `MeshTangentFrame.GenerateCore`, `AnimationHelper`, `WriteCore` (Bmp/Png/Jpeg/Exr), `EncodeBlockCore` (BC6H/BC7), `D3D11Helpers`, `ReleaseCore`/`ExecuteCore`/`UpdateCore`.
- [ ] Nested types: `ObjReader.MeshMergeBucket`, `SceneRenderResources.ResourceSlot`, `BytePatternScanner.ByteChunkReader`, `NullTerminatedStringReader.ReadChunk`/`.ExceptionFactory`, `PluginsService.PluginsState`, `AssetDirectoryTreeBuilder.MutableNode`, `HexBytesPreviewControl.ViewportControl`, sample types. `MayaUnits.cs` holds 4 enums.
- [ ] Default parameters: ~17 (`DataBuffer{T}.cs:109`, `Animation.cs:115`, `AnimationTrack.cs:82-115`, `GltfReader.cs:43`, `GltfWriter.cs:44`, `MayaAsciiWriter.cs:309`, `PythonResolver.cs:33`, …).
- [ ] Local functions: `AnimationTrack.cs:170`, `PskReader.cs:225`, `VulkanBcContext.cs:644`, `TiffCompressor.cs:100,107`.
- [ ] Abbreviations: widespread (`idx`, `kf`, `ext`, `cmd`, `buf`, `src`/`dst`, `le`, `bpp`, `cts`, `F()`).
- [ ] Block-scoped namespaces: ~20 files in Graphics3D, GDeflate, LZ4, Oodle.
- [ ] Invalid XML docs: duplicate `<summary>` (`Material.cs:17-21`, `SmdReader.cs:556`, `SmdWriter.cs:509`); `<para>` outside summary (`BvhTranslator.cs:8-21`); "Gets or sets" on get-only/enum members; missing docs across Zenith, Oodle enums, `ProgressDialogViewModel`.
- [ ] Avalonia: `SkeletonAnimationCurveViewer` code-behind UI with no view model; `MainWindowViewModel` holds Avalonia types; Samples hand-roll `RelayCommand`; UI chatter in `MainWindowViewModel.cs:297,1213,1236`, `PluginsWindow.axaml:105`, `AboutWindow.axaml:67`, `PreviewWindow.axaml:96`, tooltip `MainWindow.axaml:177`.
- [ ] CLIs use raw `Console` instead of Spectre.Console: `Template.Cli`, `HashBuilder`, Samples.

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
