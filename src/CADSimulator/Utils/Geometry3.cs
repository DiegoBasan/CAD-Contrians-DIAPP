using CADSimulator.Models;

namespace CADSimulator.Utils
{
    public struct Vec3
    {
        public double X;
        public double Y;
        public double Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public double Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;
        public Vec3 Cross(Vec3 other) => new Vec3(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X);

        public double Length => System.Math.Sqrt(Dot(this));

        public Vec3 Normalized()
        {
            var len = Length;
            return len > 1e-9 ? new Vec3(X / len, Y / len, Z / len) : new Vec3(0, 0, 0);
        }
    }

    /// <summary>
    /// An orthonormal right-handed coordinate frame: an Origin plus X/Y/Z basis vectors, all
    /// expressed in some containing (parent) coordinate system.
    /// </summary>
    public struct Frame3
    {
        public Vec3 Origin;
        public Vec3 XAxis;
        public Vec3 YAxis;
        public Vec3 ZAxis;

        public static Frame3 Identity => new Frame3
        {
            Origin = new Vec3(0, 0, 0),
            XAxis = new Vec3(1, 0, 0),
            YAxis = new Vec3(0, 1, 0),
            ZAxis = new Vec3(0, 0, 1)
        };

        /// <summary>Maps a point given in this frame's own local coordinates into this frame's containing (parent) space.</summary>
        public Vec3 TransformPoint(Vec3 localPoint) => Origin + (XAxis * localPoint.X) + (YAxis * localPoint.Y) + (ZAxis * localPoint.Z);

        /// <summary>Maps a direction given in this frame's own local coordinates into this frame's containing (parent) space.</summary>
        public Vec3 TransformDirection(Vec3 localDirection) => (XAxis * localDirection.X) + (YAxis * localDirection.Y) + (ZAxis * localDirection.Z);

        /// <summary>Re-expresses a point (given in this frame's containing space) in this frame's own local coordinates.</summary>
        public Vec3 ToLocalPoint(Vec3 point) => ToLocalDirection(point - Origin);

        /// <summary>Re-expresses a direction (given in this frame's containing space) in this frame's own local coordinates.</summary>
        public Vec3 ToLocalDirection(Vec3 direction) => new Vec3(direction.Dot(XAxis), direction.Dot(YAxis), direction.Dot(ZAxis));

        /// <summary>Treats `this` as a frame defined in `parent`'s local coordinates and returns it expressed in parent's own containing space.</summary>
        public Frame3 ComposeWithParent(Frame3 parent) => new Frame3
        {
            Origin = parent.TransformPoint(Origin),
            XAxis = parent.TransformDirection(XAxis),
            YAxis = parent.TransformDirection(YAxis),
            ZAxis = parent.TransformDirection(ZAxis)
        };

        /// <summary>Returns `this` frame re-expressed relative to `reference` (both given in the same containing space).</summary>
        public Frame3 RelativeTo(Frame3 reference) => new Frame3
        {
            Origin = reference.ToLocalPoint(Origin),
            XAxis = reference.ToLocalDirection(XAxis),
            YAxis = reference.ToLocalDirection(YAxis),
            ZAxis = reference.ToLocalDirection(ZAxis)
        };

        public Pose ToPose() => new Pose
        {
            Position = new Vector3d(Origin.X, Origin.Y, Origin.Z),
            Rotation = ToEulerDegrees()
        };

        /// <summary>Tait-Bryan XYZ (roll, pitch, yaw) in degrees, assuming R = Rz(yaw) * Ry(pitch) * Rx(roll).</summary>
        private Vector3d ToEulerDegrees()
        {
            // Rotation matrix columns are XAxis, YAxis, ZAxis (this frame's basis in its parent space).
            var r00 = XAxis.X;
            var r10 = XAxis.Y;
            var r20 = XAxis.Z;
            var r21 = YAxis.Z;
            var r22 = ZAxis.Z;

            var pitch = System.Math.Asin(Clamp(-r20, -1.0, 1.0));
            double roll, yaw;
            if (System.Math.Cos(pitch) > 1e-6)
            {
                roll = System.Math.Atan2(r21, r22);
                yaw = System.Math.Atan2(r10, r00);
            }
            else
            {
                // Gimbal lock: roll and yaw trade off freely, pick roll = 0.
                roll = 0;
                yaw = System.Math.Atan2(-YAxis.X, YAxis.Y);
            }

            const double toDegrees = 180.0 / System.Math.PI;
            return new Vector3d(roll * toDegrees, pitch * toDegrees, yaw * toDegrees);
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }
}
