#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // Explicit review only; never attached to a production prefab.
    public sealed class MemoryPresentationReview:MonoBehaviour
    {
        readonly Dictionary<string,int> backup=new Dictionary<string,int>();
        readonly List<string> report=new List<string>();
        void Check(string name,bool good){report.Add((good?"PASS ":"FAIL ")+name);}
        IEnumerator Start()
        {
            var book=StoryBook.Instance;Directory.CreateDirectory("Builds/MemoryReview");
            foreach(var c in book.Library.chapters){string k="DogLife.Memories.v1."+c.id;backup[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetInt(k):-1;}
            foreach(var c in book.Library.chapters)
            {
                int callbacks=0;
                Check(c.id+" opens",book.Play(c.id,()=>callbacks++,true));
                yield return null;
                book.NextPage();Check(c.id+" first input reveals narration without skipping",book.CurrentPage==0);
                yield return null;
                for(int i=0;i<c.pages.Length;i++)
                {
                    var field=typeof(StoryBook).GetField("pageStarted",BindingFlags.Instance|BindingFlags.NonPublic);
                    field.SetValue(book,Time.unscaledTime-100);
                    yield return null;
                    var text=book.GetComponentsInChildren<Text>().Single(t=>t.name=="Narration");
                    Check(c.id+" page "+i+" full narration",text.text==c.pages[i].text);
                    Check(c.id+" page "+i+" illustration",book.GetComponentsInChildren<RawImage>().Single(t=>t.name=="Illustration").texture==c.pages[i].artwork);
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Builds/MemoryReview/"+c.id+"_"+i+".png");yield return null;
                    if(i+1<c.pages.Length)book.NextPage();
                }
                book.Finish();book.Finish();Check(c.id+" waits for closing fade",StoryBook.IsOpen&&callbacks==0);
                yield return new WaitForSecondsRealtime(book.Library.closingFade+.15f);
                Check(c.id+" callback exactly once after fade",!StoryBook.IsOpen&&callbacks==1&&Time.timeScale>0);
            }
            book.OpenGallery();book.Play("spring");yield return null;book.Finish();
            yield return new WaitForSecondsRealtime(book.Library.closingFade+.15f);
            Check("Replay returns to gallery",book.GalleryOpen);book.Finish();
            Check("Gallery closes",!StoryBook.IsOpen);
            Restore();File.WriteAllLines("Builds/MemoryReview/checks.txt",report);
            Debug.Log("Memory review: "+report.Count+" checks, "+report.Count(s=>s.StartsWith("FAIL"))+" failures");
        }
        void Restore(){foreach(var p in backup){if(p.Value<0)PlayerPrefs.DeleteKey(p.Key);else PlayerPrefs.SetInt(p.Key,p.Value);}PlayerPrefs.Save();backup.Clear();}
        void OnDestroy(){Restore();}
    }
}
#endif
