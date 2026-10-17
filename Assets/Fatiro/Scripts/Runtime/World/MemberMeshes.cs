using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.World
{
    /// <summary>Builds the procedural meshes members need (Unity has no arch primitive).</summary>
    public static class MemberMeshes
    {
        private const int ArchSegments = 18;

        /// <summary>
        /// Square-section tube following a parabola from footA to footB with its crown at crownY.
        /// Vertices are in the space of footA/footB, so the view's body needs no transform.
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
            var triangles = new List<int>(ArchSegments * 24);
            var corners = new Vector3[4];

            for (var i = 0; i <= ArchSegments; i++)
            {
                var t = (float)i / ArchSegments;
                var point = Point(footA, span, rise, t);
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
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Point(Vector3 footA, Vector3 span, float rise, float t) =>
            footA + span * t + Vector3.up * (4f * rise * t * (1f - t));

        /// <summary>
        /// Sloped site plot: a wedge whose top falls by tan(slope) per metre towards +Z, striped like
        /// Rwandan tea terraces over red laterite so the slope reads clearly at table scale.
        /// </summary>
        public static Mesh BuildPlot(float minX, float maxX, float minZ, float maxZ, System.Func<float, float> groundY, float baseY)
        {
            var mesh = new Mesh { name = "Site Plot" };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            const int rows = 24;
            // Tea terraces: alternating greens, with a red laterite path every fourth step.
            var light = new Color(0.45f, 0.64f, 0.30f);
            var dark = new Color(0.34f, 0.53f, 0.24f);
            var path = new Color(0.62f, 0.33f, 0.20f);

            // Top surface in strips so alternate metres can be shaded like terraces.
            for (var row = 0; row < rows; row++)
            {
                var z0 = Mathf.Lerp(minZ, maxZ, (float)row / rows);
                var z1 = Mathf.Lerp(minZ, maxZ, (float)(row + 1) / rows);
                var tint = row % 4 == 3 ? path : row % 2 == 0 ? light : dark;
                var a = new Vector3(minX, groundY(z0), z0);
                var b = new Vector3(maxX, groundY(z0), z0);
                var d = new Vector3(minX, groundY(z1), z1);
                var up = Vector3.Cross(d - a, b - a).normalized;
                AddQuad(vertices, normals, colors, triangles, a, b, new Vector3(maxX, groundY(z1), z1), d, up, tint);
            }

            var side = new Color(0.55f, 0.24f, 0.14f);
            // Front (downhill) and back faces.
            AddQuad(vertices, normals, colors, triangles,
                new Vector3(minX, baseY, maxZ), new Vector3(maxX, baseY, maxZ),
                new Vector3(maxX, groundY(maxZ), maxZ), new Vector3(minX, groundY(maxZ), maxZ), Vector3.forward, side);
            AddQuad(vertices, normals, colors, triangles,
                new Vector3(maxX, baseY, minZ), new Vector3(minX, baseY, minZ),
                new Vector3(minX, groundY(minZ), minZ), new Vector3(maxX, groundY(minZ), minZ), Vector3.back, side);

            // Left and right faces follow the slope.
            for (var row = 0; row < rows; row++)
            {
                var z0 = Mathf.Lerp(minZ, maxZ, (float)row / rows);
                var z1 = Mathf.Lerp(minZ, maxZ, (float)(row + 1) / rows);
                AddQuad(vertices, normals, colors, triangles,
                    new Vector3(minX, baseY, z0), new Vector3(minX, baseY, z1),
                    new Vector3(minX, groundY(z1), z1), new Vector3(minX, groundY(z0), z0), Vector3.left, side);
                AddQuad(vertices, normals, colors, triangles,
                    new Vector3(maxX, baseY, z1), new Vector3(maxX, baseY, z0),
                    new Vector3(maxX, groundY(z0), z0), new Vector3(maxX, groundY(z1), z1), Vector3.right, side);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds quad abcd facing along outward. Unity treats triangle (p,q,r) as facing Cross(q-p, r-p).</summary>
        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            var start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (var i = 0; i < 4; i++)
            {
                normals.Add(outward);
                colors.Add(color);
            }

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
