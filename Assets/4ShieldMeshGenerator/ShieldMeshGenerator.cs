using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class ShieldMeshGenerator : MonoBehaviour
{
    private const int MAX_TRIANGLE_CLUMP = 50;
    private const float HIGH_NUMBER = 1000f;
    
    [SerializeField] private Mesh mesh;

    private int kernelTriangleClmp;
    private int kernelGetNextTriangle;

    private ComputeShader triangleClumpShader;

    public void GenerateShieldMesh()
    {
        if (triangleClumpShader == null)
        {
            triangleClumpShader = Resources.Load<ComputeShader>("TriangleClumper");
        }
        kernelTriangleClmp = triangleClumpShader.FindKernel("TriangleClmp");
        kernelGetNextTriangle = triangleClumpShader.FindKernel("GetNextTriangle");
        
        if (mesh == null)
        {
            Debug.LogWarning($"No mesh selected");
            return;
        }
        
        Mesh newMesh;
        newMesh = mesh.CopyMesh();
        //newMesh = mesh;
        newMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1));
        newMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.SInt32, 3, 2));
        newMesh.LogAllVertexAttributes();
        newMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        newMesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
        
        GraphicsBuffer vertexBuffer = newMesh.GetVertexBuffer(0);
        GraphicsBuffer parentIndexBuffer = newMesh.GetVertexBuffer(1);
        
        ComputeBuffer hexagonEdgeAppendBuffer = new ComputeBuffer(MAX_TRIANGLE_CLUMP, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        hexagonEdgeAppendBuffer.SetCounterValue(0);
        
        ComputeBuffer nextTriangleBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        nextTriangleBuffer.SetData(new int[1]);
        
        Vector2[] indexArray = new Vector2[parentIndexBuffer.count];
        Array.Fill(indexArray, new Vector2(-1, 0));
        parentIndexBuffer.SetData(indexArray);

        triangleClumpShader.GetKernelThreadGroupSizes(0, out uint x, out uint _, out uint _);
        for (int i = 0; i < newMesh.subMeshCount; i++)
        {
            List<Triangle> trianglesToCheck = new List<Triangle>();
            int nextTriangleIndex = Random.Range(0, newMesh.vertexCount);

            bool firstLoop = true;
            int j = 0;
            while ((trianglesToCheck.Count > 0 || firstLoop) && j < newMesh.GetTriangleCount(i))
            {
                firstLoop = false;
                int xThreadGroup = Mathf.CeilToInt((float)newMesh.vertexCount / 3 / x);
                
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_VertexBuffer", vertexBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_VertexParentIndex", parentIndexBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_HexagonEdgeAppend", hexagonEdgeAppendBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_HexagonEdgeCounter", nextTriangleBuffer);
                triangleClumpShader.SetInt("_ParentTriangleIndex", nextTriangleIndex);
                triangleClumpShader.SetInt("_TriangleCount", (int)newMesh.GetIndexCount(i) / 3);
                triangleClumpShader.Dispatch(kernelTriangleClmp, xThreadGroup, 1, 1);

                int amountTriangleEdges = nextTriangleBuffer.GetCounter();
                if (amountTriangleEdges > 0)
                {
                    Triangle[] triangleEdges = new Triangle[MAX_TRIANGLE_CLUMP];
                    hexagonEdgeAppendBuffer.GetData(triangleEdges);
                    hexagonEdgeAppendBuffer.SetCounterValue(0);
                    
                    Array.Resize(ref triangleEdges, amountTriangleEdges);
                    trianglesToCheck = trianglesToCheck.Concat(triangleEdges).ToList();
                    trianglesToCheck = trianglesToCheck.Distinct().ToList();
                }

                nextTriangleIndex = -1;
                int k = 0;
                while (nextTriangleIndex == -1 && k < newMesh.GetTriangleCount(i) && trianglesToCheck.Count > 0)
                {
                    Triangle nextTriangle = trianglesToCheck[0];
                    trianglesToCheck.RemoveAt(0);

                    List<Vector3> triangleEdge = new List<Vector3>();

                    if (nextTriangle.pos1.x < HIGH_NUMBER && nextTriangle.pos1.y < HIGH_NUMBER && nextTriangle.pos1.z < HIGH_NUMBER)
                        triangleEdge.Add(nextTriangle.pos1);
                
                    if (nextTriangle.pos2.x < HIGH_NUMBER && nextTriangle.pos2.y < HIGH_NUMBER && nextTriangle.pos2.z < HIGH_NUMBER)
                        triangleEdge.Add(nextTriangle.pos2);
                
                    if (nextTriangle.pos3.x < HIGH_NUMBER && nextTriangle.pos3.y < HIGH_NUMBER && nextTriangle.pos3.z < HIGH_NUMBER)
                        triangleEdge.Add(nextTriangle.pos3);

                    if (triangleEdge.Count != 2)
                    {
                        Debug.LogWarning($"Amount matching edges is not 2");
                        continue;
                    }
                
                    nextTriangleBuffer.SetData(new[] {(int)-1});
                
                    triangleClumpShader.SetVector("_HexagonEdge1", triangleEdge[0]);
                    triangleClumpShader.SetVector("_HexagonEdge2", triangleEdge[1]);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "_VertexBuffer", vertexBuffer);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "newTriangleIndex", nextTriangleBuffer);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "_VertexParentIndex", parentIndexBuffer);
                    triangleClumpShader.SetInt("_TriangleCount", (int)newMesh.GetIndexCount(i) / 3);
                    triangleClumpShader.Dispatch(kernelGetNextTriangle, xThreadGroup, 1, 1);

                    nextTriangleIndex = nextTriangleBuffer.GetCounter();
                    k++;
                }

                j++;
            }
        }
        
        parentIndexBuffer.GetData(indexArray);
        newMesh.uv = indexArray;
        
        AssetDatabase.CreateAsset( newMesh, "Assets/4ShieldMeshGenerator/GeneratedMeshes/" + newMesh.name + ".asset");
        AssetDatabase.SaveAssets();
        
        vertexBuffer?.Dispose();
        parentIndexBuffer.Dispose();
        hexagonEdgeAppendBuffer.Dispose();
        nextTriangleBuffer.Dispose();
    }
    
    private struct Triangle
    {
        public Vector3 pos1;
        public Vector3 pos2;
        public Vector3 pos3;
    }
}
