using System.Numerics;

namespace External_Aimbot
{
    internal sealed class MapRayTracer
    {
        private const int LeafTriangleLimit = 4;
        private const float RayEpsilon = 1e-7f;
        private const float RayStartOffset = 12f;
        private const float RayEndPadding = 8f;

        private readonly MapTriangle[] _triangles;
        private readonly int[] _triangleIndices;
        private readonly BvhNode[] _nodes;

        public int TriangleCount => _triangles.Length;

        private MapRayTracer(MapTriangle[] triangles, int[] triangleIndices, BvhNode[] nodes)
        {
            _triangles = triangles;
            _triangleIndices = triangleIndices;
            _nodes = nodes;
        }

        public static MapRayTracer? LoadFromTriFile(string path)
        {
            if (!File.Exists(path))
                return null;

            long fileSize = new FileInfo(path).Length;
            if (fileSize < MapTriangle.ByteSize || fileSize % MapTriangle.ByteSize != 0)
                return null;

            int triangleCount = (int)(fileSize / MapTriangle.ByteSize);
            var triangles = new MapTriangle[triangleCount];

            using (var stream = File.OpenRead(path))
            {
                var buffer = new byte[MapTriangle.ByteSize];
                for (int i = 0; i < triangleCount; i++)
                {
                    int read = stream.Read(buffer, 0, MapTriangle.ByteSize);
                    if (read != MapTriangle.ByteSize)
                        return null;

                    triangles[i] = new MapTriangle(
                        ReadVector3(buffer, 0),
                        ReadVector3(buffer, 12),
                        ReadVector3(buffer, 24));
                }
            }

            var builder = new BvhBuilder(triangles, LeafTriangleLimit);
            (BvhNode[] nodes, int[] indices) = builder.Build();
            return new MapRayTracer(triangles, indices, nodes);
        }

        public bool IsVisible(Vector3 start, Vector3 head, Vector3 chest)
        {
            if (HasClearLine(start, head))
                return true;

            if (HasClearLine(start, chest))
                return true;

            return HasClearLine(start, chest + new Vector3(0f, 0f, -20f));
        }

        private bool HasClearLine(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float distance = delta.Length();
            if (distance < 1f)
                return true;

            Vector3 direction = delta / distance;
            Vector3 rayStart = start + direction * RayStartOffset;
            Vector3 rayEnd = end - direction * RayEndPadding;
            Vector3 rayDelta = rayEnd - rayStart;
            float rayDistance = rayDelta.Length();
            if (rayDistance < 1f)
                return true;

            Vector3 rayDirection = rayDelta / rayDistance;
            return !RaycastAnyHit(rayStart, rayDirection, rayDistance);
        }

        private bool RaycastAnyHit(Vector3 origin, Vector3 direction, float maxDistance)
        {
            if (_nodes.Length == 0)
                return false;

            Span<int> stack = stackalloc int[128];
            int stackSize = 0;
            stack[stackSize++] = 0;

            while (stackSize > 0)
            {
                int nodeIndex = stack[--stackSize];
                ref BvhNode node = ref _nodes[nodeIndex];

                if (!IntersectsAabb(origin, direction, maxDistance, node.Min, node.Max))
                    continue;

                if (node.IsLeaf)
                {
                    for (int i = 0; i < node.TriangleCount; i++)
                    {
                        ref MapTriangle triangle = ref _triangles[_triangleIndices[node.TriangleStart + i]];
                        if (IntersectsTriangle(origin, direction, 0.05f, maxDistance, triangle))
                            return true;
                    }

                    continue;
                }

                if (node.Right >= 0 && stackSize < stack.Length)
                    stack[stackSize++] = node.Right;

                if (node.Left >= 0 && stackSize < stack.Length)
                    stack[stackSize++] = node.Left;
            }

            return false;
        }

        private static Vector3 ReadVector3(byte[] buffer, int offset) =>
            new(
                BitConverter.ToSingle(buffer, offset),
                BitConverter.ToSingle(buffer, offset + 4),
                BitConverter.ToSingle(buffer, offset + 8));

        private static bool IntersectsAabb(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            Vector3 min,
            Vector3 max)
        {
            float tMin = 0f;
            float tMax = maxDistance;

            if (!Slab(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax))
                return false;

            if (!Slab(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax))
                return false;

            if (!Slab(origin.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax))
                return false;

            return tMax >= tMin && tMax >= 0f;
        }

