using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class SplineTrackBuilder : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private bool closedLoop = true;

    [Header("Road")]
    [SerializeField] private float trackWidth = 10f;
    [SerializeField] private float roadThickness = 0.2f;
    [SerializeField] private float sampleSpacing = 2f;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private float uvTiling = 4f;
    [SerializeField] private bool addRoadCollider = true;

    [Header("Walls")]
    [SerializeField] private bool generateWalls = true;
    [SerializeField] private float wallHeight = 2f;
    [SerializeField] private float wallThickness = 0.4f;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private float wallYOffset = 1f; // center of wall above road

    [Header("Hierarchy")]
    [SerializeField] private string roadObjectName = "Generated Road";
    [SerializeField] private string wallsObjectName = "Generated Walls";

    [ContextMenu("Generate Track")]
    public void GenerateTrack()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null)
        {
            Debug.LogError("No SplineContainer found.");
            return;
        }

        ClearGenerated();

        List<TrackSample> samples = BuildSamples();
        if (samples.Count < 2)
        {
            Debug.LogError("Not enough spline samples to generate track.");
            return;
        }

        GenerateRoadMesh(samples);

        if (generateWalls)
            GenerateWallSegments(samples);
    }

    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        Transform road = transform.Find(roadObjectName);
        if (road != null)
            SafeDestroy(road.gameObject);

        Transform walls = transform.Find(wallsObjectName);
        if (walls != null)
            SafeDestroy(walls.gameObject);
    }

    private List<TrackSample> BuildSamples()
    {
        var samples = new List<TrackSample>();

        float length = splineContainer.CalculateLength();
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(0.25f, sampleSpacing)));

        if (!closedLoop)
            sampleCount += 1;

        for (int i = 0; i < sampleCount; i++)
        {
            float t;

            if (closedLoop)
                t = (float)i / sampleCount;
            else
                t = (float)i / (sampleCount - 1);

            splineContainer.Evaluate(t, out float3 posF, out float3 tangentF, out float3 upF);

            Vector3 pos = (Vector3)posF;
            Vector3 tangent = ((Vector3)tangentF).normalized;
            Vector3 up = ((Vector3)upF).normalized;

            if (up.sqrMagnitude < 0.001f)
                up = Vector3.up;

            Vector3 right = Vector3.Cross(up, tangent).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = transform.right;

            samples.Add(new TrackSample
            {
                position = pos,
                tangent = tangent,
                up = up,
                right = right
            });
        }

        return samples;
    }

    private void GenerateRoadMesh(List<TrackSample> samples)
    {
        GameObject roadObj = new GameObject(roadObjectName);
        roadObj.transform.SetParent(transform, false);

        MeshFilter meshFilter = roadObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = roadObj.AddComponent<MeshRenderer>();
        if (roadMaterial != null)
            meshRenderer.sharedMaterial = roadMaterial;

        Mesh mesh = BuildRoadMesh(samples, roadObj.transform);
        meshFilter.sharedMesh = mesh;

        if (addRoadCollider)
        {
            MeshCollider collider = roadObj.AddComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
        }
    }

    private Mesh BuildRoadMesh(List<TrackSample> samples, Transform roadTransform)
    {
        int ringCount = samples.Count;
        int segmentCount = closedLoop ? ringCount : ringCount - 1;

        Vector3[] vertices = new Vector3[ringCount * 4];
        Vector2[] uvs = new Vector2[ringCount * 4];
        int[] triangles = new int[segmentCount * 24];

        float halfWidth = trackWidth * 0.5f;
        float halfThickness = roadThickness * 0.5f;

        float accumulatedDistance = 0f;

        for (int i = 0; i < ringCount; i++)
        {
            TrackSample s = samples[i];

            if (i > 0)
                accumulatedDistance += Vector3.Distance(samples[i - 1].position, s.position);

            Vector3 leftTopWorld = s.position - s.right * halfWidth + s.up * halfThickness;
            Vector3 rightTopWorld = s.position + s.right * halfWidth + s.up * halfThickness;
            Vector3 leftBottomWorld = s.position - s.right * halfWidth - s.up * halfThickness;
            Vector3 rightBottomWorld = s.position + s.right * halfWidth - s.up * halfThickness;

            Vector3 leftTop = roadTransform.InverseTransformPoint(leftTopWorld);
            Vector3 rightTop = roadTransform.InverseTransformPoint(rightTopWorld);
            Vector3 leftBottom = roadTransform.InverseTransformPoint(leftBottomWorld);
            Vector3 rightBottom = roadTransform.InverseTransformPoint(rightBottomWorld);;

            int v = i * 4;
            vertices[v + 0] = leftTop;
            vertices[v + 1] = rightTop;
            vertices[v + 2] = leftBottom;
            vertices[v + 3] = rightBottom;

            float uvY = accumulatedDistance / Mathf.Max(0.01f, uvTiling);
            uvs[v + 0] = new Vector2(0f, uvY);
            uvs[v + 1] = new Vector2(1f, uvY);
            uvs[v + 2] = new Vector2(0f, uvY);
            uvs[v + 3] = new Vector2(1f, uvY);
        }

        int tri = 0;
        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % ringCount;

            int a = i * 4;
            int b = next * 4;

            // Top face
            triangles[tri++] = a + 0;
            triangles[tri++] = b + 0;
            triangles[tri++] = a + 1;

            triangles[tri++] = a + 1;
            triangles[tri++] = b + 0;
            triangles[tri++] = b + 1;

            // Bottom face
            triangles[tri++] = a + 2;
            triangles[tri++] = a + 3;
            triangles[tri++] = b + 2;

            triangles[tri++] = a + 3;
            triangles[tri++] = b + 3;
            triangles[tri++] = b + 2;

            // Left side
            triangles[tri++] = a + 2;
            triangles[tri++] = b + 0;
            triangles[tri++] = a + 0;

            triangles[tri++] = a + 2;
            triangles[tri++] = b + 2;
            triangles[tri++] = b + 0;

            // Right side
            triangles[tri++] = a + 1;
            triangles[tri++] = b + 1;
            triangles[tri++] = a + 3;

            triangles[tri++] = a + 3;
            triangles[tri++] = b + 1;
            triangles[tri++] = b + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Track";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void GenerateWallSegments(List<TrackSample> samples)
    {
        GameObject wallsParent = new GameObject(wallsObjectName);
        wallsParent.transform.SetParent(transform, false);

        int segmentCount = closedLoop ? samples.Count : samples.Count - 1;
        float halfWidth = trackWidth * 0.5f;

        for (int i = 0; i < segmentCount; i++)
        {
            int next = (i + 1) % samples.Count;

            TrackSample a = samples[i];
            TrackSample b = samples[next];

            Vector3 leftA = a.position - a.right * (halfWidth + wallThickness * 0.5f);
            Vector3 leftB = b.position - b.right * (halfWidth + wallThickness * 0.5f);

            Vector3 rightA = a.position + a.right * (halfWidth + wallThickness * 0.5f);
            Vector3 rightB = b.position + b.right * (halfWidth + wallThickness * 0.5f);

            CreateWallSegment("Wall_L_" + i, leftA, leftB, a.up, wallsParent.transform);
            CreateWallSegment("Wall_R_" + i, rightA, rightB, a.up, wallsParent.transform);
        }
    }

    private void CreateWallSegment(string name, Vector3 start, Vector3 end, Vector3 up, Transform parent)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);

        if (wallMaterial != null)
        {
            Renderer r = wall.GetComponent<Renderer>();
            r.sharedMaterial = wallMaterial;
        }

        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length < 0.01f)
        {
            SafeDestroy(wall);
            return;
        }

        Vector3 forward = delta.normalized;
        Vector3 center = (start + end) * 0.5f + up.normalized * wallYOffset;

        wall.transform.position = center;
        wall.transform.rotation = Quaternion.LookRotation(forward, up);
        wall.transform.localScale = new Vector3(wallThickness, wallHeight, length);
    }

    private void SafeDestroy(GameObject go)
    {
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private struct TrackSample
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 up;
        public Vector3 right;
    }
}