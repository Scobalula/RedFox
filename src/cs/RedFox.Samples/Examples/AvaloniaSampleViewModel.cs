using Avalonia.Platform.Storage;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Rendering.Hosting;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RedFox.Samples.Examples;

internal sealed class AvaloniaSampleViewModel : INotifyPropertyChanged
{
    private const string FallbackSceneName = "Avalonia Scene";
    private MeshSampleSceneContext? _context;
    private Scene? _scene;
    private OrbitCamera? _camera;
    private SceneViewportController? _viewportController;
    private SceneNode? _selectedNode;
    private SceneNodeItem? _selectedNodeItem;
    private bool _useViewBasedLighting;
    private SkinningMode _skinningMode = SkinningMode.Linear;
    private bool _isAnimationPaused;
    private bool _showGrid = true;
    private string _status = string.Empty;

    public AvaloniaSampleViewModel(string[] arguments)
    {
        NewSceneCommand = new RelayCommand(NewScene, null);
        AddTriangleCommand = new RelayCommand(AddTriangle, () => Scene is not null);
        RemoveSelectedNodeCommand = new RelayCommand(RemoveSelectedNode, () => SelectedNode is not null && SelectedNode.Parent is not null);
        ClearSceneCommand = new RelayCommand(ClearScene, () => Scene is not null);
        FitCameraCommand = new RelayCommand(FitCameraToScene, () => ViewportController is not null);
        ToggleGridCommand = new RelayCommand(ToggleGrid, () => Scene is not null);
        LoadFromArguments(arguments ?? []);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Scene? Scene
    {
        get => _scene;
        private set => SetProperty(ref _scene, value);
    }

    public OrbitCamera? Camera
    {
        get => _camera;
        private set => SetProperty(ref _camera, value);
    }

    public SceneViewportController? ViewportController
    {
        get => _viewportController;
        private set => SetProperty(ref _viewportController, value);
    }

    public SceneNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                RemoveSelectedNodeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public SceneNodeItem? SelectedNodeItem
    {
        get => _selectedNodeItem;
        set
        {
            if (SetProperty(ref _selectedNodeItem, value))
            {
                SelectedNode = value?.Node;
            }
        }
    }

    public ObservableCollection<SceneNodeItem> Nodes { get; } = [];

    public Vector4 ClearColor { get; } = new(0.07f, 0.09f, 0.13f, 1.0f);

    public bool UseViewBasedLighting
    {
        get => _useViewBasedLighting;
        set => SetProperty(ref _useViewBasedLighting, value);
    }

    public SkinningMode SkinningMode
    {
        get => _skinningMode;
        set => SetProperty(ref _skinningMode, value);
    }

    public bool IsAnimationPaused
    {
        get => _isAnimationPaused;
        set
        {
            if (SetProperty(ref _isAnimationPaused, value) && Scene is not null)
            {
                Scene.IsAnimationPaused = value;
            }
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (SetProperty(ref _showGrid, value))
            {
                ApplyGridVisibility();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public RelayCommand NewSceneCommand { get; }

    public RelayCommand AddTriangleCommand { get; }

    public RelayCommand RemoveSelectedNodeCommand { get; }

    public RelayCommand ClearSceneCommand { get; }

    public RelayCommand FitCameraCommand { get; }

    public RelayCommand ToggleGridCommand { get; }

    public async Task OpenFilesAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open RedFox Scene Files",
            AllowMultiple = true,
            FileTypeFilter = [CreateSceneFileType(), FilePickerFileTypes.All]
        });

        if (files.Count == 0)
        {
            return;
        }

        string[] paths = files.Select(file => file.Path.LocalPath).Where(File.Exists).ToArray();
        if (paths.Length == 0)
        {
            Status = "No local files were selected.";
            return;
        }

        MeshSampleOptions options = CreateOptionsForPaths(paths);
        if (!MeshSampleSceneFactory.TryCreate(options, Path.GetFileName(paths[0]), out MeshSampleSceneContext? context, out string? error))
        {
            Status = error ?? "Failed to open scene file.";
            return;
        }

        ArgumentNullException.ThrowIfNull(context);
        ApplyContext(context);
        Status = $"Opened {paths.Length} file(s).";
    }

    public void RefreshTree()
    {
        Nodes.Clear();
        if (Scene is not null)
        {
            Nodes.Add(new SceneNodeItem(Scene.RootNode));
        }
    }

    public void FitCameraToScene()
    {
        if (ViewportController is not null && ViewportController.RecomputeBounds() && ViewportController.FitCameraToScene())
        {
            Status = "Camera fit to scene.";
        }
    }

    public void ToggleViewLighting()
    {
        UseViewBasedLighting = !UseViewBasedLighting;
        Status = $"View lighting {(UseViewBasedLighting ? "enabled" : "disabled")}.";
    }

    public void ToggleSkeletonBones()
    {
        if (_context is null)
        {
            return;
        }

        bool showSkeletonBones = ToggleSkeletonBones(_context.Scene);
        _context.ViewportController.RecomputeBounds();
        Status = $"Skeleton bones {(showSkeletonBones ? "visible" : "hidden")}.";
    }

    public void ToggleSkinningMode()
    {
        SkinningMode = SkinningMode == SkinningMode.Linear
            ? SkinningMode.DualQuaternion
            : SkinningMode.Linear;
        Status = $"Skinning mode: {SkinningMode}.";
    }

    public void ToggleAnimationPause()
    {
        IsAnimationPaused = !IsAnimationPaused;
        Status = $"Animation {(IsAnimationPaused ? "paused" : "playing")}.";
    }

    public void ToggleBindPoseAnimation()
    {
        if (Scene is null)
        {
            return;
        }

        bool enable = Scene.IsAnimationPaused;
        IsAnimationPaused = !enable;
        if (!enable)
        {
            foreach (SceneNode node in Scene.EnumerateDescendants())
            {
                node.ResetLiveTransform();
            }

            Status = "Animations disabled; bind pose restored.";
        }
        else
        {
            Status = "Animations enabled.";
        }
    }

    private void LoadFromArguments(string[] arguments)
    {
        if (!MeshSampleSceneFactory.TryCreate(arguments, FallbackSceneName, out MeshSampleSceneContext? context, out string? error))
        {
            Status = error ?? "Failed to create scene.";
            MeshSampleSceneFactory.TryCreate([], FallbackSceneName, out context, out _);
        }

        if (context is not null)
        {
            ApplyContext(context);
        }
    }

    private void NewScene()
    {
        MeshSampleOptions options = CreateOptionsForPaths([]);
        if (!MeshSampleSceneFactory.TryCreate(options, FallbackSceneName, out MeshSampleSceneContext? context, out string? error))
        {
            Status = error ?? "Failed to create scene.";
            return;
        }

        ArgumentNullException.ThrowIfNull(context);
        ApplyContext(context);
        Status = "New scene.";
    }

    private void AddTriangle()
    {
        if (Scene is null)
        {
            return;
        }

        SceneNode parent = SelectedNode ?? Scene.RootNode;
        string name = GetUniqueChildName(parent, "Triangle");
        Mesh mesh = parent.AddNode(CreateTriangleMesh(name));
        RefreshAfterGraphMutation();
        Status = $"Added {mesh.Name}.";
    }

    private void RemoveSelectedNode()
    {
        if (SelectedNode is null || SelectedNode.Parent is null)
        {
            return;
        }

        SceneNode node = SelectedNode;
        string name = node.Name;
        if (node.Parent.RemoveNode(node))
        {
            node.Dispose();
            SelectedNode = null;
            SelectedNodeItem = null;
            RefreshAfterGraphMutation();
            Status = $"Removed {name}.";
        }
    }

    private void ClearScene()
    {
        if (Scene is null)
        {
            return;
        }

        Scene.ClearNodes();
        SelectedNode = null;
        SelectedNodeItem = null;
        RefreshAfterGraphMutation();
        Status = "Scene cleared.";
    }

    private void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
    }

