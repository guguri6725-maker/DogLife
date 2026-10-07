using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    public sealed class InventoryCharacterPreview : MonoBehaviour
    {
        public Transform sourceVisual;
        public PlayerInventory inventory;
        public RawImage image;
        GameObject stage,model,weapon;
        Camera previewCamera;
        RenderTexture texture;
        void OnEnable()
        {
            if(!sourceVisual||!inventory||!image)return;
            stage=new GameObject("InventoryPreviewStage");stage.transform.position=new Vector3(0,-5000,0);
            model=Instantiate(sourceVisual.gameObject,stage.transform);
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,160,0);model.transform.localScale=sourceVisual.lossyScale;
            // The visual clone may already contain the equipped weapon under its hand socket.
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
                if(t.name=="WeaponSocket"||t.name=="LeftWeaponSocket")foreach(Transform child in t){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            var animator=model.GetComponent<Animator>();
            if(animator){animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Play("Base Layer.Locomotion",0,0);animator.Update(0);animator.enabled=false;}
            SetLayer(model);
            var rect=image.rectTransform.rect;
            texture=new RenderTexture(660,Mathf.Max(1,Mathf.RoundToInt(660*rect.height/Mathf.Max(1,rect.width))),24);texture.Create();image.texture=texture;
            var cameraObject=new GameObject("InventoryPreviewCamera");cameraObject.transform.SetParent(stage.transform,false);
            // 투명 배경으로 Hierarchy의 장비 패널과 자연스럽게 이어집니다.
            previewCamera=cameraObject.AddComponent<Camera>();previewCamera.enabled=false;previewCamera.cullingMask=1<<31;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.clear;previewCamera.orthographic=true;previewCamera.targetTexture=texture;
            var bounds=new Bounds(model.transform.position,Vector3.zero);bool first=true;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            previewCamera.orthographicSize=Mathf.Max(.5f,bounds.size.y*.64f);
            cameraObject.transform.position=bounds.center+Vector3.back*5;cameraObject.transform.LookAt(bounds.center);
            var lightObject=new GameObject("InventoryPreviewLight");lightObject.transform.SetParent(stage.transform,false);lightObject.transform.localRotation=Quaternion.Euler(30,-25,0);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.65f;light.cullingMask=1<<31;
            inventory.Changed+=RefreshWeapon;RefreshWeapon();
        }
        static void SetLayer(GameObject root){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;}
        void RefreshWeapon()
        {
            if(weapon){weapon.SetActive(false);Destroy(weapon);}
            var item=inventory.EquippedWeapon;
            if(!model||!item||!item.visualPrefab)return;
            string socketName=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?"LeftWeaponSocket":"WeaponSocket";
            Transform socket=null;foreach(var t in model.GetComponentsInChildren<Transform>())if(t.name==socketName){socket=t;break;}
            if(!socket)return;
            weapon=Instantiate(item.visualPrefab,socket,false);
            WeaponEquipmentVisuals.Decorate(weapon,item);
            foreach(var script in weapon.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var collider in weapon.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            SetLayer(weapon);
        }
        void LateUpdate(){if(previewCamera)previewCamera.Render();}
        void OnDisable()
        {
            if(inventory)inventory.Changed-=RefreshWeapon;
            if(image)image.texture=null;
            if(previewCamera)previewCamera.targetTexture=null;
            if(stage)Destroy(stage);
            if(texture){texture.Release();Destroy(texture);}
        }
    }
}
