using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrownPickupable : MonoBehaviour
{

  public CustomNetworkObject _Self, _Thrower;

  //
  void OnCollisionEnter(Collision c)
  {

    if (!this.enabled) return;

    Debug.Log(c.gameObject.name);

    // Reset collisions
    _Self.IgnoreCollisionsWith(_Thrower, false);

    // Destroy script
    Destroy(this);
    this.enabled = false;
  }
}
