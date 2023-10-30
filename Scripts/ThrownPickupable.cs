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

    // Register network collision
    var networkObject = CustomNetworkObject.GetNetworkObjectFrom(c.collider);
    networkObject?.OnCollisionEnter(_Self);

    // Reset collisions
    _Self.IgnoreCollisionsWith(_Thrower, false);

    // Destroy script
    Destroy(this);
    this.enabled = false;
  }
}