        private static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (MathF.Abs(direction) < RayEpsilon)
                return origin >= min && origin <= max;

            float inv = 1f / direction;
            float t1 = (min - origin) * inv;
            float t2 = (max - origin) * inv;

            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            return tMin <= tMax;
        }

        private static bool IntersectsTriangle(
            Vector3 origin,
            Vector3 direction,
            float minDistance,
            float maxDistance,
            MapTriangle triangle)
        {
            Vector3 edge1 = triangle.P2 - triangle.P1;
            Vector3 edge2 = triangle.P3 - triangle.P1;
            Vector3 h = Vector3.Cross(direction, edge2);
            float a = Vector3.Dot(edge1, h);

            if (a > -RayEpsilon && a < RayEpsilon)
                return false;

            float f = 1f / a;
            Vector3 s = origin - triangle.P1;
            float u = f * Vector3.Dot(s, h);
            if (u is < 0f or > 1f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(direction, q);
            if (v < 0f || u + v > 1f)
                return false;

            float t = f * Vector3.Dot(edge2, q);
            return t > minDistance && t < maxDistance;
        }

        private struct BvhNode
        {
            public Vector3 Min;
            public Vector3 Max;
            public int Left;
            public int Right;
            public int TriangleStart;
            public int TriangleCount;

            public bool IsLeaf => TriangleCount > 0;
        }

        private sealed class BvhBuilder
        {
            private readonly MapTriangle[] _triangles;
            private readonly int[] _indices;
            private readonly int _leafLimit;
            private readonly List<BvhNode> _nodes = [];

            public BvhBuilder(MapTriangle[] triangles, int leafLimit)
            {
                _triangles = triangles;
                _leafLimit = leafLimit;
                _indices = new int[triangles.Length];
                for (int i = 0; i < _indices.Length; i++)
                    _indices[i] = i;
            }

            public (BvhNode[] Nodes, int[] Indices) Build()
            {
                if (_triangles.Length == 0)
                    return ([], _indices);

                BuildNode(0, _indices.Length, 0);
                return (_nodes.ToArray(), _indices);
            }

            private int BuildNode(int start, int end, int depth)
            {
                int nodeIndex = _nodes.Count;
                _nodes.Add(default);

                ComputeBounds(start, end, out Vector3 min, out Vector3 max);

                int count = end - start;
                if (count <= _leafLimit)
                {
                    _nodes[nodeIndex] = new BvhNode
                    {
                        Min = min,
                        Max = max,
                        Left = -1,
                        Right = -1,
                        TriangleStart = start,
                        TriangleCount = count,
                    };
                    return nodeIndex;
                }

                int axis = LongestAxis(max - min);
                int mid = PartitionByAxis(start, end, axis);

                if (mid <= start || mid >= end)
                    mid = start + count / 2;

                int left = BuildNode(start, mid, depth + 1);
                int right = BuildNode(mid, end, depth + 1);

                _nodes[nodeIndex] = new BvhNode
                {
                    Min = min,
                    Max = max,
                    Left = left,
                    Right = right,
                    TriangleStart = -1,
                    TriangleCount = 0,
                };

                return nodeIndex;
            }

            private void ComputeBounds(int start, int end, out Vector3 min, out Vector3 max)
            {
                min = _triangles[_indices[start]].P1;
                max = min;

                for (int i = start; i < end; i++)
                {
                    ref MapTriangle triangle = ref _triangles[_indices[i]];
                    Expand(ref min, ref max, triangle.P1);
                    Expand(ref min, ref max, triangle.P2);
                    Expand(ref min, ref max, triangle.P3);
                }
            }

            private int PartitionByAxis(int start, int end, int axis)
            {
                int mid = (start + end) / 2;
                Array.Sort(_indices, start, end - start, Comparer<int>.Create((a, b) =>
                {
                    float ca = GetAxis(_triangles[a].Centroid, axis);
                    float cb = GetAxis(_triangles[b].Centroid, axis);
                    return ca.CompareTo(cb);
                }));

                return mid;
            }

            private static float GetAxis(Vector3 point, int axis) =>
                axis switch
                {
                    0 => point.X,
                    1 => point.Y,
                    _ => point.Z,
                };

            private static int LongestAxis(Vector3 extent)
            {
                if (extent.X >= extent.Y && extent.X >= extent.Z)
                    return 0;

                return extent.Y >= extent.Z ? 1 : 2;
            }

            private static void Expand(ref Vector3 min, ref Vector3 max, Vector3 point)
            {
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }
    }
}
