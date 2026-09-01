using CADSimulator.Models;

namespace CADSimulator.Utils
{
    public static class MathHelper
    {
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        public static Vector3d Lerp(Vector3d a, Vector3d b, double t)
        {
            return new Vector3d(
                Lerp(a.X, b.X, t),
                Lerp(a.Y, b.Y, t),
                Lerp(a.Z, b.Z, t));
        }

        public static Pose Lerp(Pose a, Pose b, double t)
        {
            return new Pose
            {
                Position = Lerp(a.Position, b.Position, t),
                Rotation = Lerp(a.Rotation, b.Rotation, t)
            };
        }

        public static double Clamp01(double t) => t < 0 ? 0 : (t > 1 ? 1 : t);
    }
}
