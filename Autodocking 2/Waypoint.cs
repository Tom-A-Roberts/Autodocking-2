using VRageMath;

namespace IngameScript
{
    internal partial class Program
    {
        public class Waypoint
        {
            public Vector3D forward;

            /// <summary>
            ///     15 is fast but not great for doing final landing
            ///     5 is better for being accurate
            ///     High max acceleration results in lots of joltyness at the end, however faster docking.
            /// </summary>
            public double maximumAcceleration = 5;

            /// <summary>
            ///     This includes the "caution" variable already given in the main script.
            /// </summary>
            public double PercentageOfMaxAcceleration = 1;

            public Vector3D position;
            public double required_accuracy = 0.1;
            public bool RequireRotation = true;
            public Vector3D up;


            public Waypoint(Vector3D _pos, Vector3D _forward, Vector3D _up)
            {
                position = _pos;
                forward = _forward;
                up = _up;
            }
        }
    }
}