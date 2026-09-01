using System.Collections.Generic;
using CADSimulator.Models;

namespace CADSimulator.Utils
{
    /// <summary>Triangulates a simple (non-self-intersecting), planar 3D polygon loop via ear clipping.</summary>
    public static class PolygonTessellator
    {
        /// <summary>Returns a flat triangle list (length is a multiple of 3, 3 consecutive entries = one triangle), or empty if it can't be triangulated.</summary>
        public static List<Vector3d> Triangulate(IReadOnlyList<Vector3d> loop, Vector3d normal)
        {
            var triangles = new List<Vector3d>();
            if (loop.Count < 3)
            {
                return triangles;
            }

            // Project onto the plane's own 2D basis so ear-clipping can work with a 2D point-in-triangle test.
            var normalVec = new Vec3(normal.X, normal.Y, normal.Z).Normalized();
            var reference = System.Math.Abs(normalVec.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
            var uAxis = (reference - (normalVec * reference.Dot(normalVec))).Normalized();
            var vAxis = normalVec.Cross(uAxis);

            var points3D = new List<Vec3>(loop.Count);
            var points2D = new List<(double U, double V)>(loop.Count);
            foreach (var p in loop)
            {
                var v = new Vec3(p.X, p.Y, p.Z);
                points3D.Add(v);
                points2D.Add((v.Dot(uAxis), v.Dot(vAxis)));
            }

            // Ear clipping needs consistent (CCW, viewed from +normal) winding; flip if it's the other way.
            if (SignedArea(points2D) < 0)
            {
                points3D.Reverse();
                points2D.Reverse();
            }

            var indices = new List<int>();
            for (var i = 0; i < points2D.Count; i++)
            {
                indices.Add(i);
            }

            var guard = 0;
            while (indices.Count > 2 && guard++ < (points2D.Count * points2D.Count) + 8)
            {
                var earFound = false;
                for (var i = 0; i < indices.Count; i++)
                {
                    var prev = indices[(i - 1 + indices.Count) % indices.Count];
                    var curr = indices[i];
                    var next = indices[(i + 1) % indices.Count];

                    if (!IsConvex(points2D[prev], points2D[curr], points2D[next]))
                    {
                        continue;
                    }

                    var isEar = true;
                    for (var j = 0; j < indices.Count; j++)
                    {
                        var testIndex = indices[j];
                        if (testIndex == prev || testIndex == curr || testIndex == next)
                        {
                            continue;
                        }

                        if (PointInTriangle(points2D[testIndex], points2D[prev], points2D[curr], points2D[next]))
                        {
                            isEar = false;
                            break;
                        }
                    }

                    if (!isEar)
                    {
                        continue;
                    }

                    triangles.Add(ToVector3d(points3D[prev]));
                    triangles.Add(ToVector3d(points3D[curr]));
                    triangles.Add(ToVector3d(points3D[next]));
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    break; // degenerate polygon — stop rather than looping forever.
                }
            }

            return triangles;
        }

        private static Vector3d ToVector3d(Vec3 v) => new Vector3d(v.X, v.Y, v.Z);

        private static double SignedArea(List<(double U, double V)> points)
        {
            double sum = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var (u1, v1) = points[i];
                var (u2, v2) = points[(i + 1) % points.Count];
                sum += (u1 * v2) - (u2 * v1);
            }

            return sum * 0.5;
        }

        private static bool IsConvex((double U, double V) a, (double U, double V) b, (double U, double V) c) => Cross(a, b, c) > 0;

        private static double Cross((double U, double V) a, (double U, double V) b, (double U, double V) c) =>
            ((b.U - a.U) * (c.V - a.V)) - ((b.V - a.V) * (c.U - a.U));

        private static bool PointInTriangle((double U, double V) p, (double U, double V) a, (double U, double V) b, (double U, double V) c)
        {
            var d1 = Cross(a, b, p);
            var d2 = Cross(b, c, p);
            var d3 = Cross(c, a, p);

            var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPos = d1 > 0 || d2 > 0 || d3 > 0;

            return !(hasNeg && hasPos);
        }
    }
}
