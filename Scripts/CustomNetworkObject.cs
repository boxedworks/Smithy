using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using System.ComponentModel.Design;

public class CustomNetworkObject : NetworkBehaviour
{

  [System.NonSerialized]
  public Rigidbody _Rb;
  protected Collider _collider;

  //
  public enum ObjectType
  {
    NONE,

    ENTITY_PLAYER,

    BLOCK_FORGE,

    OBJECT_CRATE,

    OBJECT_ORE_IRON,

    OBJECT_BAR_IRON,

  }
  [System.NonSerialized]
  public ObjectType _ObjectType;

  //
  protected void Init(ObjectType objectType)
  {
    _Rb = GetComponent<Rigidbody>();
    _collider = transform.GetChild(0).GetComponent<Collider>();

    _ObjectType = objectType;
  }

  //
  [Command(requiresAuthority = false)]
  protected void SyncServerData()
  {
    RpcSpawn(_ObjectType);
  }
  [ClientRpc]
  protected void RpcSpawn(ObjectType objectType)
  {
    _ObjectType = objectType;

    Spawn();
  }
  protected virtual void Spawn()
  {

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
  public void NetworkDestroy()
  {

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
  protected static CustomNetworkObject GetNetworkObjectFrom(GameObject g)
  {
    return g.GetComponent<CustomNetworkObject>();
  }

  //
  [Server]
  public static GameObject SpawnNetworkObject(ObjectType objectType, Vector3 posAt)
  {
    // Spawn new network object
    var mat = NetworkManager.singleton.spawnPrefabs[0];
    var newMat = GameObject.Instantiate(mat);

    newMat.transform.position = posAt;
    newMat.name = $"{objectType}";

    // Network spawn
    GetNetworkObjectFrom(newMat)._ObjectType = objectType;
    NetworkServer.Spawn(newMat);

    //
    return newMat;
  }
}
