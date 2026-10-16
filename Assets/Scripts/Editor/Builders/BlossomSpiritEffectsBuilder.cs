using System.Linq;
using UnityEngine;
using UnityEditor;

namespace DogLife.EditorTools
{
    public static class BlossomSpiritEffectsBuilder
    {
        const string Models="Assets/Models/Monster/Spring/BlossomSpirit/";
        const string Materials="Assets/Materials/Monster/Spring/BlossomSpirit/";
        static ParticleSystem Petals(Transform parent,string name,Material material,Mesh mesh,float rate,float size)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var scale=parent.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=1;main.loop=true;main.playOnAwake=false;main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(.05f,.25f);
            main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.maxParticles=40;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1,.45f,.65f),new Color(1,.85f,.92f));
            var emission=ps.emission;emission.rateOverTime=rate;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.08f;
            var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-5,5);
            var sizeOver=ps.sizeOverLifetime;sizeOver.enabled=true;sizeOver.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var r=ps.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Mesh;r.enableGPUInstancing=false;r.mesh=mesh;r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            return ps;
        }
        public static void Attach(GameObject root)
        {
            var petal=AssetDatabase.LoadAssetAtPath<Mesh>(Models+"EnergyPetal.asset");
            if(!petal)
            {
                petal=new Mesh{name="EnergyPetal"};
                petal.vertices=new[]{new Vector3(0,-.5f,0),new Vector3(.35f,0,0),new Vector3(.25f,.5f,0),new Vector3(0,.33f,0),new Vector3(-.25f,.5f,0),new Vector3(-.35f,0,0),Vector3.zero};
                petal.triangles=new[]{6,0,1,6,1,2,6,2,3,6,3,4,6,4,5,6,5,0};petal.colors=Enumerable.Repeat(Color.white,7).ToArray();petal.RecalculateBounds();petal.RecalculateNormals();AssetDatabase.CreateAsset(petal,Models+"EnergyPetal.asset");
            }
            var energy=AssetDatabase.LoadAssetAtPath<Material>(Materials+"PetalEnergy.mat");
            if(!energy){energy=new Material(Shader.Find("DogLife/Blossom Petal Energy")){name="PetalEnergy",color=new Color(1,.38f,.65f)};AssetDatabase.CreateAsset(energy,Materials+"PetalEnergy.mat");}
            var shot=new GameObject("BlossomPetalProjectile");
            try
            {
                var cp=shot.AddComponent<CombatProjectile>();cp.hostile=true;cp.speed=5;cp.damage=4;cp.range=12;cp.radius=.13f;
                var ps=Petals(shot.transform,"PetalStream",energy,petal,45,.2f);var main=ps.main;main.playOnAwake=true;
                var trail=shot.AddComponent<TrailRenderer>();trail.sharedMaterial=energy;trail.time=.22f;trail.startWidth=.14f;trail.endWidth=0;trail.minVertexDistance=.04f;
                trail.startColor=new Color(1,.8f,.9f,.8f);trail.endColor=new Color(1,.2f,.5f,0);trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                for(int i=0;i<5;i++)
                {
                    var core=new GameObject("PetalCore"+i);core.transform.SetParent(shot.transform,false);core.transform.localRotation=Quaternion.Euler(i*31,i*53,i*137);core.transform.localScale=Vector3.one*.22f;
                    core.AddComponent<MeshFilter>().sharedMesh=petal;core.AddComponent<MeshRenderer>().sharedMaterial=energy;
                }
                PrefabUtility.SaveAsPrefabAsset(shot,"Assets/Prefabs/Monster/Spring/Shared/BlossomPetalProjectile.prefab");
            }finally{Object.DestroyImmediate(shot);}
            var enemy=root.GetComponent<BlossomSpiritEnemy>();var model=enemy.modelAnimator;
            enemy.leftHand=model.GetComponentsInChildren<Transform>().First(x=>x.name=="LeftHand");enemy.rightHand=model.GetComponentsInChildren<Transform>().First(x=>x.name=="RightHand");
            enemy.leftCharge=Petals(enemy.leftHand,"GatherPetals_L",energy,petal,35,.13f);enemy.rightCharge=Petals(enemy.rightHand,"GatherPetals_R",energy,petal,35,.13f);
            enemy.projectilePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monster/Spring/Shared/BlossomPetalProjectile.prefab");root.GetComponent<CombatHealth>().maxHealth=22;
        }
    }
}
