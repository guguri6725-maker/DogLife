using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DogLife.EditorTools
{
    // VARCO 전체 동작의 준비/마무리 구간 대신 안정된 보행 한 주기를 사용합니다.
    public static class PlayerWalkLoopBuilder
    {
        public static string Bake(AnimationClip clip)
        {
            if (clip.length < 3f) return clip.name + ": already a walk cycle";
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
            var rotations = Enumerable.Range(0, bindings.Length)
                .Where(i => bindings[i].propertyName.StartsWith("m_LocalRotation.")).ToArray();
            if (rotations.Length == 0) throw new InvalidOperationException("No bone rotation curves");
            float best = float.PositiveInfinity, start = 0, duration = 0;
            const float step = 1f / 30f;
            // 최소 0.9초로 제한하여 좌/우 발이 바뀌는 반 주기를 선택하지 않습니다.
            for (float a = 1.5f; a < clip.length - 2.4f; a += step)
            for (float length = .9f; length <= 1.5f; length += step)
            {
                float score = 0;
                foreach (int i in rotations)
                {
                    var c = curves[i];
                    float delta = c.Evaluate(a) - c.Evaluate(a + length);
                    float velocity = (c.Evaluate(a + step) - c.Evaluate(a - step))
                        - (c.Evaluate(a + length + step) - c.Evaluate(a + length - step));
                    score += delta * delta + velocity * velocity;
                }
                if (score < best) { best = score; start = a; duration = length; }
            }
            if (duration == 0) throw new InvalidOperationException("No stable walk window");
            int frames = Mathf.RoundToInt(duration * 60);
            for (int i = 0; i < bindings.Length; i++)
            {
                var c = curves[i];
                float drift = c.Evaluate(start + duration) - c.Evaluate(start);
                var values = new float[frames + 1];
                for (int f = 0; f <= frames; f++)
                {
                    float u = (float)f / frames;
                    values[f] = c.Evaluate(start + u * duration) - drift * u * u * (3 - 2 * u);
                }
                values[frames] = values[0];
                var keys = new Keyframe[frames + 1];
                float dt = duration / frames;
                for (int f = 0; f <= frames; f++)
                {
                    float tangent = (values[(f + 1) % frames] - values[(f + frames - 1) % frames]) / (2 * dt);
                    keys[f] = new Keyframe(f * dt, values[f], tangent, tangent);
                }
                AnimationUtility.SetEditorCurve(clip, bindings[i], new AnimationCurve(keys));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = duration;
            settings.loopTime = true; settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip.name + ": " + start.ToString("F3") + "–" + (start + duration).ToString("F3") + "s → " + duration.ToString("F3") + "s loop";
        }
    }
}
