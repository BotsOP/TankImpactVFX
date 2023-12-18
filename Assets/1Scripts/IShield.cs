using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IShield
{
    public void HitShield(int _objectID, Vector3 _hitPos, float _damageAmount, float _hitSize);
}
