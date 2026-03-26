Shader "Custom/PointCloud"
{
    Properties
    {
        [HideInInspector] _PclSize ("Point Cloud Size", Vector) = (4, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct appdata
            {
                // vertex defines the coordinates of a unit square, and its purpose is to specify
                // the relative offset of each vertex within the billboard. It needs to work together
                // with the billboard’s size, since the actual billboard size depends on whether this
                // offset calculation is done in local space or in clip space.
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct VS_output
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color: COLOR;
            };

            // Instance data
            struct InstanceData
            {
                float3 relativePosition;
                uint rgba;
            };
            StructuredBuffer<InstanceData> _InstanceBuffer;

            float4x4 _LocalToWorld;
            float4 _PclSize;

            float4 UnpackColor(uint packedColor) {
                float a = ((packedColor >> 24) & 0x000000FF) / 255.0;
                float b = ((packedColor >> 16) & 0x000000FF) / 255.0;
                float g = ((packedColor >> 8) & 0x000000FF) / 255.0;
                float r = (packedColor & 0x000000FF) / 255.0;
                return float4(r, g, b, a);
            }

            VS_output vert(appdata v)
            {
                InitIndirectDrawArgs(0);

                VS_output o;

                InstanceData instanceData = _InstanceBuffer[v.instanceID];

                // This is a combined parameter related to the size of the point.
                // The first component {x} defines the size type (fixed=0, splat=1).
                // The second component {y} defines the size space (screenSpace=0, worldSpace=1).
                // The last two components {zw} defines the size of points (sizeX, sizeY).
                // The last two "zw" represent sizes in screen space in pixels, while in world space they are in meters.
                bool useWorldSpace = _PclSize.y > 0.5;
                if (useWorldSpace)
                {
                    // World space
                    float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                    float3 cameraUp    = UNITY_MATRIX_V[1].xyz;

                    // v.vertex defines a unit square with x/y in world coordinates
                    float3 rightOffset = cameraRight * _PclSize.x * v.vertex.x;
                    float3 upOffset = cameraUp * _PclSize.x * v.vertex.y;

                    float4 centerWorld = mul(_LocalToWorld, float4(instanceData.relativePosition, 1.0));
                    float4 pointWorld = centerWorld + float4(rightOffset + upOffset, 0.0);

                    o.position = mul(UNITY_MATRIX_VP, pointWorld);
                }
                else
                {
                    // Screen space
                    float3 relativePosition = instanceData.relativePosition;
                    float4 worldPos = mul(_LocalToWorld, float4(relativePosition, 1.0));
                    float4 clipPos = mul(UNITY_MATRIX_VP, worldPos);

                    float2 pixelToClip = (2.0 / _ScreenParams.xy) * clipPos.w;

                    // v.vertex defines a unit square with x/y in screen space coordinates
                    clipPos.xy += float2(v.vertex.x * _PclSize.x, -v.vertex.y * _PclSize.x) * pixelToClip;

                    o.position = clipPos;
                }

                o.uv = float2(v.vertex.x, v.vertex.y) * 2.0;
                o.color = UnpackColor(instanceData.rgba);

                return o;
            }

            fixed4 frag(VS_output i) : SV_Target
            {
                float radius = length(i.uv);
                clip(1 - radius);

                return i.color;
            }
            ENDHLSL
        }
    }
}
