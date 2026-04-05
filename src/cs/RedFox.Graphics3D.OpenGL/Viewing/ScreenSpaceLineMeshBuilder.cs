using System.Numerics;

namespace RedFox.Graphics3D.OpenGL.Viewing;

internal static class ScreenSpaceLineMeshBuilder
{
    /// <summary>
    /// Attempts to append a screen-space line quad to the vertex data buffer.
    /// </summary>
    /// <param name="vertexData">The vertex data list to append quad vertices to.</param>
    /// <param name="startScene">The line start position in scene space.</param>
    /// <param name="endScene">The line end position in scene space.</param>
    /// <param name="viewMatrix">The view matrix for transforming scene positions to view space.</param>
    /// <param name="projectionMatrix">The projection matrix for transforming view positions to clip space.</param>
    /// <param name="nearPlane">The near clipping plane distance.</param>
    /// <param name="farPlane">The far clipping plane distance.</param>
    /// <param name="viewportWidth">The viewport width in pixels.</param>
    /// <param name="viewportHeight">The viewport height in pixels.</param>
    /// <param name="thicknessPixels">The line thickness in screen pixels.</param>
    /// <param name="color">The RGBA color of the line.</param>
    /// <returns><c>true</c> if the quad was appended; <c>false</c> if the line was fully clipped or degenerate.</returns>
    public static bool TryAppendQuad(
        List<float> vertexData,
        Vector3 startScene,
        Vector3 endScene,
        Matrix4x4 viewMatrix,
        Matrix4x4 projectionMatrix,
        float nearPlane,
        float farPlane,
        int viewportWidth,
        int viewportHeight,
        float thicknessPixels,
        Vector4 color)
    {
        Vector3 startView = Vector3.Transform(startScene, viewMatrix);
        Vector3 endView = Vector3.Transform(endScene, viewMatrix);

        if (!ViewSpaceLineClipper.TryClipToPerspectiveDepth(ref startView, ref endView, nearPlane, farPlane))
            return false;

        Vector4 startClip = Vector4.Transform(new Vector4(startView, 1.0f), projectionMatrix);
        Vector4 endClip = Vector4.Transform(new Vector4(endView, 1.0f), projectionMatrix);

        if (MathF.Abs(startClip.W) <= 1e-6f || MathF.Abs(endClip.W) <= 1e-6f)
            return false;

        Vector2 startNdc = new(startClip.X / startClip.W, startClip.Y / startClip.W);
        Vector2 endNdc = new(endClip.X / endClip.W, endClip.Y / endClip.W);
        Vector2 delta = endNdc - startNdc;

        float halfThickness = MathF.Max(thicknessPixels, 0.5f) * 0.5f;
        Vector2 ndcOffset;
        float lineLengthPixels = ComputeScreenLengthPixels(delta, viewportWidth, viewportHeight);
        if (lineLengthPixels <= 0.5f)
        {
            ndcOffset = new(
                (halfThickness * 2.0f) / Math.Max(viewportWidth, 1),
                0.0f);

            Vector2 verticalOffset = new(
                0.0f,
                (halfThickness * 2.0f) / Math.Max(viewportHeight, 1));

            Vector2 centerNdc = (startNdc + endNdc) * 0.5f;
            return TryAppendQuadFromNdcOffsets(
                vertexData,
                centerNdc - ndcOffset + verticalOffset,
                centerNdc - ndcOffset - verticalOffset,
                centerNdc + ndcOffset + verticalOffset,
                centerNdc + ndcOffset - verticalOffset,
                startClip,
                endClip,
                color);
        }

        Vector2 perpendicular = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        ndcOffset = new(
            perpendicular.X * ((halfThickness * 2.0f) / Math.Max(viewportWidth, 1)),
            perpendicular.Y * ((halfThickness * 2.0f) / Math.Max(viewportHeight, 1)));

        return TryAppendQuadFromNdcOffsets(
            vertexData,
            startNdc + ndcOffset,
            startNdc - ndcOffset,
            endNdc + ndcOffset,
            endNdc - ndcOffset,
            startClip,
            endClip,
            color);
    }

