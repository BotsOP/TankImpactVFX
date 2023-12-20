Shader "Custom/HexagonShield"
{
    Properties
    {
        _HealthGradient ("Health Gradient", 2D) = "white" {}
        _WireframeColor("Wireframe color", color) = (1.0, 1.0, 1.0, 1.0)
        _Color("Mesh color", color) = (0.5, 0.5, 0.5, 1.0)
        _Emissive("Emissive", float) = 1
        _InsideWidth("Inside Width", Range(0,1)) = 1
        _Padding("Padding", Range(0,1)) = 1
        _Displacement("Displacement", Range(0,0.05)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue"="Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            static const float FLOAT_TO_INT = 1000000;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float2 hexagonStats : TEXCOORD1;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float3 barycentric : TEXCOORD0;
                float2 hexagonStats : TEXCOORD1;
            };

            float easeInOut(float x)
            {
                return x < 0.5 ? 8 * x * x * x * x : 1 - pow(-2 * x + 2, 4) / 2;
            }

            float easeInOutElastic(float x) 
            {
                float c5 = (2.0 * 3.14159265358979323846) / 4.5;

                if (x == 0.0)
                    return 0.0;
                if (x == 1.0)
                    return 1.0;
                if (x < 0.5)
                    return -(pow(2.0, 20.0 * x - 10.0) * sin((20.0 * x - 11.125) * c5)) / 2.0;
                
                return (pow(2.0, -20.0 * x + 10.0) * sin((20.0 * x - 11.125) * c5)) / 2.0 + 1.0;
            }

            struct Vertex
            {
                float3 position;
                float3 normal;
                float4 tangents;
            };

            StructuredBuffer<int3> _HexagonStatsBuffer;
            StructuredBuffer<Vertex> _Vertices;
            
            float _Displacement;
            sampler2D _HealthGradient;
            float4 _HealthGradient_ST;
            
            v2f vert(appdata v)
            {
                v2f o;
               
                o.uv = v.uv;
                
                float2 hexaStatsInterpolate;

                int parentIndex = (int)v.uv.x;
                int2 hexaStats = _HexagonStatsBuffer[parentIndex];
                float alpha = 1 - abs((hexaStats.y / FLOAT_TO_INT) - 1);
                hexaStatsInterpolate.y = saturate(easeInOutElastic(alpha));
                hexaStatsInterpolate.x = saturate(hexaStats.x / FLOAT_TO_INT);
                
                o.hexagonStats = hexaStatsInterpolate;

                float3 newPos = v.vertex + _Vertices[parentIndex].normal * _Displacement * hexaStatsInterpolate.y ;
                o.vertex = UnityObjectToClipPos(newPos);
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2f IN[3], inout TriangleStream<g2f> triStream) {
                g2f o;
                o.pos = IN[0].vertex;
                o.barycentric = float3(IN[0].uv.y, 0.0, 0.0);
                o.hexagonStats = IN[0].hexagonStats;
                triStream.Append(o);
                
                o.pos = IN[1].vertex;
                o.barycentric = float3(0.0, IN[1].uv.y, 0.0);
                o.hexagonStats = IN[1].hexagonStats;
                triStream.Append(o);
                
                o.pos = IN[2].vertex;
                o.barycentric = float3(0.0, 0.0, IN[2].uv.y);
                o.hexagonStats = IN[2].hexagonStats;
                triStream.Append(o);
            }

            fixed4 _WireframeColor;
            fixed4 _Color;
            float _Emissive;
            float _InsideWidth;
            float _Padding;

            fixed4 frag(g2f i) : SV_Target
            {
                float closest = max(i.barycentric.x, max(i.barycentric.y, i.barycentric.z));
                closest = 1 - step(_InsideWidth, (closest + _Padding) % 1);
                float3 wireFrameColor = tex2D(_HealthGradient, float2(i.hexagonStats.x, 0));
                float4 color = lerp(_Color, float4(wireFrameColor, 1), closest);
                color.rgb *= _Emissive;
                color.a *= i.hexagonStats.y * (1 - floor(i.hexagonStats.x + 0.01));
                //color.a = 1;
                return color;
            }
            ENDCG
        }
    }
}
