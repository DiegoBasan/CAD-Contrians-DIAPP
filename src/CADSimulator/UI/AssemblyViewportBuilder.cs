using System.Windows.Media;
using System.Windows.Media.Media3D;
using CADSimulator.Models;
using CADSimulator.Utils;

namespace CADSimulator.UI
{
    /// <summary>Builds a WPF 3D scene graph from an Assembly, tessellating each component's planar faces.</summary>
    public static class AssemblyViewportBuilder
    {
        private static readonly Material PartMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0x8A, 0xA8, 0xC8)));

        public static Model3DGroup Build(Assembly assembly)
        {
            var root = new Model3DGroup();
            foreach (var component in assembly.Components)
            {
                var model = BuildComponentModel(component);
                if (model != null)
                {
                    root.Children.Add(model);
                }
            }

            return root;
        }

        private static Model3DGroup? BuildComponentModel(Component component)
        {
            var group = new Model3DGroup { Transform = ToTransform(component.Pose) };

            var mesh = BuildMesh(component);
            if (mesh != null)
            {
                group.Children.Add(new GeometryModel3D(mesh, PartMaterial) { BackMaterial = PartMaterial });
            }

            foreach (var child in component.Children)
            {
                var childModel = BuildComponentModel(child);
                if (childModel != null)
                {
                    group.Children.Add(childModel);
                }
            }

            return group.Children.Count > 0 ? group : null;
        }

        private static MeshGeometry3D? BuildMesh(Component component)
        {
            var mesh = new MeshGeometry3D();
            var hasTriangles = false;

            foreach (var face in component.Faces)
            {
                if (face.Type != SurfaceType.Planar || face.BoundaryLoop.Count < 3)
                {
                    continue; // not yet tessellated: curved/NURBS faces, or faces with holes.
                }

                var triangles = PolygonTessellator.Triangulate(face.BoundaryLoop, face.Axis);
                for (var i = 0; i + 2 < triangles.Count; i += 3)
                {
                    var baseIndex = mesh.Positions.Count;
                    mesh.Positions.Add(new Point3D(triangles[i].X, triangles[i].Y, triangles[i].Z));
                    mesh.Positions.Add(new Point3D(triangles[i + 1].X, triangles[i + 1].Y, triangles[i + 1].Z));
                    mesh.Positions.Add(new Point3D(triangles[i + 2].X, triangles[i + 2].Y, triangles[i + 2].Z));
                    mesh.TriangleIndices.Add(baseIndex);
                    mesh.TriangleIndices.Add(baseIndex + 1);
                    mesh.TriangleIndices.Add(baseIndex + 2);
                    hasTriangles = true;
                }
            }

            return hasTriangles ? mesh : null;
        }

        private static Transform3D ToTransform(Pose pose)
        {
            var group = new Transform3DGroup();

            // Pose.Rotation is Tait-Bryan XYZ (roll, pitch, yaw) degrees, applied as Rz * Ry * Rx —
            // matching Frame3.ToEulerDegrees(), so composing Rx then Ry then Rz here reproduces it.
            group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), pose.Rotation.X)));
            group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), pose.Rotation.Y)));
            group.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), pose.Rotation.Z)));
            group.Children.Add(new TranslateTransform3D(pose.Position.X, pose.Position.Y, pose.Position.Z));

            return group;
        }
    }
}
