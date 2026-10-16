using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DogLife.EditorTools
{
    // 원본의 관절 길이와 중립 자세를 보존하고 회전 키만 다시 작성합니다.
    public static class CoralMermaidMotionRefinement
    {
        const string Folder = "Assets/Animations/Monster/Summer/CoralMermaid/";
        public static string Build(string only = null)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop preview before authoring clips.");
            var neutral = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Monster/Summer/CoralMermaid/Neutral.anim");
            var bindings = AnimationUtility.GetCurveBindings(neutral);
            var rest = bindings.ToDictionary(b => b.path + ":" + b.propertyName, b => AnimationUtility.GetEditorCurve(neutral, b).Evaluate(0));
            var paths = bindings.Where(b => b.propertyName == "m_LocalRotation.w").Select(b => b.path).ToArray();
            foreach (string state in new[] { "Idle", "Walk", "Dive", "Burrow", "AttackReady", "Emerge", "Attack", "Recovery" })
            {
                if (only != null && !only.Split('|').Contains(state)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + state + ".anim");
                float duration = clip.length;
                // 길이와 GUID를 유지하여 기존 Animator의 전투 타이밍을 보존합니다.
                clip.ClearCurves();
                foreach (var binding in bindings.Where(b => !b.propertyName.StartsWith("m_LocalRotation")))
                {
                    float value = rest[binding.path + ":" + binding.propertyName];
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, duration, value));
                }
                int frames = Mathf.CeilToInt(duration * 30);
                var rotations = paths.ToDictionary(path => path, path => new Quaternion(rest[path + ":m_LocalRotation.x"], rest[path + ":m_LocalRotation.y"], rest[path + ":m_LocalRotation.z"], rest[path + ":m_LocalRotation.w"]));
                var allCurves = paths.ToDictionary(path => path, path => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() });
                for (int frame = 0; frame <= frames; frame++)
                {
                    float t = frame / (float)frames;
                    var pose = paths.ToDictionary(path => path, path => rotations[path] * Quaternion.Euler(Offset(state, path.Split('/').Last(), t)));
                    // 손 목표와 팔꿈치 방향을 별도로 잡아 준비/타격 실루엣을 분명히 합니다.
                    PoseArms(state, t, pose, rest);
                    foreach (string path in paths)
                    {
                        for (int axis = 0; axis < 4; axis++) allCurves[path][axis].AddKey(t * duration, pose[path][axis]);
                    }
                }
                foreach (string path in paths)
                {
                    var curves = allCurves[path];
                    for (int axis = 0; axis < 4; axis++)
                    {
                        for (int key = 0; key < curves[axis].length; key++) curves[axis].SmoothTangents(key, 0);
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]), curves[axis]);
                    }
                }
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = state == "Idle" || state == "Walk" || state == "Burrow";
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
            AssetDatabase.SaveAssets();
            return "Mermaid clips refined; original lengths and bindings retained: " + (only ?? "all eight");
        }

        static float Ease(float t, float from, float to) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        static Matrix4x4 World(string path, Dictionary<string, Quaternion> pose, Dictionary<string, float> rest)
        {
            if (string.IsNullOrEmpty(path) || !pose.ContainsKey(path)) return Matrix4x4.identity;
            Vector3 Read(string field) => new Vector3(rest[path + ":" + field + ".x"], rest[path + ":" + field + ".y"], rest[path + ":" + field + ".z"]);
            int slash = path.LastIndexOf('/');
            return World(slash < 0 ? "" : path.Substring(0, slash), pose, rest) * Matrix4x4.TRS(Read("m_LocalPosition"), pose[path], Read("m_LocalScale"));
        }
        static void PoseArms(string state, float t, Dictionary<string, Quaternion> pose, Dictionary<string, float> rest)
        {
            float coil = state == "AttackReady" ? Ease(t, 0, .72f) : state == "Emerge" || state == "Attack" ? 1 - Ease(t, 0, .30f) : 0;
            float strike = state == "Emerge" || state == "Attack" ? Ease(t, .12f, .34f) * (1 - Ease(t, .65f, 1)) : 0;
            float weight = Mathf.Clamp01(coil + strike);
            float streamline = state == "Dive" ? Ease(t, .04f, .32f) : state == "Burrow" ? 1 : state == "AttackReady" ? 1 - Ease(t, 0, .5f) : 0;
            weight = Mathf.Max(weight, streamline);
            if (weight <= 0) return;
            string chestPath = pose.Keys.First(k => k.EndsWith("/Chest"));
            Vector3 chest = World(chestPath, pose, rest).MultiplyPoint3x4(Vector3.zero);
            // 숙인 머리의 현재 높이가 아닌 중립 신장을 사용해야 팔 목표가 줄어들지 않습니다.
            var neutralPose = pose.Keys.ToDictionary(k => k, k => new Quaternion(rest[k + ":m_LocalRotation.x"], rest[k + ":m_LocalRotation.y"], rest[k + ":m_LocalRotation.z"], rest[k + ":m_LocalRotation.w"]));
            float unit = World(pose.Keys.First(k => k.EndsWith("/Head")), neutralPose, rest).MultiplyPoint3x4(Vector3.zero).y;
            foreach (string side in new[] { "L", "R" })
            {
                string upper = pose.Keys.First(k => k.EndsWith("/" + side + "UpperArm"));
                string fore = pose.Keys.First(k => k.EndsWith("/" + side + "Forearm"));
                string hand = pose.Keys.First(k => k.EndsWith("/" + side + "Hand"));
                Vector3 a = World(upper, pose, rest).MultiplyPoint3x4(Vector3.zero), b = World(fore, pose, rest).MultiplyPoint3x4(Vector3.zero), c = World(hand, pose, rest).MultiplyPoint3x4(Vector3.zero);
                float sign = Mathf.Sign(a.x), extend = strike / Mathf.Max(.001f, coil + strike);
                Vector3 target = chest + new Vector3(sign * Mathf.Lerp(.065f, .34f, extend), Mathf.Lerp(-.035f, -.015f, extend), .14f) * unit;
                // 입수는 팔을 앞으로 뻗지 않고 중력 방향으로 편하게 내립니다.
                var torso = World(chestPath, pose, rest).rotation;
                Vector3 diveTarget = chest + torso * new Vector3(sign * .035f, .42f, .08f) * unit;
                if (state == "Dive") diveTarget = chest + new Vector3(sign * .12f, -.29f, .065f) * unit;
                target = Vector3.Lerp(target, diveTarget, streamline);
                target = Vector3.Lerp(c, target, weight);
                Vector3 pole = chest + new Vector3(sign * .23f, -.16f, .025f) * unit;
                pole = Vector3.Lerp(pole, chest + torso * new Vector3(sign * .19f, .14f, .12f) * unit, streamline);
                if (state == "Dive") pole = chest + new Vector3(sign * .20f, -.13f, .035f) * unit;
                float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c);
                Vector3 direction = (target - a).normalized;
                float distance = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(l1 - l2) + .0001f, (l1 + l2) * .995f);
                target = a + direction * distance;
                float along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
                Vector3 bend = Vector3.ProjectOnPlane(pole - a, direction).normalized;
                Vector3 elbow = a + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
                string parent = upper.Substring(0, upper.LastIndexOf('/'));
                pose[upper] = Quaternion.Inverse(World(parent, pose, rest).rotation) * Quaternion.FromToRotation(b - a, elbow - a) * World(upper, pose, rest).rotation;
                b = World(fore, pose, rest).MultiplyPoint3x4(Vector3.zero); c = World(hand, pose, rest).MultiplyPoint3x4(Vector3.zero);
                pose[fore] = Quaternion.Inverse(World(upper, pose, rest).rotation) * Quaternion.FromToRotation(c - b, target - b) * World(fore, pose, rest).rotation;
            }
        }
        static Vector3 Offset(string state, string bone, float t)
        {
            float cycle = t * Mathf.PI * 2;
            bool swim = state == "Walk" || state == "Burrow";
            bool loop = swim || state == "Idle";
            float envelope = loop ? 1 : Mathf.Sin(t * Mathf.PI);
            float coil = state == "AttackReady" ? Ease(t, 0, .72f) : state == "Emerge" || state == "Attack" ? 1 - Ease(t, 0, .3f) : 0;
            float strike = state == "Emerge" || state == "Attack" ? Ease(t, .12f, .34f) * (1 - Ease(t, .65f, 1)) : 0;
            float dive = state == "Dive" ? Ease(t, .12f, .75f) : 0;
            // 입수 전 짧게 모았다가 머리·몸통·꼬리 순서로 펴서 진입합니다.
            float anticipate = state == "Dive" ? Mathf.Sin(Mathf.Clamp01(t / .23f) * Mathf.PI) : 0;
            float settle = state == "Recovery" ? Mathf.Sin(t * Mathf.PI) * (1 - t) : 0;
            // 허리를 먼저 숙이고 골반은 작게 따라가며 꼬리를 아래에 남깁니다.
            if (state == "Dive")
            {
                float bow = Ease(t, .08f, .58f);
                float pelvis = Ease(t, .38f, 1);
                if (bone == "Hips") return new Vector3(24 * pelvis, 0, 0);
                if (bone == "Spine") return new Vector3(42 * bow, 0, 0);
                if (bone == "Chest") return new Vector3(13 * Ease(t, .14f, .62f), 0, 0);
                if (bone == "Neck") return new Vector3(5 * bow, 0, 0);
                if (bone == "Head") return new Vector3(3 * bow, 0, 0);
            }
            float follow = state == "Dive" ? Ease(t, .32f, .92f) : state == "Burrow" ? 1 : state == "AttackReady" ? 1 - Ease(t, 0, 1) : 0;
            float fold = state == "Dive" ? Ease(t, .12f, .4f) * (1 - Ease(t, .55f, 1)) : 0;
            if (bone == "Hips" && (state == "Dive" || state == "Burrow" || state == "AttackReady"))
                return new Vector3(72 * follow + 8 * coil, 0, 0);
            if (bone == "Spine" && (state == "Dive" || state == "Burrow" || state == "AttackReady"))
                return new Vector3(8 * follow + 36 * fold + 6 * coil - 7 * anticipate, 0, 0);
            if (bone == "Chest" && (state == "Dive" || state == "Burrow" || state == "AttackReady"))
                return new Vector3(4 * follow + 24 * fold - 4 * coil, 0, 0);
            if (bone == "Hips") return new Vector3((swim ? 12 : 0) + 8 * coil - 9 * strike + 10 * dive, 0, (swim ? 2 : 1) * Mathf.Sin(cycle) * envelope);
            if (bone == "Spine") return new Vector3(6 * coil - 13 * strike + 9 * dive + 6 * settle - 7 * anticipate, 0, -1.5f * Mathf.Sin(cycle) * envelope);
            if (bone == "Chest") return new Vector3(-4 * coil - 8 * strike + 7 * dive + 4 * settle, 3 * Mathf.Sin(cycle) * envelope, 0);
            if (bone == "Neck" || bone == "Head") return new Vector3(-3 * coil + 4 * strike - 4 * dive, -1.5f * Mathf.Sin(cycle) * envelope, 0);
            if (bone.StartsWith("Tail") && int.TryParse(bone.Substring(4), out int segment))
            {
                // 8개의 꼬리 관절에 위상 차이를 주어 끝으로 흐르는 파동을 만듭니다.
                float amplitude = state == "Idle" ? 2.2f : swim ? (state == "Burrow" ? 11 : 7) : 4;
                float wave = Mathf.Sin(cycle - segment * .62f) * amplitude * envelope;
                float tailDive = state == "Dive" ? Ease(t, .23f + segment * .045f, .65f + segment * .045f) : 0;
                float tailStrike = state == "Emerge" ? Ease(t, .22f + segment * .02f, .44f + segment * .02f) * (1 - Ease(t, .75f, 1)) : strike;
                float curl = state == "Dive" ? 2.5f * Mathf.Sin(Mathf.PI * Ease(t, .4f + segment * .035f, .72f + segment * .035f)) : 0;
                if (state == "Dive") wave *= .25f;
                return new Vector3(wave + coil * (segment < 4 ? 5 : -5) - tailStrike * 4 + curl + settle * 5, 0, Mathf.Sin(cycle - segment * .62f + 1) * amplitude * .25f * envelope);
            }
            float side = bone.StartsWith("L") || bone.Contains("L") ? 1 : -1;
            if (bone.EndsWith("UpperArm")) return new Vector3(-12 * coil + 28 * strike - 18 * dive, side * 5 * coil, side * (-7 * coil - 18 * strike + (swim ? 5 : 2) * Mathf.Sin(cycle) * envelope));
            if (bone.EndsWith("Forearm")) return new Vector3(-8 * coil + 10 * strike, 0, side * (24 * coil - 10 * strike + 8 * dive));
            if (bone.EndsWith("Hand")) return new Vector3(6 * coil - 8 * strike, 0, side * 3 * Mathf.Sin(cycle - .5f) * envelope);
            if (bone.StartsWith("Hair")) return new Vector3(4 * Mathf.Sin(cycle - .8f) * envelope + 5 * strike, 0, side * 3 * Mathf.Sin(cycle - 1.1f) * envelope);
            if (bone.StartsWith("Fin")) return new Vector3((swim ? 8 : 3) * Mathf.Sin(cycle - 4.8f) * envelope, 0, side * (3 * Mathf.Sin(cycle - 5.1f) * envelope + 7 * strike));
            return Vector3.zero;
        }
    }
}
