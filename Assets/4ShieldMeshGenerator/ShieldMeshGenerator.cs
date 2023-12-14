using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ShieldMeshGenerator : MonoBehaviour
{
    private const int MAX_TRIANGLE_CLUMP = 50;
    private const float HIGH_NUMBER = 99999999999999999999999999999999999999f;
    
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
        
        //ClearCustomMeshData();

        Mesh newMesh = mesh.CopyMesh();
        //Mesh newMesh = mesh;
        newMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 1, 1));
        newMesh.AddVertexAttribute(new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 2));
        newMesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        newMesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
        
        GraphicsBuffer vertexBuffer = newMesh.GetVertexBuffer(0);
        GraphicsBuffer parentIndexBuffer = newMesh.GetVertexBuffer(1);
        
        ComputeBuffer hexagonEdgeAppendBuffer = new ComputeBuffer(MAX_TRIANGLE_CLUMP, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        hexagonEdgeAppendBuffer.SetCounterValue(0);
        
        ComputeBuffer nextTriangleBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
        nextTriangleBuffer.SetData(new int[1]);
        
        GraphicsBuffer indexBuffer = newMesh.GetIndexBuffer();

        triangleClumpShader.GetKernelThreadGroupSizes(0, out uint x, out uint y, out uint z);
        for (int i = 0; i < newMesh.subMeshCount; i++)
        {
            List<Triangle> trianglesToCheck = new List<Triangle>();
            int nextTriangleIndex = 0;
            
            float[] testIndex = new float[parentIndexBuffer.count];
            Array.Fill(testIndex, -1f);
            parentIndexBuffer.SetData(testIndex);

            bool firstLoop = true;
            int j = 0;
            while ((trianglesToCheck.Count > 0 || firstLoop) && j < newMesh.GetTriangleCount(i))
            {
                firstLoop = false;
                int xThreadGroup = Mathf.CeilToInt((float)newMesh.GetTriangleCount(i) / x);
                
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_VertexBuffer", vertexBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_IndexBuffer", indexBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_VertexParentIndex", parentIndexBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_HexagonEdgeAppend", hexagonEdgeAppendBuffer);
                triangleClumpShader.SetBuffer(kernelTriangleClmp, "_HexagonEdgeCounter", nextTriangleBuffer);
                triangleClumpShader.SetInt("_ParentTriangleIndex", nextTriangleIndex);
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
                        return;
                    }
                
                    nextTriangleBuffer.SetData(new[] {-1});
                
                    triangleClumpShader.SetVector("_HexagonEdge1", triangleEdge[0]);
                    triangleClumpShader.SetVector("_HexagonEdge2", triangleEdge[1]);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "_VertexBuffer", vertexBuffer);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "_IndexBuffer", indexBuffer);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "newTriangleIndex", nextTriangleBuffer);
                    triangleClumpShader.SetBuffer(kernelGetNextTriangle, "_VertexParentIndex", parentIndexBuffer);
                    triangleClumpShader.Dispatch(kernelGetNextTriangle, xThreadGroup, 1, 1);

                    nextTriangleIndex = nextTriangleBuffer.GetCounter();
                    k++;
                }

                j++;
            }
            
        }
        
        AssetDatabase.CreateAsset( newMesh, "Assets/4ShieldMeshGenerator/GeneratedMeshes/" + newMesh.name + ".asset");
        AssetDatabase.SaveAssets();
        
        indexBuffer?.Dispose();
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
