using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private int amountLinePoints;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private LayerMask mask;
    [SerializeField] private float speed;
    [SerializeField] private float warmUpTime;
    [SerializeField] private float jiggleAmount = 10f;
    [SerializeField] private float jiggleSpeed = 1f;

    public ShieldManager shieldManager;
    public Vector3 targetPos;
    public Vector3 targetNormal;
    public float damageAmount;
    public float hitSize;

    private Quaternion originalRotation;
    private float startTime;
    private Material mat;
    private Vector3 spawnPos;
    private LineRenderer lineRenderer;

    private void OnEnable()
    {
        lineRenderer = gameObject.GetComponent<LineRenderer>();
        transform.LookAt(targetPos);
        transform.Rotate(Vector3.up, 180);
        originalRotation = transform.rotation;
        spawnPos = transform.position;
        mat = meshRenderer.material;
        startTime = Time.time;
    }

    private void FixedUpdate()
    {
        if (Time.time < startTime + warmUpTime)
        {
            float t = (Time.time - startTime) / warmUpTime;
            mat.SetFloat("_YMask", t);
            float jiggle = Mathf.Sin(Time.time * jiggleSpeed) * jiggleAmount;
            transform.rotation = originalRotation * Quaternion.Euler(0, 0, jiggle);
            return;
        }
        
        transform.position += (targetPos - spawnPos) * speed;
        Vector3[] allPos = new Vector3[amountLinePoints];
        for (int i = 0; i <= amountLinePoints - 1; i++)
        {
            float t = (float)i / (amountLinePoints - 1);
            Vector3 pos = Vector3.Lerp(spawnPos, transform.position, t);
            allPos[i] = pos;
        }
        lineRenderer.SetPositions(allPos);
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((mask & 1 << other.gameObject.layer) == 0)
        {
            return;
        }
        
        Destroy(gameObject);
        shieldManager.ActuallyHitShield(targetPos, targetNormal, damageAmount, hitSize);
    }
}
