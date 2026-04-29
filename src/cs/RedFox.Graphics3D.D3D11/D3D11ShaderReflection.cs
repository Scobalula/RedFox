using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;

namespace RedFox.Graphics3D.D3D11;

internal static unsafe class D3D11ShaderReflection
{
    public static D3D11ShaderConstantBufferLayout[] Reflect(ReadOnlySpan<byte> bytecode, ShaderStage stage)
    {
        if (bytecode.IsEmpty)
        {
            return [];
        }

        D3DCompiler compiler = D3DCompiler.GetApi();
        fixed (byte* bytecodePointer = bytecode)
        {
            ComPtr<ID3D11ShaderReflection> reflection = compiler.Reflect<ID3D11ShaderReflection>(bytecodePointer, (nuint)bytecode.Length);
            try
            {
                return ReflectConstantBuffers(reflection.Handle, GetStageFlags(stage));
            }
            finally
            {
                reflection.Dispose();
            }
        }
    }

    private static D3D11ShaderConstantBufferLayout[] ReflectConstantBuffers(ID3D11ShaderReflection* reflection, D3D11ShaderStageFlags stage)
    {
        ShaderDesc shaderDesc = default;
        D3D11Helpers.ThrowIfFailed(reflection->GetDesc(ref shaderDesc), "ID3D11ShaderReflection::GetDesc");

        D3D11ShaderConstantBufferLayout[] layouts = new D3D11ShaderConstantBufferLayout[shaderDesc.ConstantBuffers];
        for (uint bufferIndex = 0; bufferIndex < shaderDesc.ConstantBuffers; bufferIndex++)
        {
            ID3D11ShaderReflectionConstantBuffer* constantBuffer = reflection->GetConstantBufferByIndex(bufferIndex);
            ShaderBufferDesc bufferDesc = default;
            D3D11Helpers.ThrowIfFailed(constantBuffer->GetDesc(ref bufferDesc), "ID3D11ShaderReflectionConstantBuffer::GetDesc");

            string bufferName = ReadString(bufferDesc.Name);
            int slot = ResolveConstantBufferSlot(reflection, in shaderDesc, bufferName);
            List<D3D11ShaderVariableLayout> variables = new(checked((int)bufferDesc.Variables));
            for (uint variableIndex = 0; variableIndex < bufferDesc.Variables; variableIndex++)
            {
                ID3D11ShaderReflectionVariable* shaderVariable = constantBuffer->GetVariableByIndex(variableIndex);
                variables.Add(ReflectVariable(shaderVariable));
            }

            layouts[bufferIndex] = new D3D11ShaderConstantBufferLayout(
                bufferName,
                slot,
                stage,
                checked((int)bufferDesc.Size),
                variables);
        }

        return layouts;
    }

    private static D3D11ShaderVariableLayout ReflectVariable(ID3D11ShaderReflectionVariable* shaderVariable)
    {
        ShaderVariableDesc variableDesc = default;
        D3D11Helpers.ThrowIfFailed(shaderVariable->GetDesc(ref variableDesc), "ID3D11ShaderReflectionVariable::GetDesc");

        ID3D11ShaderReflectionType* shaderType = shaderVariable->GetType();
        ShaderTypeDesc typeDesc = default;
        D3D11Helpers.ThrowIfFailed(shaderType->GetDesc(ref typeDesc), "ID3D11ShaderReflectionType::GetDesc");

        bool isMatrix = IsMatrix(typeDesc.Class);
        int componentCount = isMatrix
            ? checked((int)(typeDesc.Rows * typeDesc.Columns))
            : checked((int)Math.Max(typeDesc.Columns, 1));
        int arrayLength = checked((int)Math.Max(typeDesc.Elements, 1));
        int sizeBytes = checked((int)variableDesc.Size);

        D3D11ShaderVariableKind kind = isMatrix
            ? D3D11ShaderVariableKind.Matrix4x4
            : IsInt(typeDesc.Type)
                ? D3D11ShaderVariableKind.Int
                : D3D11ShaderVariableKind.Float;

        return new D3D11ShaderVariableLayout(
            ReadString(variableDesc.Name),
            kind,
            checked((int)variableDesc.StartOffset),
            componentCount,
            sizeBytes,
            typeDesc.Elements > 0,
            arrayLength,
            typeDesc.Elements > 0 ? sizeBytes / arrayLength : 0);
    }

    private static int ResolveConstantBufferSlot(ID3D11ShaderReflection* reflection, in ShaderDesc shaderDesc, string bufferName)
    {
        for (uint resourceIndex = 0; resourceIndex < shaderDesc.BoundResources; resourceIndex++)
        {
            ShaderInputBindDesc bindDesc = default;
            D3D11Helpers.ThrowIfFailed(reflection->GetResourceBindingDesc(resourceIndex, ref bindDesc), "ID3D11ShaderReflection::GetResourceBindingDesc");
            if (ReadString(bindDesc.Name).Equals(bufferName, StringComparison.Ordinal))
            {
                return checked((int)bindDesc.BindPoint);
            }
        }

        throw new InvalidOperationException($"D3D11 shader constant buffer '{bufferName}' does not have a reflected binding slot.");
    }

    private static D3D11ShaderStageFlags GetStageFlags(ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex => D3D11ShaderStageFlags.Vertex,
            ShaderStage.Fragment => D3D11ShaderStageFlags.Fragment,
            ShaderStage.Compute => D3D11ShaderStageFlags.Compute,
            _ => D3D11ShaderStageFlags.None,
        };
    }

    private static bool IsMatrix(D3DShaderVariableClass variableClass)
    {
        return variableClass is D3DShaderVariableClass.D3DSvcMatrixRows
            or D3DShaderVariableClass.D3D10SvcMatrixRows
            or D3DShaderVariableClass.D3DSvcMatrixColumns
            or D3DShaderVariableClass.D3D10SvcMatrixColumns;
    }

    private static bool IsInt(D3DShaderVariableType variableType)
    {
        return variableType is D3DShaderVariableType.D3DSvtInt
            or D3DShaderVariableType.D3D10SvtInt;
    }

    private static string ReadString(byte* value)
    {
        return value is null ? string.Empty : Marshal.PtrToStringAnsi((nint)value) ?? string.Empty;
    }
}