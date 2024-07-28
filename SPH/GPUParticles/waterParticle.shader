
Shader "Unlit/waterParticle"
{
	Properties
	{
		[HDR]_Color("Color",color) = (1,1,1,1)
		_MainTex("_MainTex", 2D) = "white" {}
		_Size("Size",float) = 1.6
	}
		SubShader
	{
		Tags { "Queue" = "Transparent+300" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }

		ZTest Off
		Cull Back
		Blend SrcAlpha OneMinusSrcAlpha
		Lighting Off
		ZWrite Off
		Fog { Mode Off }

		LOD 200
		Pass
		{
			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			struct Particle
			{
				float3 position;     //起始位置
				float3 velocity;      //更新位置
			    float density;      //密度
			};
			
			StructuredBuffer<Particle> _Particles;
			// StructuredBuffer<int> _Result;
			
			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			float4 _Color;
			CBUFFER_END
			
			struct appdata {
				float4 positionOS	: POSITION;
				float2 uv			: TEXCOORD0;
			};
			
			struct Varyings {
				float4 positionCS	: SV_POSITION;
				float2 uv			: TEXCOORD0;
			    float4 velocity     : TEXCOORD1;
			};
			
			Varyings vert(appdata v, uint instanceID : SV_InstanceID)
			{
				Varyings o;
				Particle particle = _Particles[instanceID];
				o.positionCS = TransformWorldToHClip(v.positionOS.xyz * 0.2f + particle.position);
				o.uv = v.uv;
			    o.velocity = float4(particle.velocity, particle.density*0.1f);
				return o;
			}
			float4 frag(Varyings i) : SV_Target
			{
			    // return i.velocity;
				// float2 uvMainTex = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
			    // float u = clamp(i.velocity.w - 100, -0.5f, 0.5f);
			    float u = min(length(i.velocity.xyz)*0.05f, 1);
				float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(u+0.1f, 0.5f)).rgb;
				// col = col * _Color;
				return float4(color, 0.2f);
			}
			ENDHLSL
		}

	}
	FallBack Off
}
