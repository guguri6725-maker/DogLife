using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // 실제 씬의 X/Z 좌표로 미리 배치한 지도와 플레이어 표식을 연결합니다.
    public sealed class SeasonMapLayout : MonoBehaviour
    {
        public string seasonTitle, seasonCode;
        public Color accent = Color.white;
        public Vector2 worldOrigin;
        public float mapScale = 1;
        public Image[] rooms, passages;
        public Text[] roomLabels;
        public RectTransform playerMarker;
        public void Refresh(ForestRoomStreaming stream)
        {
            int passage = stream.MapPassageIndex;
            for (int i = 0; i < rooms.Length; i++)
            {
                bool current = passage < 0 && stream.CurrentRoom == i;
                Color tint = current ? accent : stream.WasCleared(i) ? new Color(.73f, .86f, .61f, .9f) : new Color(.72f, .67f, .58f, .72f);
                rooms[i].color = tint;
                roomLabels[i].color = tint;
            }
            for (int i = 0; i < passages.Length; i++)
                passages[i].color = passage == i ? accent : new Color(.7f, .64f, .5f, .8f);
            var motor = stream.director.hud.motor;
            Vector3 pos = motor.transform.position;
            playerMarker.anchoredPosition = (new Vector2(pos.x, pos.z) - worldOrigin) * mapScale;
            // 강아지 얼굴은 항상 똑바로 유지해 작은 지도에서도 알아보기 쉽게 합니다.
            playerMarker.localRotation = Quaternion.identity;
        }
    }
}
