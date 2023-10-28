using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using UnityEngine.UI;

public class CustomNetworkObject : NetworkBehaviour
{

  public Rigidbody _Rb;
  protected Collider _collider;

  // Start is called before the first frame update
  protected void Start()
  {
    _Rb = GetComponent<Rigidbody>();
    _collider = transform.GetChild(0).GetComponent<Collider>();
  }

  // Update is called once per frame
  void Update()
  {

  }

  //
  protected System.Action<CustomNetworkObject> _OnNetworkCollision;
  [Server]
  void OnCollisionEnter(Collision c)
  {

    // Do not check if no collision function
    if (_OnNetworkCollision == null) return;

    // Gather network object and invoke function
    var networkObject = GetNetworkObjectFrom(c.collider);
    if (networkObject == null) return;

    _OnNetworkCollision?.Invoke(networkObject);
  }

//
public void NetworkDestroy(){

  NetworkServer.Destroy(gameObject);
  //GameObject.Destroy(gameObject);

}

  //
  public void ToggleCollider(bool toggle)
  {
    _collider.enabled = toggle;
  }

  //
  public void IgnoreCollisionsWith(CustomNetworkObject other, bool ignore = true)
  {
    Physics.IgnoreCollision(_collider, other._collider, ignore);
  }

  //
  protected static CustomNetworkObject GetNetworkObjectFrom(Collider c)
  {
    return c.transform.parent.GetComponent<CustomNetworkObject>();
  }
}
