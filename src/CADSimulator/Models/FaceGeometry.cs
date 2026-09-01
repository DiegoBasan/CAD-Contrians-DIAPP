namespace CADSimulator.Models
{
    public enum SurfaceType
    {
        Planar,
        Cylindrical
    }

    /// <summary>
    /// Exact analytic geometry for one BREP face, read directly from STEP (PLANE /
    /// CYLINDRICAL_SURFACE) — meant as a constraint target (Coincident/Coaxial/Parallel/...),
    /// not for display. Curved/free-form (B-spline) faces are not extracted here.
    /// </summary>
    public class FaceGeometry
    {
        public SurfaceType Type { get; set; }
        public Vector3d Origin { get; set; }

        /// <summary>Face normal for a Planar face, or the axis direction for a Cylindrical face.</summary>
        public Vector3d Axis { get; set; }

        /// <summary>Cylinder radius; unused for Planar faces.</summary>
        public double Radius { get; set; }
    }
}
