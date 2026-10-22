using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>Builds the procedural meshes members need (Unity has no arch primitive).</summary>
    public static class MemberMeshes
    {
        private const int ArchSegments = 18;

        /// <summary>Submesh order of the site plot, matching the order of the plot renderer materials.</summary>
        public const int PlotTerraceLight = 0;
        public const int PlotTerraceDark = 1;
        public const int PlotLaterite = 2;

        /// <summary>
        /// Square-section tube following a parabola from footA to footB with its crown at crownY.
        /// Vertices are in the space of footA/footB, so the view's body needs no transform.
        /// UVs are in metres (across the section, along the arc) so textures keep their real size.
        /// </summary>
        public static Mesh BuildArch(Vector3 footA, Vector3 footB, float crownY, float thickness, Mesh reuse = null)
        {
            var mesh = reuse != null ? reuse : new Mesh { name = "Arch" };
            mesh.Clear();

            var rise = Mathf.Max(0.1f, crownY - (footA.y + footB.y) * 0.5f);
            var span = footB - footA;
            var planeNormal = Vector3.Cross(span, Vector3.up);
            planeNormal = planeNormal.sqrMagnitude < 1e-6f ? Vector3.forward : planeNormal.normalized;
            var half = thickness * 0.5f;

            var vertices = new List<Vector3>((ArchSegments + 1) * 8);
            var normals = new List<Vector3>((ArchSegments + 1) * 8);
            var uvs = new List<Vector2>((ArchSegments + 1) * 8);
            var triangles = new List<int>(ArchSegments * 24);
            var corners = new Vector3[4];
            var arc = 0f;
            var previous = footA;

            for (var i = 0; i <= ArchSegments; i++)
            {
                var t = (float)i / ArchSegments;
                var point = Point(footA, span, rise, t);
                arc += Vector3.Distance(previous, point);
                previous = point;
                var tangent = (Point(footA, span, rise, Mathf.Min(1f, t + 0.01f)) - Point(footA, span, rise, Mathf.Max(0f, t - 0.01f))).normalized;
                var outward = Vector3.Cross(planeNormal, tangent).normalized;

                corners[0] = point + (outward + planeNormal) * half;
                corners[1] = point + (outward - planeNormal) * half;
                corners[2] = point + (-outward - planeNormal) * half;
                corners[3] = point + (-outward + planeNormal) * half;

                for (var face = 0; face < 4; face++)
                {
                    var c0 = corners[face];
                    var c1 = corners[(face + 1) % 4];
                    var faceNormal = ((c0 + c1) * 0.5f - point).normalized;
                    vertices.Add(c0);
                    vertices.Add(c1);
                    normals.Add(faceNormal);
                    normals.Add(faceNormal);
                    uvs.Add(new Vector2(0f, arc));
                    uvs.Add(new Vector2(thickness, arc));
                }
            }

            for (var i = 0; i < ArchSegments; i++)
            {
                var ring = i * 8;
                var next = ring + 8;
                for (var face = 0; face < 4; face++)
                {
                    var a = ring + face * 2;
                    var b = a + 1;
                    var c = next + face * 2;
                    var d = c + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Point(Vector3 footA, Vector3 span, float rise, float t) =>
            footA + span * t + Vector3.up * (4f * rise * t * (1f - t));

        /// <summary>
        /// Sloped site plot: a wedge whose top falls by tan(slope) per metre towards +Z, laid out as
        /// Rwandan tea terraces (two grass tones) crossed by laterite paths, with laterite sides.
        /// Three submeshes: light terraces, dark terraces, laterite. UVs are in metres.
        /// </summary>
        public static Mesh BuildPlot(float minX, float maxX, float minZ, float maxZ, System.Func<float, float> groundY, float baseY)
        {
            var mesh = new Mesh { name = "Site Plot", subMeshCount = 3 };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var submeshes = new[] { new List<int>(), new List<int>(), new List<int>() };

            const int rows = 24;
            for (var row = 0; row < rows; row++)
            {
                var z0 = Mathf.Lerp(minZ, maxZ, (float)row / rows);
                var z1 = Mathf.Lerp(minZ, maxZ, (float)(row + 1) / rows);
                var target = row % 4 == 3 ? PlotLaterite : row % 2 == 0 ? PlotTerraceLight : PlotTerraceDark;
                var a = new Vector3(minX, groundY(z0), z0);
                var b = new Vector3(maxX, groundY(z0), z0);
                var c = new Vector3(maxX, groundY(z1), z1);
                var d = new Vector3(minX, groundY(z1), z1);
                var up = Vector3.Cross(d - a, b - a).normalized;
                AddQuad(vertices, normals, uvs, submeshes[target], a, b, c, d, up,
                    new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z));
            }

            var soil = submeshes[PlotLaterite];
            AddSide(vertices, normals, uvs, soil, new Vector3(minX, baseY, maxZ), new Vector3(maxX, baseY, maxZ),
                new Vector3(maxX, groundY(maxZ), maxZ), new Vector3(minX, groundY(maxZ), maxZ), Vector3.forward, true);
            AddSide(vertices, normals, uvs, soil, new Vector3(maxX, baseY, minZ), new Vector3(minX, baseY, minZ),
                new Vector3(minX, groundY(minZ), minZ), new Vector3(maxX, groundY(minZ), minZ), Vector3.back, true);

            for (var row = 0; row < rows; row++)
            {
                var z0 = Mathf.Lerp(minZ, maxZ, (float)row / rows);
                var z1 = Mathf.Lerp(minZ, maxZ, (float)(row + 1) / rows);
                AddSide(vertices, normals, uvs, soil, new Vector3(minX, baseY, z0), new Vector3(minX, baseY, z1),
                    new Vector3(minX, groundY(z1), z1), new Vector3(minX, groundY(z0), z0), Vector3.left, false);
                AddSide(vertices, normals, uvs, soil, new Vector3(maxX, baseY, z1), new Vector3(maxX, baseY, z0),
                    new Vector3(maxX, groundY(z0), z0), new Vector3(maxX, groundY(z1), z1), Vector3.right, false);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            for (var i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Compacted building platform: a surface following the slope just above the terraces over the
        /// house footprint, with a short kerb all round so it reads as prepared, firm ground.
        /// </summary>
        public static Mesh BuildPlatform(Rect area, System.Func<float, float> groundY, float lift)
        {
            var mesh = new Mesh { name = "Building Platform" };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            const float kerb = 0.12f;

            var rows = Mathf.Max(2, Mathf.CeilToInt(area.height));
            for (var row = 0; row < rows; row++)
            {
                var z0 = Mathf.Lerp(area.yMin, area.yMax, (float)row / rows);
                var z1 = Mathf.Lerp(area.yMin, area.yMax, (float)(row + 1) / rows);
                var a = new Vector3(area.xMin, groundY(z0) + lift, z0);
                var b = new Vector3(area.xMax, groundY(z0) + lift, z0);
                var c = new Vector3(area.xMax, groundY(z1) + lift, z1);
                var d = new Vector3(area.xMin, groundY(z1) + lift, z1);
                var up = Vector3.Cross(d - a, b - a).normalized;
                AddQuad(vertices, normals, uvs, triangles, a, b, c, d, up,
                    new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z));

                // Side kerbs follow the slope.
                foreach (var x in new[] { area.xMin, area.xMax })
                {
                    var outward = x < 0f ? Vector3.left : Vector3.right;
                    AddSide(vertices, normals, uvs, triangles, new Vector3(x, groundY(z0) - kerb, z0), new Vector3(x, groundY(z1) - kerb, z1),
                        new Vector3(x, groundY(z1) + lift, z1), new Vector3(x, groundY(z0) + lift, z0), outward, false);
                }
            }

            foreach (var z in new[] { area.yMin, area.yMax })
            {
                var outward = z < area.center.y ? Vector3.back : Vector3.forward;
                AddSide(vertices, normals, uvs, triangles, new Vector3(area.xMin, groundY(z) - kerb, z), new Vector3(area.xMax, groundY(z) - kerb, z),
                    new Vector3(area.xMax, groundY(z) + lift, z), new Vector3(area.xMin, groundY(z) + lift, z), outward, true);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Side faces are mapped with the horizontal run as U and height as V.</summary>
        private static void AddSide(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, bool alongX)
        {
            Vector2 Uv(Vector3 p) => new Vector2(alongX ? p.x : p.z, p.y);
            AddQuad(vertices, normals, uvs, triangles, a, b, c, d, outward, Uv(a), Uv(b), Uv(c), Uv(d));
        }

        /// <summary>Adds quad abcd facing along outward. Unity treats triangle (p,q,r) as facing Cross(q-p, r-p).</summary>
        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            var start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
            for (var i = 0; i < 4; i++) normals.Add(outward);

            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f)
            {
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            else
            {
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
        }
    }
}
