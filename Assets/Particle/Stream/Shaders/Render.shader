Shader "GPU_Particle/Render"
{
    Properties
    {
        _MainTex("Particle Sprite", 2D) = "white" {}
        _SizeMul("Size Multiplier", Float) = 1
    }

	CGINCLUDE
#include "UnityCG.cginc"

	uniform sampler2D _MainTex;

	sampler2D _pPos;//particle position
	float4 _pPos_TexelSize;
	sampler2D _pVel;//velocity
	sampler2D _pCol;//colors
	sampler2D _pSca;//scalars x=lefttime,y=lifetime,z=size;

	float _offset;

	float _SizeMul;

	half4 _Color;
	half _Tail;

	struct appdata
	{
		float4 position : POSITION;
		float2 texcoord : TEXCOORD0;
	};

	struct v2f
	{
		float4 pos : POSITION;
		float2 uv : TEXCOORD0;
		float4 col : COLOR;
	};

	v2f vert(appdata v)
	{
		v2f o;

		//float3 q = quad[id];
		/*int id = v.position.x;
		float3 q = float3(-0.5, 0.5, 0);
		
		if (id % 4 == 1) {
			q = float3(0.5f, 0.5f, 0);
		}
		else if (id % 4 == 2) {
			q = float3(0.5f, -0.5f, 0);
		}
		else if (id % 4 == 3) {
			q = float3(-0.5f, -0.5f, 0);
		}*/
		int id = v.position.x;
		float3 q = float3(0, 0, 0);//when -1
		if (id == 0) {
			q = float3(-0.5, -0.5, 0);
		}
		else if (id == 1) {
			q = float3(0.5f, -0.5f, 0);
		}
		else if (id == 2) {
			q = float3(0.5f, 0.5f, 0);
		}
		else if (id == 3) {
			q = float3(-0.5f, 0.5f, 0);
		}

		//float y = floor(inst / _pPos_TexelSize.z);
		//float2 uv = float2(frac(inst / _pPos_TexelSize.z), y / _pPos_TexelSize.w);
		float2 uv = v.texcoord.xy + _pPos_TexelSize.xy / 2 + float2(0, _offset);
		

		float4 pP = tex2Dlod(_pPos, float4(uv, 0, 0));
		float4 pC = tex2Dlod(_pCol, float4(uv, 0, 0));
		float4 pS = tex2Dlod(_pSca, float4(uv, 0, 0));

		//o.pos = UnityObjectToClipPos(q+float4(0, 0, 0, 1));
		//o.pos = UnityObjectToClipPos(float4(q.xyz +p1.xyz, 1));

		o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_V, float4(pP.xyz, 1.0)) + float4(q, 0.0) * pS.z * _SizeMul);//

		o.uv = q + 0.5f;

		o.col = pC;
		return o;
	}

	fixed4 frag(v2f i) : COLOR
	{
		return tex2D(_MainTex, i.uv) * i.col * i.col.a;//tex2D(_MainTex, i.uv) * i.col * i.col.a//tex2D(_MainTex, i.uv) * i.col * i.col.a
	}

	ENDCG

    SubShader
    {
        Pass
        {
			Cull Off
			Lighting Off
			Zwrite Off//Not compartible with SKYBOX!!!!
			//Test LEqual

        //Blend SrcAlpha OneMinusSrcAlpha
        //Blend One OneMinusSrcAlpha
        Blend One One
        //Blend OneMinusDstColor One

        LOD 200

        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        CGPROGRAM

        #pragma target 3.0
        #pragma vertex vert
        #pragma fragment frag
		ENDCG
	}
	}
}