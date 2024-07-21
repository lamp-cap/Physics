
Shader "Unlit/testParticle"
{
	Properties
	{
		[HDR]_Color("Color",color) = (1,1,1,1)
		_MainTex("_MainTex", 2D) = "white" {}
		_Size("Size",float) = 1.6
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
		[Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 0
	}
		SubShader
	{
		Tags { "Queue" = "Transparent+300" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }

		ZTest[_ZTest]
		Cull[_Cull]
		Blend One One, SrcAlpha OneMinusSrcAlpha
		Lighting Off
		ZWrite Off
		Fog{ Mode Off }

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
				float3 pos;     //起始位置
				float3 velocity;      //更新位置
			};
			StructuredBuffer<Particle> _ParticleBuffer;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			float _Size;
			float4x4 _GameobjectMatrix;
			
			struct appdata {
				float4 positionOS	: POSITION;
				float2 uv			: TEXCOORD0;
			};
			
			struct Varyings {
				float4 positionCS	: SV_POSITION;
				float2 uv			: TEXCOORD0;
			};
			
			float4x4 GetModelToWorldMatrix(float3 pos)
			{
				float4x4 transformMatrix = float4x4(
						_Size,0,0,pos.x,
						0,_Size,0,pos.y,
						0,0,_Size,pos.z,
						0,0,0,1
				);
				return transformMatrix;
			}

			float3x3 GetRotMatrix_X(float cosQ, float sinQ)
			{
				float3x3 rotMatrix_X = float3x3(
					1, 0, 0,
					0, cosQ, -sinQ,
					0, sinQ, cosQ
					);
				return rotMatrix_X;
			}

			float3x3 GetRotMatrix_Y(float cosQ, float sinQ)
			{
				float3x3 rotMatrix_Y = float3x3(
					cosQ, 0, sinQ,
					0, 1, 0,
					-sinQ, 0, cosQ
					);
				return rotMatrix_Y;
			}

			float3x3 GetRotMatrix_Z(float cosQ, float sinQ)
			{
				float3x3 rotMatrix_Z = float3x3(
					cosQ, -sinQ, 0,
					sinQ, cosQ, 0,
					0, 0, 1
					);
				return rotMatrix_Z;
			}

			Varyings vert(appdata v, uint instanceID : SV_InstanceID)
			{
				Varyings o;
				Particle particle = _ParticleBuffer[instanceID];
				o.positionCS = TransformWorldToHClip(v.positionOS * 0.01f + particle.pos);
				o.uv = v.uv;
				return o;
			}
			float4 frag(Varyings i) : SV_Target
			{
				float2 uvMainTex = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
				float3 col = tex2D(_MainTex, uvMainTex).rgb;
				col = col * _Color;
				return float4(col.rgb, 1);
			}
			ENDHLSL
		}

	}
	FallBack Off
}
