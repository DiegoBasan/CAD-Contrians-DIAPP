namespace CADSimulator.Models
{
    public struct Vector3d
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3d Zero => new Vector3d(0, 0, 0);
    }
}
