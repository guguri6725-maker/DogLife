Shader "DogLife/Original Coastal Waterfall"
{
    Properties
    {
        _MainTex("Original Water Atlas", 2D) = "white" {}
        _Color("Water Tint", Color) = (0.65,0.94,1,0.86)
        _FlowSpeed("Flow Speed", Range(0,2)) = 0.65
        _Opacity("Opacity", Range(0,1)) = 1
        _SurfaceMode("Horizontal surface", Float) = 0
        _WorldTiling("Surface tiling per metre", Float) = .18
        _GateMode("Centre opening gate", Float) = 0
        _OpenProgress("Opening progress", Range(0,1)) = 0
        _FlowTime("Gate flow clock (-1 uses time)", Float) = -1
        _ImpactMode("Soft landing foam", Float) = 0
        _WaterLevel("Landing water height", Float) = 0
        _FloodVisibility("Flood flow visibility", Range(0,1)) = 1
        _FloodLevel("Flood surface level", Float) = -10000
        _FloodFeather("Flood contact softness", Float) = .8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        CGPROGRAM
        #pragma surface surf ConsistentWater alpha:fade noambient noforwardadd
        #pragma target 3.0
        #include "CoastalWaterSurface.cginc"
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}

