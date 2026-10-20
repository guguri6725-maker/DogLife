using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // 합쳐진 나무 메시도 보스 앞을 가리는 부분만 지웁니다. 충돌과 원본 재질은 변경하지 않습니다.
    public sealed class SpringCinematicFoliage : System.IDisposable
    {
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        readonly SkinnedMeshRenderer[] bodies;
        readonly Transform subject;
        float fade, releaseAt = float.PositiveInfinity;
        static readonly int Fade = Shader.PropertyToID("_CinematicFade");
        static readonly int Rect = Shader.PropertyToID("_CinematicRect");
        static readonly int ViewProjection = Shader.PropertyToID("_CinematicViewProjection");
        static readonly int CameraPosition = Shader.PropertyToID("_CinematicCameraPosition");
        static readonly int Depth = Shader.PropertyToID("_CinematicDepth");

        public SpringCinematicFoliage(Transform actor, Shader shader)
        {
            subject = actor;
            bodies = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (!shader) return;
            var room = actor.GetComponentInParent<ForestRoom>();
            var renderers = room ? room.GetComponentsInChildren<Renderer>(true) : Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                if (!(renderer is MeshRenderer) || renderer.GetComponentInParent<CombatHealth>()) continue;
                var source = renderer.sharedMaterials;
                var replacement = (Material[])source.Clone();
                bool changed = false;
                for (int i = 0; i < source.Length; i++)
                {
                    var material = source[i];
                    if (!IsFoliage(material)) continue;
                    if (!copies.TryGetValue(material, out var copy))
                    {
                        copy = new Material(material) { shader = shader, name = material.name + " (cinematic)", hideFlags = HideFlags.DontSave };
                        copy.SetFloat(Fade, 0);
                        copies.Add(material, copy);
                    }
                    replacement[i] = copy;
                    changed = true;
                }
                if (!changed) continue;
                originals.Add(renderer, source);
                renderer.sharedMaterials = replacement;
            }
        }

        static bool IsFoliage(Material material)
        {
            if (!material || material.shader.name != "Standard") return false;
            string name = material.name;
            return name.StartsWith("Garden3D_Leaf") || name.StartsWith("Garden3D_Bark") ||
                name.StartsWith("Journey_Petal") || name.StartsWith("GardenPollen");
        }

        // 나무로 카메라를 당기지 않고 기존 구도를 유지합니다. 돌벽 판정은 그대로 남깁니다.
        public bool Covers(RaycastHit hit)
        {
            var collider = hit.collider;
            if (!collider) return false;
            var renderer = collider.GetComponentInParent<Renderer>();
            if (!renderer || !originals.TryGetValue(renderer, out var materials)) return false;
            if (collider.GetComponent<CameraStructure>()) return true;
            bool onlyPlants = true;
            foreach (var material in materials) if (!IsFoliage(material)) onlyPlants = false;
            if (onlyPlants) return true;
            // 줄기와 돌이 합쳐진 메시에서는 맞은 삼각형의 재질만 판별합니다.
            var meshCollider = collider as MeshCollider;
            var filter = renderer.GetComponent<MeshFilter>();
            if (!meshCollider || !filter || filter.sharedMesh != meshCollider.sharedMesh || hit.triangleIndex < 0) return false;
            var mesh = filter.sharedMesh;
            long index = (long)hit.triangleIndex * 3;
            for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
            {
                long count = mesh.GetIndexCount(i);
                if (index < count) return IsFoliage(materials[i]);
                index -= count;
            }
            return false;
        }

        public void Release(float returnSeconds) { releaseAt = Time.unscaledTime + returnSeconds; }

        public bool Tick(Camera camera)
        {
            if (!camera || !subject) return false;
            bool releasing = Time.unscaledTime >= releaseAt;
            fade = Mathf.MoveTowards(fade, releasing ? 0 : 1, Time.unscaledDeltaTime / (releasing ? .35f : .25f));
            if (releasing && fade <= 0) return false;
            var bounds = new Bounds(subject.position + Vector3.up * 3, new Vector3(8, 6, 10));
            bool found = false;
            foreach (var body in bodies)
            {
                if (!body || !body.enabled || !body.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = body.bounds; found = true; } else bounds.Encapsulate(body.bounds);
            }
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            float far = 0;
            bool crossesNear = false;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var point = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                        far = Mathf.Max(far, point.z);
                        if (point.z <= camera.nearClipPlane) { crossesNear = true; continue; }
                        min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    }
            if (crossesNear && far > camera.nearClipPlane) { min = Vector2.zero; max = Vector2.one; }
            // 머리·뿔·쓰러진 몸을 둘러싼 화면 영역에 여백을 더해 경계를 부드럽게 합니다.
            min -= Vector2.one * .035f; max += Vector2.one * .035f;
            var rect = new Vector4(min.x, min.y, max.x, max.y);
            var forward = camera.transform.forward;
            foreach (var material in copies.Values)
            {
                material.SetFloat(Fade, far > camera.nearClipPlane ? fade : 0);
                material.SetVector(Rect, rect);
                material.SetMatrix(ViewProjection, camera.projectionMatrix * camera.worldToCameraMatrix);
                material.SetVector(CameraPosition, camera.transform.position);
                material.SetVector(Depth, new Vector4(forward.x, forward.y, forward.z, far));
            }
            return true;
        }

        public void Dispose()
        {
            foreach (var entry in originals) if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            foreach (var material in copies.Values) if (material) Object.Destroy(material);
            originals.Clear(); copies.Clear();
        }
    }
}
