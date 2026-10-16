using UnityEngine;

namespace DogLife
{
    public sealed partial class KrakenPhaseTwo
    {
        [Header("Motion studio — scene-only, preplaced arms")]
        [SerializeField] KrakenDeepArm[] previewArms;
        [SerializeField] KrakenDeepVfx previewWater;
        [SerializeField] Transform previewSeabedCover;
        Vector3 previewBodyPosition, previewBodyScale;
        Vector3[] previewArmScales;
        bool previewReady;
        const float PreviewWaterDepth = 12;

        // 전투 프리팹의 수치와 동일한 길이·크기·부착점을 사용합니다.
        public void PreparePreview()
        {
            if (previewReady || previewArms == null || previewArms.Length != 4) return;
            if (!bodyVisual) bodyVisual = transform.Find("RiggedBody");
            var skin = bodyVisual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var b = skin.sharedMesh.bounds;
            var bounds = new Bounds(skin.transform.TransformPoint(b.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                bounds.Encapsulate(skin.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z))));
            float fit = Mathf.Min(bodyWidth / bounds.size.x, bodyHeight / bounds.size.y);
            previewBodyScale = bodyVisual.localScale * fit;
            var bottom = bodyVisual.position + (new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - bodyVisual.position) * fit;
            previewBodyPosition = bodyVisual.position + transform.position + Vector3.up * bodyFloatHeight - bottom;
            float reach = Mathf.Max(armLength, Mathf.Sqrt(Mathf.Pow(sweepRadius + armSocketRadius, 2) + Mathf.Pow(PreviewWaterDepth * .72f - armSocketHeight, 2)) + .5f);
            previewArmScales = new Vector3[4];
            for (int i = 0; i < 4; i++) { previewArms[i].PreparePreview(reach); previewArmScales[i] = previewArms[i].transform.localScale; }
            previewReady = true;
        }

