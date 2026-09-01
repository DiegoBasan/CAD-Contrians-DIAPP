using System.Collections.Generic;
using System.Linq;
using CADSimulator.Models;
using CADSimulator.Utils;

namespace CADSimulator.Core
{
    /// <summary>
    /// Converts an Assembly into a plain, JSON-friendly tree for the WebView2/Three.js frontend,
    /// tessellating each component's planar faces (Component.Faces) into a flat triangle array
    /// the browser side can hand straight to a THREE.BufferGeometry. The STEP-derived data
    /// (poses, analytic face geometry) stays exact on the C# side — this DTO exists only for display.
    /// </summary>
    public static class AssemblySceneExport
    {
        public class ComponentDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public double[] Position { get; set; } = new double[3];
            public double[] RotationDeg { get; set; } = new double[3];
            public int FaceCount { get; set; }

            /// <summary>Flattened triangle list: 3 consecutive doubles per vertex, 3 vertices per triangle.</summary>
            public double[] Triangles { get; set; } = System.Array.Empty<double>();

            public List<ComponentDto> Children { get; set; } = new List<ComponentDto>();
        }

        public class AssemblyDto
        {
            public string Name { get; set; } = string.Empty;
            public List<ComponentDto> Components { get; set; } = new List<ComponentDto>();
        }

        public static AssemblyDto ToDto(Assembly assembly) => new AssemblyDto
        {
            Name = assembly.Name,
            Components = assembly.Components.Select(ToComponentDto).ToList()
        };

        private static ComponentDto ToComponentDto(Component component)
        {
            var triangles = new List<double>();
            foreach (var face in component.Faces)
            {
                if (face.Type != SurfaceType.Planar || face.BoundaryLoop.Count < 3)
                {
                    continue;
                }

                foreach (var point in PolygonTessellator.Triangulate(face.BoundaryLoop, face.Axis))
                {
                    triangles.Add(point.X);
                    triangles.Add(point.Y);
                    triangles.Add(point.Z);
                }
            }

            return new ComponentDto
            {
                Id = component.Id,
                Name = component.Name,
                Position = new[] { component.Pose.Position.X, component.Pose.Position.Y, component.Pose.Position.Z },
                RotationDeg = new[] { component.Pose.Rotation.X, component.Pose.Rotation.Y, component.Pose.Rotation.Z },
                FaceCount = component.Faces.Count,
                Triangles = triangles.ToArray(),
                Children = component.Children.Select(ToComponentDto).ToList()
            };
        }
    }
}
