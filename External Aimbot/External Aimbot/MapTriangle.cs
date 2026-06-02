using System.Numerics;
using System.Runtime.InteropServices;

namespace External_Aimbot
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct MapTriangle
    {
        public readonly Vector3 P1;
        public readonly Vector3 P2;
        public readonly Vector3 P3;

        public const int ByteSize = 36;

        public MapTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public Vector3 Centroid => (P1 + P2 + P3) / 3f;
    }
}
