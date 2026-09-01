using System.Collections.Generic;

namespace CADSimulator.Models
{
    public enum SurfaceType
    {
        Planar,
        Cylindrical
    }

    /// <summary>
    /// Exact analytic geometry for one BREP face, read directly from STEP (PLANE /
    /// CYLINDRICAL_SURFACE) — meant as a constraint target (Coincident/Coaxial/Parallel/...).
    /// Curved/free-form (B-spline) faces are not extracted here.
    /// </summary>
    public class FaceGeometry
    {
        public SurfaceType Type { get; set; }
        public Vector3d Origin { get; set; }

        /// <summary>Face normal for a Planar face, or the axis direction for a Cylindrical face.</summary>
        public Vector3d Axis { get; set; }

        /// <summary>Cylinder radius; unused for Planar faces.</summary>
        public double Radius { get; set; }

        /// <summary>
        /// The face's outer boundary as an ordered vertex loop, in the component's local
        /// coordinates — only populated for a Planar face whose outer bound is entirely straight
        /// edges (STEP LINE curves). Used to tessellate the face for display; empty for any face
        /// this scaffold doesn't yet know how to bound exactly (curved edges, cylindrical/NURBS
        /// surfaces) rather than approximating it.
        /// </summary>
        public List<Vector3d> BoundaryLoop { get; set; } = new List<Vector3d>();
    }
}
