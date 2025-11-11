Shader "Custom/NeuronInstanced"
{
    Properties
    {
        _DefaultColor("Default Color", Color) = (1,0,0,1)
        _Size ("Neuron Size", Float) = 0.5
        _InactiveEmission ("Inactive Emission", Float) = 0.1
        _ActiveEmission ("Active Emission (HDR)", Float) = 5.0
        _GlowThreshold ("Glow Threshold", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct GPUNeuron
            {
                float3 position;
                float3 velocity;
                float3 color;
                float activation;
            };

            StructuredBuffer<GPUNeuron> _Neurons;
            
            float4 _DefaultColor;
            float _Size;
            float _InactiveEmission;
            float _ActiveEmission;
            float _GlowThreshold;
            float _Smoothness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 color : TEXCOORD2;
                float activation : TEXCOORD3;
            };

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                // Get neuron data
                GPUNeuron neuron = _Neurons[instanceID];
                
                // Scale based on activation
                float scale = _Size * lerp(0.9, 1.1, neuron.activation);
                
                // Transform vertex to world space
                float3 worldPos = v.vertex.xyz * scale + neuron.position;
                o.worldPos = worldPos;
                
                // Transform to clip space
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                
                // Transform normal to world space (for smooth shading)
                o.worldNormal = normalize(v.normal);
                
                o.color = _DefaultColor.rgb; // Fallback color
                // Pass color and activation
                o.color = neuron.color;
                o.activation = neuron.activation;
                
                return o;
            }

            // fixed4 frag (v2f i) : SV_Target
            // {
            //     // Normalize normal
            //     float3 normal = normalize(i.worldNormal);
                
            //     // Simple lighting
            //     float3 lightDir = normalize(float3(1, 1, 1));
            //     float ndotl = max(0.3, dot(normal, lightDir));
                
            //     // Calculate emission
            //     float emissionStrength = _InactiveEmission;
            //     if (i.activation > _GlowThreshold)
            //     {
            //         float t = (i.activation - _GlowThreshold) / (1.0 - _GlowThreshold);
            //         emissionStrength = lerp(_InactiveEmission, _ActiveEmission, t);
            //     }
                
            //     // Combine lighting and emission
            //     float3 litColor = i.color * ndotl;
            //     float3 emission = i.color * emissionStrength;
            //     float3 finalColor = litColor + emission;
                
            //     return fixed4(finalColor, 1.0);
            // }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize the interpolated normal for smooth shading
                float3 normal = normalize(i.worldNormal);
                
                // Calculate view direction
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                
                // Lighting calculation
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                
                // Diffuse lighting (Lambertian)
                float ndotl = max(0, dot(normal, lightDir));
                
                // Specular highlight (Blinn-Phong)
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(max(0, dot(normal, halfDir)), 32.0) * _Smoothness;
                
                // Ambient lighting
                float3 ambient = ShadeSH9(float4(normal, 1.0)) * 0.5;
                
                // Calculate emission based on activation
                float emissionStrength = _InactiveEmission;
                if (i.activation > _GlowThreshold)
                {
                    float t = (i.activation - _GlowThreshold) / (1.0 - _GlowThreshold);
                    emissionStrength = lerp(_InactiveEmission, _ActiveEmission, t);
                }
                
                // Combine all lighting
                float3 diffuse = i.color * _LightColor0.rgb * ndotl;
                float3 specular = _LightColor0.rgb * spec;
                float3 emission = i.color * emissionStrength;
                
                float3 finalColor = ambient + diffuse + specular + emission;
                
                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}