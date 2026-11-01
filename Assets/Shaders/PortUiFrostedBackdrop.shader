Shader "Port UI/Frosted Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Tint ("Glass Tint", Color) = (0.70,0.88,0.98,0.38)
        _Radius ("Blur Radius", Range(0,4)) = 1.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        GrabPass { "_PortUiBackground" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float4 grab : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _PortUiBackground;
            float4 _PortUiBackground_TexelSize;
            fixed4 _Color, _Tint;
            float _Radius;
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.grab=ComputeGrabScreenPos(o.vertex);
                o.color=v.color*_Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv=i.grab.xy/i.grab.w;
                float2 d=_PortUiBackground_TexelSize.xy*_Radius;
                float3 c=0;
                [unroll] for(int x=-2;x<=2;x++)
                {
                    float wx=abs(x)==2?1:abs(x)==1?4:6;
                    [unroll] for(int y=-2;y<=2;y++)
                    {
                        float wy=abs(y)==2?1:abs(y)==1?4:6;
                        c+=tex2D(_PortUiBackground,uv+float2(x,y)*d).rgb*(wx*wy);
                    }
                }
                return fixed4(lerp(c/256,_Tint.rgb,_Tint.a)*i.color.rgb,i.color.a);
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
