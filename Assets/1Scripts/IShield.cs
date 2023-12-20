using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IShield
{
    public void HitShield(Vector3 _hitPos, Vector3 _hitNormal, float _damageAmount, float _hitSize);
}
