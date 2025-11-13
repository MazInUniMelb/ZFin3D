Shader "Custom/NeuronInstancedSimple"
{
    Properties
    {
        _Size ("Size", Float) = 0.5
        _InactiveEmission ("Inactive Emission", Float) = 0.1
        _ActiveEmission ("Active Emission", Float) = 5.0
        _GlowThreshold ("Glow Threshold", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            
            #include "UnityCG.cginc"

            struct GPUNeuron
            {
                float4 position;
                float4 originalPosition;
                float4 velocity;
                float4 color;
                float4 activationAndPadding;
            };

            StructuredBuffer<GPUNeuron> _Neurons;
            float _Size;
            float _InactiveEmission;
            float _ActiveEmission;
            float _GlowThreshold;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 color : TEXCOORD1;
                float activation : TEXCOORD2;
            };

            v2f vert (float4 vertex : POSITION, float3 normal : NORMAL, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                GPUNeuron neuron = _Neurons[instanceID];
                
                // Extract activation from activationAndPadding.x
                float activation = neuron.activationAndPadding.x;
                
                float scale = _Size * lerp(0.9, 1.1, activation);
                float3 worldPos = vertex.xyz * scale + neuron.position.xyz;
                
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.normal = normal;
                o.color = neuron.color.xyz;
                o.activation = activation;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float3 lightDir = normalize(float3(1, 1, 1));
                float ndotl = max(0.3, dot(normal, lightDir));
                
                float emissionStrength = _InactiveEmission;
                if (i.activation > _GlowThreshold)
                {
                    float t = (i.activation - _GlowThreshold) / (1.0 - _GlowThreshold);
                    emissionStrength = lerp(_InactiveEmission, _ActiveEmission, t);
                }
                
                float3 litColor = i.color * ndotl;
                float3 emission = i.color * emissionStrength;
                float3 finalColor = litColor + emission;
                
                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}