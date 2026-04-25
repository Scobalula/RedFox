using System.Numerics;

namespace RedFox.Graphics3D.D3D11;

internal struct D3D11LightingConstants
{
    public Vector3 AmbientColor;
    public int LightCount;
    public Vector4 LightDirectionAndIntensity0;
    public Vector4 LightDirectionAndIntensity1;
    public Vector4 LightDirectionAndIntensity2;
    public Vector4 LightDirectionAndIntensity3;
    public Vector4 LightColor0;
    public Vector4 LightColor1;
    public Vector4 LightColor2;
    public Vector4 LightColor3;
    public Vector3 CameraPosition;
    public int UseViewBasedLighting;
    public Vector4 BaseColor;
    public Vector4 Specular;
}
