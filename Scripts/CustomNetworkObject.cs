using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using UnityEngine.UI;

public class CustomNetworkObject : NetworkBehaviour
{

  public Rigidbody _Rb;
  Collider _collider;

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
  public void ToggleCollider(bool toggle)
  {
    _collider.enabled = toggle;
  }
}
