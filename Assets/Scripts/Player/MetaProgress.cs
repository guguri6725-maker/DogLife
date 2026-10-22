using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DogLife
{
    // 새 게임/사망과 무관하게 영구히 남는 메타 재화(발자국)와 유물 해금 상태.
    // SeasonRunSave/RunEconomy의 런별 초기화 로직과는 완전히 분리된 저장 키를 씁니다.
    public static class MetaProgress
    {
        public const string Key = "DogLife.MetaProgress.v1";
        [Serializable] public sealed class Data { public int version = 1; public int pawprints; public string[] unlockedRelics = new string[0]; }
        static Data current;
        static Data Current
        {
            get
            {
                if (current != null) return current;
                try { current = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key, "")); } catch { current = null; }
                if (current == null || current.version != 1) current = new Data();
                if (current.unlockedRelics == null) current.unlockedRelics = new string[0];
                return current;
            }
        }
        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); }
        public static int Pawprints => Current.pawprints;
        public static bool IsUnlocked(string relicId) => Array.IndexOf(Current.unlockedRelics, relicId) >= 0;
        public static void AddPawprints(int amount)
        {
            if (amount <= 0) return;
            Current.pawprints += amount; Save();
        }
        public static bool TryUnlock(WeaponRelic relic, out string message)
        {
            if (relic == null) { message = "알 수 없는 유물입니다."; return false; }
            if (IsUnlocked(relic.id)) { message = "이미 해금된 유물입니다."; return false; }
            if (Current.pawprints < relic.unlockCost) { message = "발자국이 부족합니다."; return false; }
            Current.pawprints -= relic.unlockCost;
            var list = new List<string>(Current.unlockedRelics) { relic.id };
            Current.unlockedRelics = list.ToArray();
            Save(); message = relic.displayName + " 해금 완료"; return true;
        }
        public static WeaponRelic[] UnlockedPool() => RelicCatalog.All.Where(r => IsUnlocked(r.id)).ToArray();
        // 전시회 빌드에서 다음 관람객을 위해 쌓인 데이터를 전부 지웁니다.
        public static void ResetAll()
        {
            current = new Data();
            PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        }
    }
}
