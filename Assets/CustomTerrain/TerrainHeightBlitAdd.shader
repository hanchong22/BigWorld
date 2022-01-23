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

            #pragma multi_compile_local _HEIGHT_TYPE _HOLE_TYPE       
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_Tex1);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_Tex2);

            uniform float4 _Tex1_ST;
            uniform float4 _Tex2_ST;
            uniform float _Height_Offset;
            uniform float _Height_Scale;
            uniform float _Target_Height;
            uniform float _Overlay_Layer;

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
                #ifdef _HEIGHT_TYPE
                    float height1 = UnpackHeightmap(map1);
                    float height2 = UnpackHeightmap(map2);

                    if(map2.z == 0)
                    {
                        if(height2 > 0)
                        {
                            height2 =  clamp(height2 * _Height_Scale + _Height_Offset, 0, _Target_Height);
                        }
                        else
                        {
                            height2 =  clamp(height2 * _Height_Scale + _Height_Offset, -_Target_Height, 0);
                        }
                    }

                    if(_Overlay_Layer >= 1 && height2 > 0)               
                    {
                        height1 = 0;
                    }

                    float height = height1 + height2;

                    return PackHeightmap(height);
                #elif _HOLE_TYPE
                    bool isHole1 = map1.x < 0.5f;
                    bool isHole2 = map2.x < 0.5f;
                    bool isHole = isHole1 && isHole2;
                    return float4(isHole?0:1,0,0,0);

                #else
                    return map1 + map2;
                #endif
            }
            ENDCG

        }
    }
    Fallback Off
}