        public void ShowPreviewArms(bool visible)
        {
            if (previewWater) previewWater.gameObject.SetActive(false);
            // 2페이즈 미리보기에서는 해저 연결부 가림 식물도 함께 표시합니다.
            if (previewSeabedCover) foreach (var r in previewSeabedCover.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            if (previewArms == null) return;
            foreach (var arm in previewArms) if (arm)
            {
                arm.gameObject.SetActive(visible);
                foreach (var r in arm.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            }
        }

        public void SamplePreviewWater(string pattern, float t, bool visible)
        {
            if (!previewWater) return;
            bool active = visible && pattern != "대기" && pattern != "그로기";
            float w = 1, duration = PreviewLength(pattern);
            Vector3 center = transform.position + Vector3.up * mouthHeight;
            var fx = previewWater;
            fx.transform.SetPositionAndRotation(center, Quaternion.identity);
            fx.Intensity = 1; fx.Impact = false;
            switch (pattern)
            {
                case "협공":
                    w = pincer.warning; active &= t <= w + pincer.attack;
                    fx.Shape = KrakenDeepVfx.Form.Band; fx.transform.position = center + Vector3.forward * Mathf.Min(10, pincerReach);
                    fx.Size = new Vector3(pincerHalfWidth * 2 + pincerContactWidth, pincerHeight, pincerDepth); break;
                case "포격":
                    w = cannon.warning; active &= t <= w + (beamDistance + beamLength) / beamSpeed;
                    fx.Shape = KrakenDeepVfx.Form.Beam;
                    float distance = Mathf.Max(0, t - w) * beamSpeed, back = Mathf.Max(0, distance - beamLength);
                    fx.transform.position = center + Vector3.forward * (mouthForward + back);
                    fx.Size = new Vector3(beamRadius * 2, 0, t < w ? beamDistance : Mathf.Max(0, Mathf.Min(beamDistance, distance) - back)); break;
                case "소용돌이":
                    w = vortex.warning;
                    fx.Shape = t < w + vortex.attack ? KrakenDeepVfx.Form.Vortex : t < w + vortex.attack + pauseBeforeBurst ? KrakenDeepVfx.Form.Orb : KrakenDeepVfx.Form.Burst;
                    fx.Size = new Vector3(fx.Shape == KrakenDeepVfx.Form.Vortex ? suctionRadius : burstRadius, 5, 0);
                    active &= t < duration - vortex.recovery;
                    if (fx.Shape == KrakenDeepVfx.Form.Burst) { fx.Progress = (t - w - vortex.attack - pauseBeforeBurst) / .7f; fx.Intensity = 1 - fx.Progress; }
                    break;
                case "휩쓸기":
                    w = sweep.warning; active &= t <= w + sweep.attack;
                    fx.Shape = KrakenDeepVfx.Form.Sector; fx.transform.position = transform.position + Vector3.up * (PreviewWaterDepth * .28f);
                    fx.Size = new Vector3(sweepRadius, sweepHeight, sweepAngle); break;
                case "심장 노출":
                    float cycle = heartWarning + heartExplosion + heartInterval;
                    active &= t < cycle * 3 && t % cycle < heartWarning + heartExplosion;
                    int layer = new[] { 0, 2, 1 }[Mathf.Min(2, (int)(t / cycle))];
                    fx.Shape = KrakenDeepVfx.Form.Band; fx.Size = new Vector3(30, PreviewWaterDepth / 3, 30);
                    fx.transform.position = transform.position + Vector3.up * (PreviewWaterDepth / 3 * (layer + .5f));
                    t %= cycle; w = heartWarning; break;
                case "사망":
                    fx.Shape = KrakenDeepVfx.Form.Sand; fx.Size = new Vector3(sandRadius, sandHeight * .5f, 0);
                    fx.transform.position = transform.position; fx.Intensity = 1 - t / deathTime; break;
            }
            if (fx.Shape != KrakenDeepVfx.Form.Burst) fx.Progress = Mathf.Clamp01(t / w);
            fx.Impact = t >= w;
            fx.gameObject.SetActive(active);
            fx.GetComponent<Renderer>().enabled = active;
            if (active) fx.SamplePreview(t);
        }

        public float PreviewLength(string pattern)
        {
            switch (pattern)
            {
                case "협공": return pincer.warning + pincer.attack + exposure + pincer.recovery;
                case "포격": return cannon.warning + (beamDistance + beamLength) / beamSpeed + cannon.recovery;
                case "소용돌이": return vortex.warning + vortex.attack + pauseBeforeBurst + .7f + vortex.recovery;
                case "휩쓸기": return sweep.warning + sweep.attack + sweep.recovery;
                case "심장 노출": return 3 * (heartWarning + heartExplosion + heartInterval) + groggyTime;
                case "그로기": return groggyTime;
                case "사망": return deathTime;
                default: return 3;
            }
        }

        // 고정된 전방 표적을 기준으로 인게임 패턴을 재생합니다. 플레이어 추적/피해는 실행하지 않습니다.
        public void SamplePreview(string pattern, float time, out string clip, out float progress)
        {
            clip = "Idle"; progress = time % 1;
            if (!previewReady) return;
            bodyVisual.localScale = previewBodyScale;
            bodyVisual.position = previewBodyPosition;
            Vector3 origin = transform.position, forward = Vector3.forward, side = Vector3.right;
            Vector3 middle = origin + Vector3.up * mouthHeight;
            float warning = 0, active = 0, recover = 0;
            if (pattern == "협공") { warning = pincer.warning; active = pincer.attack + exposure; recover = pincer.recovery; }
            if (pattern == "포격") { warning = cannon.warning; active = (beamDistance + beamLength) / beamSpeed; recover = cannon.recovery; }
            if (pattern == "소용돌이") { warning = vortex.warning; active = vortex.attack + pauseBeforeBurst + .7f; recover = vortex.recovery; }
            if (pattern == "휩쓸기") { warning = sweep.warning; active = sweep.attack; recover = sweep.recovery; }
            float attackTime = Mathf.Max(0, time - warning);
            float recovery = recover > 0 ? Mathf.SmoothStep(0, 1, (time - warning - active) / recover) : 0;
            if (warning > 0)
            {
                clip = time < warning ? (pattern == "포격" ? "Roar" : "Command") : "Wave";
                progress = time < warning ? time / warning : Mathf.Clamp01(attackTime / Mathf.Max(.01f, active));
                if (pattern == "소용돌이" && attackTime < vortex.attack + pauseBeforeBurst) { clip = "Command"; progress = 1; }
            }
            float heartCycle = heartWarning + heartExplosion + heartInterval;
            if (pattern == "심장 노출")
            {
                float q = time % heartCycle;
                clip = time >= heartCycle * 3 ? "Groggy" : q < heartWarning ? "Roar" : "Wave";
                progress = time >= heartCycle * 3 ? (time - heartCycle * 3) / groggyTime : q < heartWarning ? q / heartWarning : Mathf.Clamp01((q - heartWarning) / heartExplosion);
            }
            if (pattern == "그로기" || pattern == "사망") { clip = "Groggy"; progress = time / PreviewLength(pattern); }
            if (pattern == "대기" || pattern == "그로기") bodyVisual.position += Vector3.up * Mathf.Sin(time * .7f) * idleSway;
            if (pattern == "사망") bodyVisual.position -= Vector3.up * Mathf.SmoothStep(0, 1, progress) * Mathf.Max(.65f, Mathf.Abs(bodyFloatHeight));
            if (pattern == "포격") bodyVisual.position -= forward * (time < warning ? .3f * time / warning : .8f * (1 - Mathf.Clamp01(attackTime / active))) * (1 - recovery);

            for (int i = 0; i < 4; i++)
            {
                var arm = previewArms[i]; arm.transform.localScale = previewArmScales[i];
                float angle = i == 0 ? 70 : i == 1 ? -70 : i == 2 ? 140 : -140;
                var dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                var start = origin + Vector3.up * armSocketHeight + dir * armSocketRadius + bodyVisual.position - previewBodyPosition;
                var idle = origin + Vector3.up * (3.2f + Mathf.Sin(time * .7f + i) * .3f) + dir * 10;
                var end = idle;
                var curve = Vector3.up * .3f + Vector3.Cross(dir, Vector3.up) * 2;
                bool flowing = true;
                if (pattern == "협공" && i < 2)
                {
                    float sign = i == 0 ? -1 : 1;
                    var target = middle + forward * Mathf.Min(10, pincerReach);
                    float close = Mathf.SmoothStep(0, 1, attackTime / pincer.attack);
                    end = target + side * sign * Mathf.Lerp(pincerHalfWidth, .3f, close);
                    end = Vector3.Lerp(idle, end, Mathf.SmoothStep(0, 1, time / warning));
                    curve = side * sign * 2 + Vector3.up * Mathf.Sin(Mathf.Clamp01(time / warning) * Mathf.PI) * .4f; flowing = false;
                }
                if (pattern == "휩쓸기" && i == 2)
                {
                    var d = Quaternion.Euler(0, Mathf.Lerp(-sweepAngle * .5f, sweepAngle * .5f, Mathf.SmoothStep(0, 1, attackTime / sweep.attack)), 0) * forward;
                    end = origin + Vector3.up * (PreviewWaterDepth * .28f) + d * sweepRadius;
                    end = Vector3.Lerp(idle, end, Mathf.SmoothStep(0, 1, time / warning));
                    curve = Vector3.Cross(Vector3.up, d); flowing = false;
                }
                if (pattern == "소용돌이" && time >= warning)
                {
                    float a = (warning + Mathf.Min(attackTime, vortex.attack)) * .6f + i * Mathf.PI * .5f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    end = middle + d * 9 + Vector3.up * 2; curve = Vector3.up * 3; flowing = false;
                }
                if (pattern == "심장 노출" && time < heartCycle * 3)
                {
                    int layer = new[] { 0, 2, 1 }[Mathf.Min(2, (int)(time / heartCycle))];
                    float a = i * Mathf.PI * .5f;
                    end = origin + Vector3.up * (PreviewWaterDepth / 3 * (layer + .5f)) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 10;
                    curve = Vector3.up * 2 * Mathf.Sin(Mathf.Clamp01((time % heartCycle) / heartWarning) * Mathf.PI); flowing = false;
                }
                if (pattern == "사망") end -= Vector3.up * burialDepth * Mathf.SmoothStep(0, 1, progress) * .5f;
                arm.SamplePreview(start, Vector3.Lerp(end, idle, recovery), curve, flowing);
            }
        }
    }
}
