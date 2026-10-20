using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DogLife.EditorTools
{
    // 얇은 잎을 찌그러뜨리는 면 축소 대신, 완전한 잎의 밀도만 단계적으로 줄입니다.
    public static class SpringFoliageLodRepair
    {
        const string Folder = "Assets/MapDesigne/Spring/LOD";
        sealed class Island
        {
            public int root, slot, triangles;
            public Bounds bounds;
            public bool preserve;
            public uint rank;
        }
        struct Vertex : IEquatable<Vertex>
        {
            public Vector3 position, normal;
            public Vector4 tangent;
            public Vector2 uv, uv2;
            public Color color;
            public bool Equals(Vertex v) => position.Equals(v.position) && normal.Equals(v.normal) &&
                tangent.Equals(v.tangent) && uv.Equals(v.uv) && uv2.Equals(v.uv2) && color.Equals(v.color);
            public override bool Equals(object other) => other is Vertex v && Equals(v);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = position.GetHashCode(); h = h * 397 ^ normal.GetHashCode();
                    h = h * 397 ^ tangent.GetHashCode(); h = h * 397 ^ uv.GetHashCode();
                    h = h * 397 ^ uv2.GetHashCode(); return h * 397 ^ color.GetHashCode();
                }
            }
        }
        static int Find(int[] parent, int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }
        static void Join(int[] parent, int a, int b)
        {
            a = Find(parent, a); b = Find(parent, b);
            if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
        }
        static uint Rank(Vector3 point)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)Mathf.RoundToInt(point.x * 1000)) * 16777619;
                h = (h ^ (uint)Mathf.RoundToInt(point.y * 1000)) * 16777619;
                h = (h ^ (uint)Mathf.RoundToInt(point.z * 1000)) * 16777619;
                h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15;
                return h;
            }
        }
        public static string RebuildAll()
        {
            var paths = AssetDatabase.FindAssets("t:Mesh", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_0.asset")).OrderBy(p => p).ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("Export Spring LOD0 source meshes first.");
            long original = 0, middle = 0, distant = 0; int retained = 0;
            foreach (string path in paths)
            {
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var v = source.vertices; var normals = source.normals; var uv = source.uv;
                var tangents = source.tangents; var uv2 = source.uv2; var colors = source.colors;
                if (normals.Length != v.Length || uv.Length != v.Length)
                    throw new InvalidOperationException("Missing source normals/UV: " + path);
                var parent = Enumerable.Range(0, v.Length).ToArray();
                var welded = new Dictionary<Vector3, int>();
                for (int i = 0; i < v.Length; i++)
                {
                    if (welded.TryGetValue(v[i], out int other)) Join(parent, i, other);
                    else welded.Add(v[i], i);
                }
                var parts = Enumerable.Range(0, source.subMeshCount).Select(source.GetTriangles).ToArray();
                foreach (var part in parts)
                    for (int t = 0; t < part.Length; t += 3)
                    { Join(parent, part[t], part[t + 1]); Join(parent, part[t], part[t + 2]); }
                var islands = new Dictionary<int, Island>();
                for (int slot = 0; slot < parts.Length; slot++)
                {
                    var part = parts[slot];
                    for (int t = 0; t < part.Length; t += 3)
                    {
                        int root = Find(parent, part[t]);
                        if (!islands.TryGetValue(root, out var leaf))
                        {
                            leaf = new Island { root = root, slot = slot, bounds = new Bounds(v[part[t]], Vector3.zero) };
                            islands.Add(root, leaf);
                        }
                        leaf.triangles++;
                        for (int j = 0; j < 3; j++) leaf.bounds.Encapsulate(v[part[t + j]]);
                    }
                }
                var tokens = Path.GetFileNameWithoutExtension(path).Split('_');
                int cellX = int.Parse(tokens[tokens.Length - 3]), cellZ = int.Parse(tokens[tokens.Length - 2]);
                foreach (var leaf in islands.Values)
                {
                    var b = leaf.bounds;
                    // 원래 12m 구역 경계에서 나뉜 잎은 양쪽 LOD에서 모두 남겨 절단면을 막습니다.
                    leaf.preserve = leaf.triangles > 64 || b.size.magnitude > 1.6f ||
                        b.min.x < cellX * 12 + .01f || b.max.x > (cellX + 1) * 12 - .01f ||
                        b.min.z < cellZ * 12 + .01f || b.max.z > (cellZ + 1) * 12 - .01f;
                    leaf.rank = Rank(b.center);
                }
                // 가장 바깥쪽 잎은 남겨 수관의 외곽과 LOD 선택 범위를 유지합니다.
                for (int axis = 0; axis < 3 && islands.Count > 0; axis++)
                {
                    islands.Values.OrderBy(i => i.bounds.min[axis]).First().preserve = true;
                    islands.Values.OrderByDescending(i => i.bounds.max[axis]).First().preserve = true;
                }
                var buckets = islands.Values.Where(i => !i.preserve).GroupBy(i =>
                    (i.slot, Mathf.FloorToInt(i.bounds.center.x / 2), Mathf.FloorToInt(i.bounds.center.y / 2), Mathf.FloorToInt(i.bounds.center.z / 2)))
                    .Select(g => g.OrderBy(i => i.rank).ThenBy(i => i.root).ToArray()).ToArray();
                original += parts.Sum(p => (long)p.Length / 3);
                retained += islands.Values.Count(i => i.preserve);
                for (int level = 1; level <= 2; level++)
                {
                    var keep = new HashSet<int>(islands.Values.Where(i => i.preserve).Select(i => i.root));
                    // 같은 정렬에서 일부만 덜어 LOD2의 잎이 LOD1에 없던 위치에서 나타나지 않습니다.
                    foreach (var bucket in buckets)
                    {
                        int count = Mathf.Min(bucket.Length, Mathf.Max(2, Mathf.CeilToInt(bucket.Length * (level == 1 ? .8f : .55f))));
                        for (int i = 0; i < count; i++) keep.Add(bucket[i].root);
                    }
                    var unique = new Dictionary<Vertex, int>(); var output = new List<Vertex>();
                    var indices = new List<int>[parts.Length];
                    for (int slot = 0; slot < parts.Length; slot++)
                    {
                        var part = parts[slot]; indices[slot] = new List<int>();
                        for (int t = 0; t < part.Length; t += 3)
                        {
                            if (!keep.Contains(Find(parent, part[t]))) continue;
                            for (int j = 0; j < 3; j++)
                            {
                                int i = part[t + j];
                                var vertex = new Vertex { position = v[i], normal = normals[i], uv = uv[i],
                                    tangent = tangents.Length == v.Length ? tangents[i] : Vector4.zero,
                                    uv2 = uv2.Length == v.Length ? uv2[i] : Vector2.zero,
                                    color = colors.Length == v.Length ? colors[i] : Color.white };
                                if (!unique.TryGetValue(vertex, out int mapped))
                                { mapped = output.Count; unique.Add(vertex, mapped); output.Add(vertex); }
                                indices[slot].Add(mapped);
                            }
                        }
                    }
                    // 좌표·UV·법선을 원본에서 복사해 잎의 면이 눌리거나 뒤집히지 않게 합니다.
                    var mesh = new Mesh { name = source.name.Substring(0, source.name.Length - 1) + level, indexFormat = IndexFormat.UInt32 };
                    mesh.SetVertices(output.Select(x => x.position).ToList());
                    mesh.SetNormals(output.Select(x => x.normal).ToList()); mesh.SetUVs(0, output.Select(x => x.uv).ToList());
                    if (tangents.Length == v.Length) mesh.SetTangents(output.Select(x => x.tangent).ToList());
                    if (uv2.Length == v.Length) mesh.SetUVs(1, output.Select(x => x.uv2).ToList());
                    if (colors.Length == v.Length) mesh.SetColors(output.Select(x => x.color).ToList());
                    mesh.subMeshCount = parts.Length;
                    for (int slot = 0; slot < indices.Length; slot++) mesh.SetTriangles(indices[slot], slot, false);
                    mesh.bounds = source.bounds;
                    if (tangents.Length != v.Length) mesh.RecalculateTangents();
                    MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Low);
                    string destination = path.Substring(0, path.Length - 7) + level + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(destination);
                    if (existing)
                    { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); }
                    else AssetDatabase.CreateAsset(mesh, destination);
                    long triangles = indices.Sum(p => (long)p.Count / 3);
                    if (level == 1) middle += triangles; else distant += triangles;
                }
            }
            AssetDatabase.SaveAssets();
            return $"Rebuilt {paths.Length * 2} LOD meshes; triangles {original}/{middle}/{distant}; protected islands {retained}";
        }
    }
}
