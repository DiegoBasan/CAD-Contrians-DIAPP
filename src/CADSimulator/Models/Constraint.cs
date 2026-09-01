namespace CADSimulator.Models
{
    public enum ConstraintType
    {
        Coincident,
        Coaxial,
        Parallel,
        Perpendicular,
        Distance,
        Angle,
        Slider
    }

    public class Constraint
    {
        public ConstraintType Type { get; set; }
        public string ComponentAId { get; set; } = string.Empty;
        public string ComponentBId { get; set; } = string.Empty;

        /// <summary>Numeric parameter for the constraint (distance in mm, angle in degrees, slider offset, etc).</summary>
        public double Value { get; set; }
    }
}
