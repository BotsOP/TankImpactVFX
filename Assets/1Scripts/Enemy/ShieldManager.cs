using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(VisualEffect))]
public class ShieldManager : MonoBehaviour, IShield
{
    [SerializeField] private VisualEffect sparkVFX;
    [SerializeField] private float shieldMaxHealth;
    [SerializeField] private float sparkForceShield;
    [SerializeField] private float sparkForceNotShield;
    [SerializeField] private float sparkGravityShield;
    [SerializeField] private float sparkGravityNotShield;
    [SerializeField] private Transform bulletSpawnPos;
    [SerializeField] private GameObject bulletObject;
    [SerializeField] private Color shieldSparkColor;
    [SerializeField] private Color notShieldSparkColor;
    [SerializeField, Range(0, 0.1f)] private float shieldShockwaveSpeed;
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private ComputeShader shieldManagerShader;
    private int kernelCheckShieldHit;
    private int kernelHitShield;
    private int kernelUpdateShield;
    private int threadGroupX;
    private int shockwaveFrameTime;
    private int shockwaveFrameTimer;

    private void OnEnable()
    {
        UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.Disabled);
        
        shieldManagerShader = Resources.Load<ComputeShader>("ShieldManagerShader");
        kernelCheckShieldHit = shieldManagerShader.FindKernel("CheckShieldHit");
        kernelHitShield = shieldManagerShader.FindKernel("HitShield");
        kernelUpdateShield = shieldManagerShader.FindKernel("UpdateShield");
        shieldManagerShader.GetKernelThreadGroupSizes(kernelHitShield, out uint x, out uint _, out uint _);
        threadGroupX = (int)x;

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        meshFilter = gameObject.GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.sharedMesh;
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        GraphicsBuffer hexagonShieldStatsBuffer = mesh.GetVertexBuffer(2);
        int[] hexaStats = new int[mesh.vertexCount * 3];
        hexagonShieldStatsBuffer.SetData(hexaStats);
        hexagonShieldStatsBuffer.Dispose();

        shockwaveFrameTime = Mathf.CeilToInt(2 / shieldShockwaveSpeed);
    }
    
    public void HitShield(Vector3 _hitPos, Vector3 _hitNormal, float _damageAmount, float _hitSize)
    {
        GameObject tempBullet = Instantiate(bulletObject, bulletSpawnPos);
        Bullet bullet = tempBullet.GetComponent<Bullet>();
        bullet.targetPos = _hitPos;
        bullet.targetNormal = _hitNormal;
        bullet.damageAmount = _damageAmount;
        bullet.hitSize = _hitSize;
        bullet.shieldManager = this;
    }

    public void ActuallyHitShield(Vector3 _hitPos, Vector3 _hitNormal, float _damageAmount, float _hitSize)
    {
        shockwaveFrameTimer = shockwaveFrameTime;
        
        Mesh mesh = meshFilter.sharedMesh;
        int x = Mathf.CeilToInt(mesh.GetTriangleCount(0) / (float)threadGroupX);
        
        Vector3 localHitPos = transform.InverseTransformPoint(_hitPos);
        
        GraphicsBuffer vertexBuffer = mesh.GetVertexBuffer(0);
        GraphicsBuffer parentIndexBuffer = mesh.GetVertexBuffer(1);
        GraphicsBuffer hexagonShieldStatsBuffer = mesh.GetVertexBuffer(2);
        
        ComputeBuffer hitTriangles = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        hitTriangles.SetData(new [] {0});

        float intensity = _damageAmount / shieldMaxHealth * 2;
        intensity = Mathf.Clamp01(intensity);
        
        shieldManagerShader.SetVector("_HitPos", localHitPos);
        shieldManagerShader.SetFloat("_Damage", _damageAmount);
        shieldManagerShader.SetFloat("_MaxHealth", shieldMaxHealth);
        shieldManagerShader.SetFloat("_HitSize", _hitSize);
        shieldManagerShader.SetInt("_VertexCount", mesh.vertexCount);
        
        shieldManagerShader.SetBuffer(kernelCheckShieldHit, "_VertexBuffer", vertexBuffer);
        shieldManagerShader.SetBuffer(kernelCheckShieldHit, "_VertexParentIndex", parentIndexBuffer);
        shieldManagerShader.SetBuffer(kernelCheckShieldHit, "_HexagonStats", hexagonShieldStatsBuffer);
        shieldManagerShader.SetBuffer(kernelCheckShieldHit, "_HitTriangles", hitTriangles);
        shieldManagerShader.Dispatch(kernelCheckShieldHit, x, 1, 1);
        
        if (hitTriangles.GetCounter() > 0)
        {
            ShieldSparks(_hitPos, _hitNormal * sparkForceNotShield, notShieldSparkColor, intensity, sparkGravityNotShield);
        }
        else
        {
            meshRenderer.material.SetBuffer("_HexagonStatsBuffer", hexagonShieldStatsBuffer);
            meshRenderer.material.SetBuffer("_Vertices", vertexBuffer);
        
            shieldManagerShader.SetBuffer(kernelHitShield, "_VertexBuffer", vertexBuffer);
            shieldManagerShader.SetBuffer(kernelHitShield, "_VertexParentIndex", parentIndexBuffer);
            shieldManagerShader.SetBuffer(kernelHitShield, "_HexagonStats", hexagonShieldStatsBuffer);
            shieldManagerShader.Dispatch(kernelHitShield, x, 1, 1);
            
            ShieldSparks(_hitPos, _hitNormal * sparkForceShield, shieldSparkColor, intensity, sparkGravityShield);
        }

        hitTriangles?.Dispose();
        vertexBuffer?.Dispose();
        parentIndexBuffer?.Dispose();
        hexagonShieldStatsBuffer?.Dispose();
    }

    private void ShieldSparks(Vector3 _pos, Vector3 _normal, Color _color, float _intensity, float _gravity)
    {
        sparkVFX.SetVector3("HitPos", transform.InverseTransformPoint(_pos));
        sparkVFX.SetVector3("HitNormal", transform.InverseTransformDirection(_normal));
        sparkVFX.SetFloat("Intensity", _intensity);
        sparkVFX.SetFloat("Gravity", _gravity);
        sparkVFX.SetVector4("SparkColor", _color);
        sparkVFX.SendEvent("Hit");
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
