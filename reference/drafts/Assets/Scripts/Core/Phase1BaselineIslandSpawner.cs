using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.Core
{
    public sealed class Phase1BaselineIslandSpawner : MonoBehaviour
    {
        [Header("Phase 1 Verification Materials")]
        [SerializeField] private Material baselineIslandMaterial;
        [SerializeField] private Material baselineOceanMaterial;

        [Header("1-Acre Spatial Dimensions")]
        [SerializeField] private float islandSizeMeters = 64.0f; // 64m x 64m = 4,096 m^2 (~1 Acre)
        [SerializeField] private int islandGridSubdivisions = 32; // Prevents giant tile-spanning triangles
        [SerializeField] private float oceanSizeMeters = 512.0f;
        [SerializeField] private int oceanGridSubdivisions = 32;

        private GameObject _islandRoot;

        private void Start()
        {
            BuildPhase1BaselineEnvironment();
        }

        private void BuildPhase1BaselineEnvironment()
        {
            _islandRoot = new GameObject("Phase1_Baseline_Environment");

            // 1. Create Flat Tan 1-Acre Island (y = 0.0m)
            GameObject islandObj = CreateSubdividedPlane(
                "OneAcre_Tan_Island_64x64",
                islandSizeMeters,
                islandGridSubdivisions,
                new Color(0.86f, 0.78f, 0.62f, 1.0f), // Warm Tan Sand vertex color
                baselineIslandMaterial,
                addBoxCollider: true);
            islandObj.transform.SetParent(_islandRoot.transform, false);
            islandObj.transform.localPosition = Vector3.zero;

            // 2. Create Surrounding Ocean Plane (y = -0.35m)
            GameObject oceanObj = CreateSubdividedPlane(
                "Surrounding_Ocean_Plane",
                oceanSizeMeters,
                oceanGridSubdivisions,
                new Color(0.08f, 0.45f, 0.62f, 1.0f), // Tropical Water vertex color
                baselineOceanMaterial,
                addBoxCollider: false);
            oceanObj.transform.SetParent(_islandRoot.transform, false);
            oceanObj.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        }

        private static GameObject CreateSubdividedPlane(
            string objectName,
            float sizeMeters,
            int subdivisions,
            Color vertexColor,
            Material assignedMaterial,
            bool addBoxCollider)
        {
            GameObject go = new GameObject(objectName);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            // Disable shadow casting on flat ground/water to save shadow pass draw calls
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.sharedMaterial = assignedMaterial;

            int vertCountPerSide = subdivisions + 1;
            int totalVerts = vertCountPerSide * vertCountPerSide;
            Vector3[] vertices = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            Color[] colors = new Color[totalVerts];
            int[] triangles = new int[subdivisions * subdivisions * 6];

            float halfSize = sizeMeters * 0.5f;
            float step = sizeMeters / subdivisions;

            int vIndex = 0;
            for (int z = 0; z <= subdivisions; z++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    vertices[vIndex] = new Vector3(-halfSize + x * step, 0f, -halfSize + z * step);
                    normals[vIndex] = Vector3.up;
                    uvs[vIndex] = new Vector2((float)x / subdivisions, (float)z / subdivisions);
                    colors[vIndex] = vertexColor;
                    vIndex++;
                }
            }

            int tIndex = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int bottomLeft = z * vertCountPerSide + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + vertCountPerSide;
                    int topRight = topLeft + 1;

                    triangles[tIndex++] = bottomLeft;
                    triangles[tIndex++] = topLeft;
                    triangles[tIndex++] = topRight;

                    triangles[tIndex++] = bottomLeft;
                    triangles[tIndex++] = topRight;
                    triangles[tIndex++] = bottomRight;
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"{objectName}_Mesh",
                indexFormat = IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;

            if (addBoxCollider)
            {
                BoxCollider col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(sizeMeters, 0.2f, sizeMeters);
                col.center = new Vector3(0f, -0.1f, 0f);
            }

            return go;
        }
    }
}
