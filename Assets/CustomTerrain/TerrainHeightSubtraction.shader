Shader "Hidden/TerrainEngine/HeightSubtraction" {
    Properties
    {
        _MainTex ("Texture", any) = "" {}
        _OldHeightMap ("Old Height Map", 2D) = "Black" {}
    }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_OldHeightMap);
            uniform float4 _MainTex_ST;
            uniform float4 _OldHeightMap_ST;
            uniform float _Height_Offset;
            uniform float _Height_Scale;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 map = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.texcoord);
                float height = saturate(map.x + map.y); //UnpackHeightmap(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.texcoord));
                //float oldHeight = UnpackHeightmap(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_OldHeightMap, i.texcoord));
                height = (height) * _Height_Scale + _Height_Offset ;
                return saturate(height);
            }
            ENDCG

        }
    }
    Fallback Off
}
