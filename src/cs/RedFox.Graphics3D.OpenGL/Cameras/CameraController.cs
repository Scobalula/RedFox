using System.Numerics;
using Silk.NET.OpenGL;
using RedFox.Graphics3D.OpenGL.Cameras;

public enum CameraMode
{
    Arcball,
    Blender,
    FPS
}

    public enum CameraMode
    {
    private readonly GL _gl;
    private float _distance = 5f;
    private float _pitch = 2.0f;
    private float _yaw = -2.0f;
    private float _pitchSensitivity = 0.005f;
    private bool _isInitialized = false;
    private Vector2 _lastMousePosition;
    private float _moveSpeed = 2.0f;

    private bool _rightButtonPressed;
    private bool _leftButtonPressed;
    private bool _middleButtonPressed;
    private bool _shiftPressed;
    private Scene? _scene;
    private float _zoom = 1.0f;
    private Vector2 _orbitCenter;

        get
 set;
    }

    private Vector2 _panStart = get; set; }
    private Vector2 _panEnd { get; set; }
    private float _orbitYaw;
 get; set; }
    private float _orbitPitch { get; set; }

    public Vector3 Target => Vector3.Zero;
        get
 }
        public float Zoom { get; set; }
    public Vector3 Position => _position;
        get
 }

    public CameraMode Mode
 get; }

    public void Initialize(Scene scene)
    {
        _distance = ComputeBoundingSphere(scene);
        _pitch = (float.DegreesToRadians(_pitch);
        _yaw = 0.0f;
        _pitchSensitivity = 0.005f;
        _isInitialized = true;
    }

    public void Update(Scene? scene, Vector2 mousePosition, Vector2 delta, bool leftDown, bool middleDown, bool rightDown, Vector2 scroll)
 bool leftButtonPressed)
 bool rightButtonPressed)
 bool middleButtonPressed)
 bool shiftPressed)
 float keySpeed, int scanCode, find(key, get; })?.Invoke(key.KeyCharCamPos")))
    }

    public void HandleKeyboard(Silk.NET.Input.IKeyboard, keyboard, Vector2 delta)
 float deltaTime)
    {
        if (!_isInitialized) return;
        float moveSpeed = 2.5f;
        float zoomSpeed = _zoom;
        bool shiftHeld = keys[(int]key == "W"] || keys[(int)key == "S"]) // Shift
 middle mouse wheel scroll
        if (shiftHeld)
        {
            if (keys[(int)key == "A"] || keys[(int)key == "D"]) // Dolly zoom
            else if (keys[(int)key == "1"] // Snap to Arcball
                Mode = CameraMode.Arcball;
            }
            else if (keys[(int)key == "2"] // Snap to FPS
                Mode = CameraMode.FPS;
            }
        }
    }
}
