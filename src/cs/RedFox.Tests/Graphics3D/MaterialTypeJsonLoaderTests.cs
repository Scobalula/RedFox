using RedFox.Graphics3D;
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

    [Fact]
    public void LoadStringReadsTextureSemanticsAndUniformDefaults()
    {
        const string Json = """
        {
          "name": "SemanticDefaults",
          "pipelineKind": "graphics",
          "vertexShaderName": "Default",
          "fragmentShaderName": "Pbr",
          "pipelineState": { "depthTest": true, "depthWrite": true, "primitiveTopology": "triangles" },
          "requirements": {
            "textures": [
              { "name": "NormalMap", "slot": 1, "required": false, "semantic": "normal" },
              { "name": "OpacityMap", "slot": 0, "required": false }
            ],
            "uniforms": [
              { "name": "TextureTiling", "valueType": "float2", "required": false, "defaultValue": [2, 3] },
              { "name": "Model", "valueType": "matrix4x4", "required": true }
            ]
          }
        }
        """;

        MaterialTypeDescriptor descriptor = MaterialTypeJsonLoader.LoadString(Json);

        Assert.Equal("normal", descriptor.Requirements.Textures[0].Semantic);
        Assert.Null(descriptor.Requirements.Textures[1].Semantic);
        Assert.Equal([2.0f, 3.0f], descriptor.Requirements.Uniforms[0].DefaultValue);
        Assert.Null(descriptor.Requirements.Uniforms[1].DefaultValue);
    }

    [Theory]
    [InlineData(PbrMaterial.TypeName)]
    [InlineData(PbrMaterial.TransparentTypeName)]
    [InlineData(PbrMaterial.HairTypeName)]
    [InlineData(PbrMaterial.HairTransparentTypeName)]
    [InlineData(PbrMaterial.EyeTypeName)]
    [InlineData(PbrMaterial.UnlitTypeName)]
    [InlineData(PbrMaterial.UnlitTransparentTypeName)]
    [InlineData(PbrMaterial.AdditiveTypeName)]
    public void BuiltInPbrDefinitionsFitTheFragmentSamplerBudget(string typeName)
    {
        IReadOnlyList<MaterialTypeDefinition> definitions = BuiltInMaterialTypes.CreateDefinitions(new TestMaterialShaderFactory());

        MaterialTypeDescriptor descriptor = Assert.Single(definitions, definition => definition.Name == typeName).Descriptor;
        MaterialTextureRequirement diffuse = Assert.Single(descriptor.Requirements.Textures, static texture => texture.Name == PbrMaterial.DiffuseMap);
        MaterialUniformRequirement tiling = Assert.Single(descriptor.Requirements.Uniforms, static uniform => uniform.Name == PbrMaterial.TextureTiling);
        Assert.Equal(15, diffuse.Slot);
        Assert.Equal("diffuse", diffuse.Semantic);
        Assert.Equal([1.0f, 1.0f], tiling.DefaultValue);
        Assert.InRange(descriptor.Requirements.Textures.Count, 1, 12);
    }

    private sealed class TestMaterialShaderFactory : IMaterialShaderFactory
    {
        public IGpuShader CreateShader(IGraphicsDevice graphicsDevice, string shaderName, ShaderStage stage)
        {
            throw new NotSupportedException();
        }
    }
}