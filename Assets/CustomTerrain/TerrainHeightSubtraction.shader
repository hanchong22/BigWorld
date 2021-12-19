Shader "Hidden/TerrainEngine/HeightSubtraction" {
    Properties
    {
        _MainTex ("Texture", any) = "" {}       
        _HeightNormal("HeightNormal", int) = 1
    }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;           
          
            int _HeightNormal;
            uniform float4 _MainTex_ST;
            uniform float4 _OldHeightMap_ST;
            uniform float _Height_Offset;
            uniform float _Height_Scale;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;                
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;                
            };

            v2f vert (appdata_t v)
            {
                v2f o;               
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {                
                float4 map = tex2D(_MainTex, i.texcoord);
                return float4(map.x , map.x, map.z, map.w );
            }
            ENDCG

        }
    }
    Fallback Off
}
