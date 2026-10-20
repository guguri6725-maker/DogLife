using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // 여러 연출이 겹쳐도 마지막 연출과 카메라 복귀가 끝난 뒤에만 HUD를 복구합니다.
    public static class BossCinematicUI
    {
        static readonly Dictionary<Behaviour, float> owners = new Dictionary<Behaviour, float>();
        static readonly List<Behaviour> expired = new List<Behaviour>();
        static readonly HashSet<Canvas> hidden = new HashSet<Canvas>();
        static readonly HashSet<Canvas> presentation = new HashSet<Canvas>();
        static bool listening;

        public static bool Hidden
        {
            get
            {
                // 플레이어가 죽으면 재시도 화면을 가리지 않도록 즉시 소유권을 해제합니다.
                if (PlayerDeathSequence.IsActive) owners.Clear();
                expired.Clear();
                foreach (var entry in owners)
                    if (!entry.Key || !entry.Key.isActiveAndEnabled || Time.unscaledTime >= entry.Value)
                        expired.Add(entry.Key);
                foreach (var owner in expired) owners.Remove(owner);
                if (owners.Count == 0) Restore();
                return owners.Count != 0;
            }
        }

        public static void Begin(Behaviour owner)
        {
            if (!owner) return;
            owners[owner] = float.PositiveInfinity;
            // HUD의 Update 이후 연출이 시작되어도 첫 프레임에 조준점이 남지 않게 합니다.
            foreach (var hud in Object.FindObjectsByType<PrototypeHUD>(FindObjectsSortMode.None))
                if (hud.crosshair) hud.crosshair.SetActive(false);
            if (!listening)
            {
                Camera.onPreCull += BeforeCamera;
                Canvas.preWillRenderCanvases += Refresh;
                listening = true;
            }
            Refresh();
        }

        public static void End(Behaviour owner, float returnSeconds = 0)
        {
            if (owner && owners.ContainsKey(owner))
            {
                if (returnSeconds > 0 && owner.isActiveAndEnabled)
                    owners[owner] = Time.unscaledTime + returnSeconds;
                else owners.Remove(owner);
            }
            Refresh();
        }

        // 레터박스는 게임 HUD가 아닌 연출의 일부입니다.
        public static void KeepVisible(Canvas canvas)
        {
            if (!canvas) return;
            presentation.Add(canvas);
            if (hidden.Remove(canvas)) canvas.enabled = true;
        }

        static void BeforeCamera(Camera camera) { if (camera.cameraType == CameraType.Game) Refresh(); }
        static void Refresh()
        {
            if (!Hidden) return;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                // 일시정지/로딩 조작은 계속 가능하며, 늦게 생성되는 HUD도 그리기 전에 숨깁니다.
                bool keep = presentation.Contains(canvas) || canvas.GetComponentInParent<SeasonLoadingScreen>() ||
                    (canvas.GetComponentInParent<GameSettingsWindow>() && GameSettingsWindow.IsOpen);
                if (keep)
                {
                    if (hidden.Remove(canvas)) canvas.enabled = true;
                    continue;
                }
                if (!canvas.enabled) continue;
                hidden.Add(canvas);
                canvas.enabled = false;
            }
        }

        static void Restore()
        {
            foreach (var canvas in hidden) if (canvas) canvas.enabled = true;
            hidden.Clear();
            presentation.Clear();
            if (!listening) return;
            Camera.onPreCull -= BeforeCamera;
            Canvas.preWillRenderCanvases -= Refresh;
            listening = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { owners.Clear(); expired.Clear(); Restore(); }
    }
}
