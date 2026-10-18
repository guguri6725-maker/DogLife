#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class ExpandedStoryArt
    {
        static readonly Dictionary<string,string[]> Shots=new Dictionary<string,string[]>
        {
            {"prologue",new[]{"Expanded/Prologue_Footsteps","Fear","LastNight","Meadow","Expanded/Prologue_Guide","Expanded/Prologue_Promise","Expanded/Prologue_Departure"}},
            {"spring",new[]{"Home","Expanded/Spring_BuriedBones","Expanded/Spring_Boar","Expanded/Spring_Onward"}},
            {"summer",new[]{"Expanded/Summer_SubmergedMemory","Expanded/Summer_FearRecognition","Expanded/Summer_Kraken","Expanded/Summer_Resistance","Expanded/Summer_Question"}},
            {"autumn",new[]{"Expanded/Autumn_MemoryPath","Expanded/Autumn_WaitingSoul","Expanded/Autumn_LovedSoul","Expanded/Autumn_Reaper","Expanded/Autumn_LastNight","Expanded/Autumn_LettingGo"}},
            {"winter",new[]{"Expanded/Winter_LonelyPath","Expanded/Winter_Conflicted","Expanded/Winter_Knight","Expanded/Winter_FinalStand","WinterDoor"}},
            {"life",new[]{"Expanded/Ending_Reunion","Expanded/Life_FirstStep","Life"}},
            {"farewell",new[]{"Expanded/Ending_Reunion","Expanded/Farewell_LastScent","Farewell"}},
            {"revenge",new[]{"Expanded/Ending_Reunion","Expanded/Revenge_Anger","Revenge"}}
        };
        public static string ArtworkPath(string image)
        {
            string name=image.Substring(image.LastIndexOf('/')+1);
            string group=new[]{"Prologue","Spring","Summer","Autumn","Winter"}.FirstOrDefault(prefix=>name.StartsWith(prefix+"_"))??"Shared";
            return "Assets/Textures/Story/"+group+"/"+name+".png";
        }
        public static string Apply(StoryLibrary library)
        {
            // Resolve everything first; never leave the story with a half-applied art set.
            var textures=Shots.Values.SelectMany(v=>v).Distinct().ToDictionary(s=>s,s=>AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkPath(s)));
            foreach(var p in textures)if(!p.Value)throw new InvalidOperationException("Missing story illustration: "+p.Key);
            foreach(var c in library.chapters)if(!Shots.ContainsKey(c.id)||Shots[c.id].Length!=c.pages.Length)throw new InvalidOperationException("Story layout changed: "+c.id);
            foreach(var c in library.chapters)for(int i=0;i<c.pages.Length;i++)
            {
                string shot=Shots[c.id][i];var p=c.pages[i];p.artwork=textures[shot];p.focus=new Vector2(.5f,.52f);
                bool intimate=shot.Contains("Conflicted")||shot.Contains("Recognition")||shot.Contains("LastNight")||shot.Contains("LastScent")||shot.Contains("Footsteps");
                bool confrontation=shot.Contains("Boar")||shot.Contains("Kraken")||shot.Contains("Reaper")||shot.Contains("Knight");
                p.zoomOverride=intimate?.055f:confrontation?.025f:.04f;
                p.motionSeconds=intimate?22:confrontation?15:18;
                p.narrationDelayOverride=confrontation?2.1f:intimate?1.8f:1.4f;
            }
            EditorUtility.SetDirty(library);
            return library.chapters.Sum(c=>c.pages.Length)+" pages / "+textures.Count+" distinct illustrations";
        }
        public static string ApplySaved()
        {
            var library=AssetDatabase.LoadAssetAtPath<StoryLibrary>("Assets/Resources/Story/StoryLibrary.asset");
            var result=Apply(library);AssetDatabase.SaveAssets();return result;
        }
    }
}
#endif