    private static bool TryAppendQuadFromNdcOffsets(
        List<float> vertexData,
        Vector2 startA,
        Vector2 startB,
        Vector2 endA,
        Vector2 endB,
        Vector4 startClip,
        Vector4 endClip,
        Vector4 color)
    {
        AppendVertex(vertexData, ToClipPosition(startA, startClip), color);
        AppendVertex(vertexData, ToClipPosition(startB, startClip), color);
        AppendVertex(vertexData, ToClipPosition(endA, endClip), color);
        AppendVertex(vertexData, ToClipPosition(endA, endClip), color);
        AppendVertex(vertexData, ToClipPosition(startB, startClip), color);
        AppendVertex(vertexData, ToClipPosition(endB, endClip), color);
        return true;
    }

    private static Vector4 ToClipPosition(Vector2 ndcPosition, Vector4 clipPosition)
    {
        return new Vector4(
            ndcPosition.X * clipPosition.W,
            ndcPosition.Y * clipPosition.W,
            clipPosition.Z,
            clipPosition.W);
    }

    private static void AppendVertex(List<float> vertexData, Vector4 clipPosition, Vector4 color)
    {
        vertexData.Add(clipPosition.X);
        vertexData.Add(clipPosition.Y);
        vertexData.Add(clipPosition.Z);
        vertexData.Add(clipPosition.W);
        vertexData.Add(color.X);
        vertexData.Add(color.Y);
        vertexData.Add(color.Z);
        vertexData.Add(color.W);
    }

    /// <summary>
    /// Attempts to append a screen-space point quad to the vertex data buffer.
    /// </summary>
    /// <param name="vertexData">The vertex data list to append quad vertices to.</param>
    /// <param name="scenePosition">The point position in scene space.</param>
    /// <param name="viewMatrix">The view matrix for transforming scene positions to view space.</param>
    /// <param name="projectionMatrix">The projection matrix for transforming view positions to clip space.</param>
    /// <param name="nearPlane">The near clipping plane distance.</param>
    /// <param name="farPlane">The far clipping plane distance.</param>
    /// <param name="viewportWidth">The viewport width in pixels.</param>
    /// <param name="viewportHeight">The viewport height in pixels.</param>
    /// <param name="sizePixels">The point size in screen pixels.</param>
    /// <param name="color">The RGBA color of the point.</param>
    /// <returns><c>true</c> if the quad was appended; <c>false</c> if the point was clipped or degenerate.</returns>
    public static bool TryAppendPoint(
        List<float> vertexData,
        Vector3 scenePosition,
        Matrix4x4 viewMatrix,
        Matrix4x4 projectionMatrix,
        float nearPlane,
        float farPlane,
        int viewportWidth,
        int viewportHeight,
        float sizePixels,
        Vector4 color)
    {
        Vector3 viewPosition = Vector3.Transform(scenePosition, viewMatrix);

        float nearZ = -MathF.Max(nearPlane, 1e-5f);
        float farZ = -MathF.Max(farPlane, nearPlane + 1e-3f);
        if (viewPosition.Z > nearZ || viewPosition.Z < farZ)
            return false;

        Vector4 clipPosition = Vector4.Transform(new Vector4(viewPosition, 1.0f), projectionMatrix);

        if (MathF.Abs(clipPosition.W) <= 1e-6f)
            return false;

        Vector2 ndc = new(clipPosition.X / clipPosition.W, clipPosition.Y / clipPosition.W);
        float halfSize = MathF.Max(sizePixels, 1.0f) * 0.5f;

        Vector2 offsetX = new(halfSize * 2.0f / Math.Max(viewportWidth, 1), 0.0f);
        Vector2 offsetY = new(0.0f, halfSize * 2.0f / Math.Max(viewportHeight, 1));

        AppendVertex(vertexData, ToClipPosition(ndc - offsetX + offsetY, clipPosition), color);
        AppendVertex(vertexData, ToClipPosition(ndc - offsetX - offsetY, clipPosition), color);
        AppendVertex(vertexData, ToClipPosition(ndc + offsetX + offsetY, clipPosition), color);
        AppendVertex(vertexData, ToClipPosition(ndc + offsetX + offsetY, clipPosition), color);
        AppendVertex(vertexData, ToClipPosition(ndc - offsetX - offsetY, clipPosition), color);
        AppendVertex(vertexData, ToClipPosition(ndc + offsetX - offsetY, clipPosition), color);
        return true;
    }

    private static float ComputeScreenLengthPixels(Vector2 deltaNdc, int viewportWidth, int viewportHeight)
    {
        Vector2 deltaPixels = new(
            deltaNdc.X * Math.Max(viewportWidth, 1) * 0.5f,
            deltaNdc.Y * Math.Max(viewportHeight, 1) * 0.5f);
        return deltaPixels.Length();
    }
}
