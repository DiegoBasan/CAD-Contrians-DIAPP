namespace CADSimulator.Models
{
    public enum JointType
    {
        Fixed,
        Revolute,
        Slider
    }

    public class Joint
    {
        public string Name { get; set; } = string.Empty;
        public JointType Type { get; set; }
        public string ComponentId { get; set; } = string.Empty;
        public Vector3d Axis { get; set; } = new Vector3d(0, 0, 1);
        public double Min { get; set; }
        public double Max { get; set; }
        public double CurrentValue { get; set; }
    }
}
