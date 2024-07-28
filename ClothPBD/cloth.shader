Shader "Unlit/cloth"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            StructuredBuffer<float3> _ClothDataBuffer;

            v2f vert (appdata v, uint id : SV_InstanceID)
            {
                v2f o;
                o.vertex = TransformWorldToHClip(v.vertex * 0.05f + _ClothDataBuffer[id]);
                o.uv = v.uv;
                
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return 1;
            }
            ENDHLSL
        }
    }
}
