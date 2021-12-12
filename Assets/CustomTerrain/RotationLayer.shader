
Shader "Hidden/TerrainEngine/RotationLayer"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
        _Angle("Angle", Range(-5.0,  5.0)) = 0.0
        _Scale("Scale", Range(0, 3)) = 1
        _Pivot("Pivot", vector) = (0.5,0.5,0,0)        
	}

	SubShader
	{
        Tags{ "RenderType" = "Opaque" }    
        Pass
        {
            Name "Rotation"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
    
            float _Angle;
            float _Scale;
            float _HeightScale;
            half4 _Pivot;
        
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
        
                float2 pivot = _Pivot.xy;        
                float2 offset = _Pivot.zw;
        
                float cosAngle = cos(_Angle);
                float sinAngle = sin(_Angle);
                float2x2 rot = float2x2(cosAngle, -sinAngle, sinAngle, cosAngle);  
        
                float2 uv = v.texcoord.xy / _Scale - pivot + offset;
                o.uv = mul(rot, uv); 
                o.uv += pivot;
                
                return o;
            }
        
            sampler2D _MainTex;
        
            half4 frag(v2f i) : SV_Target
            {
                if(i.uv.x < 0 || i.uv.y < 0 || i.uv.x > 1 || i.uv.y > 1)
                    return half4(0,0,0,0);

                half4 result = tex2D(_MainTex, i.uv);
                return result * half4(1,_HeightScale,1,1);
            }
        
            ENDCG
        }
       
	}
}