    private static bool ToggleSkeletonBones(Scene scene)
    {
        SkeletonBone[] bones = scene.GetDescendants<SkeletonBone>();
        bool showSkeletonBones = bones.Any(bone => !bone.ShowSkeletonBone);
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].ShowSkeletonBone = showSkeletonBones;
        }

        return showSkeletonBones;
    }

    private void ApplyContext(MeshSampleSceneContext context)
    {
        _context = context;
        Scene = context.Scene;
        Camera = context.Camera;
        ViewportController = context.ViewportController;
        UseViewBasedLighting = context.Options.UseViewBasedLighting;
        SkinningMode = context.Options.SkinningMode;
        IsAnimationPaused = context.Scene.IsAnimationPaused;
        _showGrid = context.Grid?.Enabled == true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowGrid)));
        RefreshTree();
        RaiseCommandStateChanged();
        Status = context.AnimationPlayers.Count > 0
            ? $"Scene ready. {context.AnimationPlayers.Count} animation player(s)."
            : "Scene ready.";
    }

    private MeshSampleOptions CreateOptionsForPaths(IEnumerable<string> paths)
    {
        MeshSampleOptions options = new()
        {
            ShowGrid = ShowGrid,
            UseViewBasedLighting = UseViewBasedLighting,
            SkinningMode = SkinningMode,
            UpAxis = _context?.Options.UpAxis ?? SceneUpAxis.Y,
            FaceWinding = _context?.Options.FaceWinding ?? FaceWinding.CounterClockwise
        };

        foreach (string path in paths)
        {
            options.ScenePaths.Add(path);
        }

        return options;
    }

    private void ApplyGridVisibility()
    {
        if (_context?.Grid is null)
        {
            return;
        }

        _context.Grid.Enabled = ShowGrid;
        Status = $"Grid {(ShowGrid ? "visible" : "hidden")}.";
    }

    private void RefreshAfterGraphMutation()
    {
        ViewportController?.RecomputeBounds();
        RefreshTree();
        RaiseCommandStateChanged();
    }

    private static FilePickerFileType CreateSceneFileType()
    {
        return new FilePickerFileType("RedFox Scene Files")
        {
            Patterns = ["*.obj", "*.gltf", "*.glb", "*.semodel", "*.seanim", "*.smd", "*.ma", "*.fbx", "*.cast", "*.bvh", "*.md5mesh", "*.md5anim"]
        };
    }

    private static Mesh CreateTriangleMesh(string name)
    {
        Mesh mesh = new()
        {
            Name = name,
            Positions = CreatePositions(),
            Normals = CreateNormals(),
            FaceIndices = CreateIndices()
        };
        Material material = new($"{name}Material")
        {
            DiffuseColor = new Vector4(0.92f, 0.3f, 0.24f, 1.0f)
        };
        mesh.Materials = new List<Material> { material };
        return mesh;
    }

    private static string GetUniqueChildName(SceneNode parent, string baseName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        if (!parent.TryFindChild(baseName, out _))
        {
            return baseName;
        }

        int suffix = 2;
        while (parent.TryFindChild($"{baseName}{suffix}", out _))
        {
            suffix++;
        }

        return $"{baseName}{suffix}";
    }

    private static RedFox.Graphics3D.Buffers.DataBuffer<float> CreatePositions()
    {
        RedFox.Graphics3D.Buffers.DataBuffer<float> positions = new(3, 1, 3);
        positions.Add(new Vector3(-0.9f, -0.7f, 0.0f));
        positions.Add(new Vector3(0.9f, -0.7f, 0.0f));
        positions.Add(new Vector3(0.0f, 0.85f, 0.0f));
        return positions;
    }

    private static RedFox.Graphics3D.Buffers.DataBuffer<float> CreateNormals()
    {
        RedFox.Graphics3D.Buffers.DataBuffer<float> normals = new(3, 1, 3);
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        normals.Add(new Vector3(0.0f, 0.0f, 1.0f));
        return normals;
    }

    private static RedFox.Graphics3D.Buffers.DataBuffer<uint> CreateIndices()
    {
        RedFox.Graphics3D.Buffers.DataBuffer<uint> indices = new(3, 1, 1);
        indices.Add(0u);
        indices.Add(1u);
        indices.Add(2u);
        return indices;
    }

    private void RaiseCommandStateChanged()
    {
        AddTriangleCommand.RaiseCanExecuteChanged();
        RemoveSelectedNodeCommand.RaiseCanExecuteChanged();
        ClearSceneCommand.RaiseCanExecuteChanged();
        FitCameraCommand.RaiseCanExecuteChanged();
        ToggleGridCommand.RaiseCanExecuteChanged();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
