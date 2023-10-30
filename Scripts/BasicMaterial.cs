using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class BasicMaterial : CustomNetworkObject, IPickupable
{

  //
  bool _spawned;

  //
  public override void OnStartClient()
  {
    SyncServerData();
  }

  //
  protected override void Spawn()
  {

    if (_spawned)
      return;
    _spawned = true;

    // Load mesh and init physics
    var newMesh = SpawnNetworkObjectModel(_ObjectType, transform);
    newMesh.transform.localPosition = Vector3.zero;

    //
    Init(_ObjectType);
  }
}
