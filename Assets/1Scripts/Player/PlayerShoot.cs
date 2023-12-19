using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private float damageAmount;
    [SerializeField] private float hitSize;
    [SerializeField] private LayerMask shieldMask;
    [SerializeField] private Camera cam;
    void Awake()
    {
        
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction);
            RaycastHit hit;
            if (Physics.Raycast (ray, out hit, 100, shieldMask))
            {
                int instanceID = hit.transform.gameObject.GetInstanceID();
                hit.transform.GetComponent<IShield>().HitShield(instanceID, hit.point, damageAmount, hitSize);
            }
        }
    }
}
