Shader "Hidden/TerrainEngine/HeightBlitAdd" {
    Properties
    {
        _Tex1 ("Tex1", 2D) = "" {}
        _Tex2 ("Tex2", 2D) = "" {}
    }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_Tex1);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_Tex2);
            uniform float4 _Tex1_ST;
            uniform float4 _Tex2_ST;
            uniform float _Height_Offset;
            uniform float _Height_Scale;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;   
                UNITY_VERTEX_INPUT_INSTANCE_ID            
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float4 texcoord : TEXCOORD0;    
                UNITY_VERTEX_OUTPUT_STEREO          
            };

            v2f vert (appdata_t v)
            {
                v2f o;   
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);            
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord.xy = TRANSFORM_TEX(v.texcoord.xy, _Tex1);
                o.texcoord.zw = TRANSFORM_TEX(v.texcoord.xy, _Tex2);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {               
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float4 map1 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Tex1, i.texcoord.xy);
                float4 map2 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Tex2, i.texcoord.zw);
                float height = UnpackHeightmap(map1) + UnpackHeightmap(map2);
                return PackHeightmap(height);
            }
            ENDCG

        }
    }
    Fallback Off
}
