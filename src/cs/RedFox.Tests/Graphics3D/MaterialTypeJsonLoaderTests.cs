using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Tests.Graphics3D;

public sealed class MaterialTypeJsonLoaderTests
{
    [Fact]
    public void LoadStringReadsGraphicsDescriptorWithRequirements()
    {
        const string Json = """
        {
          "name": "LitTransitionEmissive",
          "pipelineKind": "graphics",
          "vertexShaderName": "Lit",
          "fragmentShaderName": "LitTransitionEmissive",
          "vertexAttributes": [
            { "name": "Positions", "componentCount": 3, "type": "float32", "offsetBytes": 0, "strideBytes": 32 },
            { "name": "TexCoords", "componentCount": 2, "type": "float32", "offsetBytes": 12, "strideBytes": 32 }
          ],
          "pipelineState": {
            "cullMode": "back",
            "faceWinding": "counterClockwise",
            "wireframe": false,
            "blend": true,
            "sourceBlendFactor": "sourceAlpha",
            "destinationBlendFactor": "inverseSourceAlpha",
            "blendOperation": "add",
            "depthTest": true,
            "depthWrite": false,
            "depthCompareFunc": "lessOrEqual",
            "primitiveTopology": "triangles"
          },
          "requirements": {
            "textures": [
              { "name": "Diffuse", "slot": 0, "required": true },
              { "name": "Emissive", "slot": 1, "required": false }
            ],
            "uniforms": [
              { "name": "EmissiveIntensity", "valueType": "float", "required": false }
            ]
          }
        }
        """;

        MaterialTypeDescriptor descriptor = MaterialTypeJsonLoader.LoadString(Json);

        Assert.Equal("LitTransitionEmissive", descriptor.Name);
        Assert.Equal(MaterialPipelineKind.Graphics, descriptor.PipelineKind);
        Assert.Equal("Lit", descriptor.VertexShaderName);
        Assert.Equal("LitTransitionEmissive", descriptor.FragmentShaderName);
        Assert.Equal(2, descriptor.VertexAttributes.Count);
        Assert.Equal("Positions", descriptor.VertexAttributes[0].Name);
        Assert.Equal(3, descriptor.VertexAttributes[0].ComponentCount);
        Assert.Equal(VertexAttributeType.Float32, descriptor.VertexAttributes[0].Type);
        Assert.Equal(32, descriptor.VertexAttributes[0].StrideBytes);
        Assert.Equal(BlendFactor.SourceAlpha, descriptor.PipelineState?.SourceBlendFactor);
        Assert.Equal(BlendFactor.InverseSourceAlpha, descriptor.PipelineState?.DestinationBlendFactor);
        Assert.Equal(CompareFunc.LessOrEqual, descriptor.PipelineState?.DepthCompareFunc);
        Assert.Equal(2, descriptor.Requirements.Textures.Count);
        Assert.Equal("EmissiveIntensity", Assert.Single(descriptor.Requirements.Uniforms).Name);
    }

    [Fact]
    public void BuiltInDefinitionsAreLoadedFromMaterialTypeDocument()
    {
        IReadOnlyList<MaterialTypeDefinition> definitions = BuiltInMaterialTypes.CreateDefinitions(new TestMaterialShaderFactory());

        MaterialTypeDefinition defaultDefinition = Assert.Single(definitions, static definition => definition.Name == "Default");
        Assert.NotNull(defaultDefinition.Descriptor);
        Assert.Contains(defaultDefinition.Descriptor.Requirements.Uniforms, static uniform => uniform.Name == "Model" && uniform.ValueType == MaterialValueType.Matrix4x4);
        Assert.Contains(defaultDefinition.Descriptor.Requirements.Textures, static texture => texture.Name == "Diffuse" && texture.Slot == 0);

        MaterialTypeDefinition gridDefinition = Assert.Single(definitions, static definition => definition.Name == "Grid");
        Assert.NotNull(gridDefinition.Descriptor);
        Assert.Empty(gridDefinition.Descriptor.VertexAttributes);
        Assert.Contains(gridDefinition.Descriptor.Requirements.Uniforms, static uniform => uniform.Name == "GridCellSize" && uniform.ValueType == MaterialValueType.Float);
    }

    private sealed class TestMaterialShaderFactory : IMaterialShaderFactory
    {
        public IGpuShader CreateShader(IGraphicsDevice graphicsDevice, string shaderName, ShaderStage stage)
        {
            throw new NotSupportedException();
        }
    }
}