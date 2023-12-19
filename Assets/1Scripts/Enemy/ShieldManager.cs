using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ShieldManager : MonoBehaviour, IShield
{
    [SerializeField] private float shieldMaxHealth;
    [SerializeField, Range(0, 0.1f)] private float shieldShockwaveSpeed;
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private ComputeShader shieldManagerShader;
    private int kernelHitShield;
    private int kernelUpdateShield;
    private int threadGroupX;
    private int shockwaveFrameTime;
    private int shockwaveFrameTimer;

    private void OnEnable()
    {
        shieldManagerShader = Resources.Load<ComputeShader>("ShieldManagerShader");
        kernelHitShield = shieldManagerShader.FindKernel("HitShield");
        kernelUpdateShield = shieldManagerShader.FindKernel("UpdateShield");
        shieldManagerShader.GetKernelThreadGroupSizes(kernelHitShield, out uint x, out uint _, out uint _);
        threadGroupX = (int)x;

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        meshFilter = gameObject.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        GraphicsBuffer hexagonShieldStatsBuffer = mesh.GetVertexBuffer(2);
        int[] hexaStats = new int[mesh.vertexCount * 2];
        hexagonShieldStatsBuffer.SetData(hexaStats);

        shockwaveFrameTime = Mathf.CeilToInt(2 / shieldShockwaveSpeed);
    }
    
    public void HitShield(int _objectID, Vector3 _hitPos, float _damageAmount, float _hitSize)
    {
        shockwaveFrameTimer = shockwaveFrameTime;
        
        Mesh mesh = meshFilter.sharedMesh;
        int x = Mathf.CeilToInt(mesh.GetTriangleCount(0) / (float)threadGroupX);
        
        Vector3 localHitPos = transform.InverseTransformPoint(_hitPos);
        
        GraphicsBuffer vertexBuffer = mesh.GetVertexBuffer(0);
        GraphicsBuffer parentIndexBuffer = mesh.GetVertexBuffer(1);
        GraphicsBuffer hexagonShieldStatsBuffer = mesh.GetVertexBuffer(2);
        meshRenderer.material.SetBuffer("_HexagonStatsBuffer", hexagonShieldStatsBuffer);
        meshRenderer.material.SetBuffer("_Vertices", vertexBuffer);

        ComputeBuffer hitTriangles = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        hitTriangles.SetData(new [] {0});
        
        shieldManagerShader.SetVector("_HitPos", localHitPos);
        shieldManagerShader.SetFloat("_Damage", _damageAmount);
        shieldManagerShader.SetFloat("_MaxHealth", shieldMaxHealth);
        shieldManagerShader.SetFloat("_HitSize", _hitSize);
        shieldManagerShader.SetInt("_VertexCount", mesh.vertexCount);
        shieldManagerShader.SetBuffer(kernelHitShield, "_HitTriangles", hitTriangles);
        shieldManagerShader.SetBuffer(kernelHitShield, "_VertexBuffer", vertexBuffer);
        shieldManagerShader.SetBuffer(kernelHitShield, "_VertexParentIndex", parentIndexBuffer);
        shieldManagerShader.SetBuffer(kernelHitShield, "_HexagonStats", hexagonShieldStatsBuffer);
        shieldManagerShader.Dispatch(kernelHitShield, x, 1, 1);
        
        Debug.Log(hitTriangles.GetCounter());
            
        hitTriangles?.Dispose();
        vertexBuffer?.Dispose();
        parentIndexBuffer?.Dispose();
        hexagonShieldStatsBuffer?.Dispose();
    }

    private void Update()
    {
        if (shockwaveFrameTimer > 0)
        {
            shockwaveFrameTimer--;
            
            Mesh mesh = meshFilter.sharedMesh;
            int x = Mathf.CeilToInt(mesh.vertexCount / (float)threadGroupX);
        
            GraphicsBuffer parentIndexBuffer = mesh.GetVertexBuffer(1);
            GraphicsBuffer hexagonShieldStatsBuffer = mesh.GetVertexBuffer(2);
        
            shieldManagerShader.SetFloat("_Damage", shieldShockwaveSpeed);
            shieldManagerShader.SetInt("_VertexCount", mesh.vertexCount);
            shieldManagerShader.SetBuffer(kernelUpdateShield, "_VertexParentIndex", parentIndexBuffer);
            shieldManagerShader.SetBuffer(kernelUpdateShield, "_HexagonStats", hexagonShieldStatsBuffer);
            shieldManagerShader.Dispatch(kernelUpdateShield, x, 1, 1);
            
            parentIndexBuffer?.Dispose();
            hexagonShieldStatsBuffer?.Dispose();
        }
        
    }

    public void ResetHealth()
    {
        
    }
}
