using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldManager : MonoBehaviour, IShield
{
    private ComputeShader shieldManagerShader;
    private Dictionary<int, MeshFilter> childMeshDictionary;
    private int kernelHitShield;
    private int kernelUpdateShield;
    private int threadGroupX;

    private void OnEnable()
    {
        shieldManagerShader = Resources.Load<ComputeShader>("ShieldManagerShader");
        kernelHitShield = shieldManagerShader.FindKernel("HitShield");
        kernelUpdateShield = shieldManagerShader.FindKernel("UpdateShield");
        shieldManagerShader.GetKernelThreadGroupSizes(kernelHitShield, out uint x, out uint _, out uint _);
        threadGroupX = (int)x;

        childMeshDictionary = new Dictionary<int, MeshFilter>();

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject tempGameObject = transform.GetChild(i).gameObject;
            int instanceID = tempGameObject.GetInstanceID();
            childMeshDictionary.Add(instanceID, tempGameObject.GetComponent<MeshFilter>());
        }
    }
    
    public void HitShield(int _objectID, Vector3 _hitPos, float _damageAmount, float _hitSize)
    {
        if (!childMeshDictionary.ContainsKey(_objectID))
        {
            Debug.LogWarning($"object hit is not in dictionary");
            return;
        }

        Transform hitTransform = childMeshDictionary[_objectID].transform;
        Mesh mesh = childMeshDictionary[_objectID].sharedMesh;
        int x = Mathf.CeilToInt(mesh.vertexCount / (float)threadGroupX);
        
        Vector3 localHitPos = hitTransform.InverseTransformPoint(_hitPos);
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Structured;
        
        GraphicsBuffer vertexBuffer = mesh.GetVertexBuffer(0);
        GraphicsBuffer parentIndexBuffer = mesh.GetVertexBuffer(1);
        GraphicsBuffer hexagonShieldStatsBuffer = mesh.GetVertexBuffer(2);
        
        shieldManagerShader.SetVector("_HitPos", localHitPos);
        shieldManagerShader.SetFloat("_Damage", _damageAmount);
        shieldManagerShader.SetFloat("_HitSize", _hitSize);
        shieldManagerShader.SetBuffer(kernelHitShield, "_VertexBuffer", vertexBuffer);
        shieldManagerShader.SetBuffer(kernelHitShield, "_VertexParentIndex", parentIndexBuffer);
        shieldManagerShader.SetBuffer(kernelHitShield, "_HexagonStats", hexagonShieldStatsBuffer);
        shieldManagerShader.Dispatch(kernelHitShield, x, 1, 1);
        
        vertexBuffer?.Dispose();
        parentIndexBuffer?.Dispose();
        hexagonShieldStatsBuffer?.Dispose();
    }
}
