//
// GPGPU kernels for Stream
//
// Texture format:
// .xyz = particle position
// .w   = particle life
//
Shader "GPU_Particle/Kernel"
{
    Properties
    {
        _MainTex     ("-", 2D)     = ""{}
        _EmitterPos  ("-", Vector) = (0, 0, 20, 0)
        _EmitterSize ("-", Vector) = (40, 40, 40, 0)
        _NoiseParams ("-", Vector) = (0.2, 0.1, 1)  // (frequency, amplitude, animation)
        _Config      ("-", Vector) = (1, 2, 0, 0)   // (throttle, life, random seed, dT)
    }

    CGINCLUDE

    #pragma multi_compile NOISE_OFF NOISE_ON

    #include "UnityCG.cginc"
    #include "SimplexNoise.cginc"

    sampler2D _MainTex;
    sampler2D _pPos;
    sampler2D _pVel;
    sampler2D _pCol;
    sampler2D _pSca;
    sampler2D _colorTable;
    sampler2D _sizeTable;

    float3 _EmitterPos;
    float3 _EmitterSize;

    float4 _NoiseParams;
    float4 _Config;

    // PRNG function.
    float nrand(float2 uv, float salt)
    {
        uv += float2(salt, _Config.z);
        return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
    }

    float4 init_pos(float2 uv) {
        float t = _Time.x;

        // Random position.
        float3 p = float3(nrand(uv, t + 1), nrand(uv, t + 2), nrand(uv, t + 3));
        p = (p - (float3)0.5) * _EmitterSize + _EmitterPos;
        return float4(p, 0);
    }

    float4 init_sca(float2 uv) {
        // Life duration.
        float t = _Time.x;
        float life = _Config.y * (0.5 + nrand(uv, t + 0));
        return float4(life, life, 0.01, 0);
    }

    // 0. Init particle position
    float4 frag_init_pos(v2f_img i) : SV_Target
    {
        return init_pos(i.uv);
    }

    // 1. Init particle velocity
    float4 frag_init_vel(v2f_img i) : SV_Target
    {
        float2 uv = i.uv;
        return float4(0, 0, 0, 0);//as a test
    }

    // 2. Init particle color
    float4 frag_init_col(v2f_img i) : SV_Target
    {
        float2 uv = i.uv;
        return float4(1, 0, 0, 1);//temporary for test
    }

    // 3. Init particle scalers !!Note(x=lefttime,y=lifetime,z=size)
    float4 frag_init_sca(v2f_img i) : SV_Target
    {
        return init_sca(i.uv);
    }

    // Position dependant velocity field.
    float3 get_velocity(float3 pos, float2 uv)
    {
        // Add noise vector.
        pos = (pos) * _NoiseParams.x;
        float4 vel = tex2D(_pVel, uv);
        float3 v = curlNoise(float4(pos, _Time.y * _NoiseParams.z * _NoiseParams.x)) * _NoiseParams.y + vel.xyz;
        return v;
    }

    // Pass 0 Update Position
    float4 frag_update_pos(v2f_img i) : SV_Target
    {
        float4 pos = tex2D(_MainTex, i.uv);
        
        float4 pS = tex2D(_pSca, i.uv);
        if (pS.x > 0)//pos.w > 0
        {
            float dt = _Config.w;
            pos.xyz += get_velocity(pos.xyz, i.uv) * dt; // position
            return pos;
        }
        else
        {
            return init_pos(i.uv);
        }
    }

        float4 frag_update_vel(v2f_img i) : SV_Target
    {

        return tex2D(_pVel, i.uv);
    }

        float4 frag_update_col(v2f_img i) : SV_Target
    {
        float4 pS = tex2D(_pSca, i.uv);
        float lifeProp = 1.0-min(max(0.01,pS.x), pS.y)/ max(0, pS.y);//timeleft/lifetime
        return tex2Dlod(_colorTable, float4(lifeProp, 0, 0, 0));
    }

    float4 frag_update_sca(v2f_img i) : SV_Target
    {
        float dt = _Config.w;
        float4 pS = tex2D(_pSca, i.uv);//x=lefttime, y=lifetime, z=size
        float lifeProp = 1.0 - min(max(0.01, pS.x), pS.y) / max(0, pS.y);//timeleft/lifetime
        pS.z = tex2Dlod(_sizeTable, float4(lifeProp, 0, 0, 0)).x;
        if (pS.x < 0) {
            return init_sca(i.uv);
        }
        pS.x -= dt;
        return pS;
    }

    ENDCG

    SubShader
    {
        // Pass 0: Position
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_init_pos
            ENDCG
        }
        // Pass 1: Velocity
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_init_vel
            ENDCG
        }
        // Pass 2: Color
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_init_col
            ENDCG
        }
        // Pass 3: Scalars
         Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_init_sca
            ENDCG
        }
        // Pass 4: Update Position
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag_update_pos
            ENDCG
        }
        // Pass 0: Update Position
            Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag_update_vel
            ENDCG
        }
            // Pass 0: Update Position
            Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag_update_col
            ENDCG
        }
            // Pass 0: Update Position
            Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag_update_sca
            ENDCG
        }
    }
}
