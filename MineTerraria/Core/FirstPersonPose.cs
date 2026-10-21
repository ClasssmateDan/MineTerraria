using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public static class FirstPersonPose
{
    public static readonly Vector3 GripOffset = new(-0.01f, 0.30f, 0.04f);

    public static Matrix4x4 Hand(Matrix4x4 projection, float elapsed, bool moving, float swing = 0)
    {
        float bob = moving ? MathF.Sin(elapsed * 12) * 0.012f : 0;
        return Swing(swing) * Matrix4x4.CreateRotationX(-0.3f) * Matrix4x4.CreateRotationZ(0.75f) * Matrix4x4.CreateRotationY(-0.4f) *
            Matrix4x4.CreateTranslation(HeldItemLayout.HandOffsetX(projection.M11), -0.62f + bob, -1.15f);
    }

    public static Matrix4x4 Item(HeldItemLayout layout, Matrix4x4 restingHand, Matrix4x4 projection, float swing = 0)
    {
        Matrix4x4 gripOffset = Matrix4x4.CreateTranslation(GripOffset);
        Matrix4x4 attachment = gripOffset * restingHand;
        float rotation = layout.Rotation;
        if (layout.AlignUpright)
        {
            // Keep the tool upright in the camera, independent of the forearm.
            // Turning the arm inward must not tip the blade/shaft sideways.
            const float targetAngle = 95 * MathF.PI / 180;
            Vector2 tip = layout.At(layout.FlipHorizontally ? 0 : 1, 0);
            Vector3 origin = Vector3.Transform(Vector3.Zero, attachment);
            Vector2 a = ProjectDirection(Vector3.TransformNormal(new(tip, 0), attachment));
            Vector2 b = ProjectDirection(Vector3.TransformNormal(new(-tip.Y, tip.X, 0), attachment));
            Vector2 desired = new(MathF.Cos(targetAngle), MathF.Sin(targetAngle));
            float crossA = desired.X * a.Y - desired.Y * a.X;
            float crossB = desired.X * b.Y - desired.Y * b.X;
            rotation = MathF.Atan2(-crossA, crossB);
            if (Vector2.Dot(a * MathF.Cos(rotation) + b * MathF.Sin(rotation), desired) < 0) rotation += MathF.PI;
            rotation = MathF.IEEERemainder(rotation, MathF.Tau);

            // Under perspective, the projected tip direction is a*cos(r)+b*sin(r),
            // up to a positive scale. Solving it avoids atan2 wrap and roll limits.
            Vector2 ProjectDirection(Vector3 v) => new(origin.X * v.Z - v.X * origin.Z, origin.Y * v.Z - v.Y * origin.Z);
        }
        return Matrix4x4.CreateRotationZ(rotation) * gripOffset * Swing(swing) * restingHand;
    }

    public static Matrix4x4 Swing(float amount)
    {
        amount = float.IsFinite(amount) ? Math.Clamp(amount, 0, 1) : 0;
        return Matrix4x4.CreateTranslation(0, -0.16f, 0) * Matrix4x4.CreateRotationZ(0.38f * amount) *
            Matrix4x4.CreateRotationX(-0.65f * amount) * Matrix4x4.CreateTranslation(-0.07f * amount, 0.16f - 0.07f * amount, -0.10f * amount);
    }

    // Aspect-correct screen coordinates, positive Y up. Angles here correspond
    // to displayed pixels rather than the stretched normalized device space.
    public static Vector2 Project(Vector3 point, Matrix4x4 world, Matrix4x4 projection)
    {
        Vector4 clip = Vector4.Transform(new Vector4(point, 1), world * projection);
        return new Vector2(clip.X / clip.W * projection.M22 / projection.M11, clip.Y / clip.W);
    }
}
