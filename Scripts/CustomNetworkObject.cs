using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using System.ComponentModel.Design;

public class CustomNetworkObject : NetworkBehaviour
{

  [System.NonSerialized]
  public Rigidbody _Rb;
  [System.NonSerialized]
  public Collider _Collider;

  //
  public bool _IsJustThrown
  {
    get
    {
      var thrownComponent = GetComponent<ThrownPickupable>();
      return thrownComponent?.enabled ?? false;
    }
  }

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
  public System.Action _OnInit;
  protected void Init(ObjectType objectType)
  {
    _Rb = GetComponent<Rigidbody>();
    _Collider = transform.GetChild(0).GetComponent<Collider>();

    _ObjectType = objectType;

    //
    _OnInit?.Invoke();
    _OnInit = null;
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
  protected virtual void Spawn() { }

  // Update is called once per frame
  void Update()
  {

  }

  //
  protected System.Action<CustomNetworkObject> _OnNetworkCollision;
  [Server]
  public void OnCollisionEnter(Collision c)
  {

    // Do not check if no collision function
    if (_OnNetworkCollision == null) return;

    // Gather network object and invoke function
    var networkObject = GetNetworkObjectFrom(c.collider);
    OnCollisionEnter(networkObject);
  }
  public void OnCollisionEnter(CustomNetworkObject other)
  {
    // Do not check if no collision function
    if (_OnNetworkCollision == null) return;

    //
    if (other == null) return;

    _OnNetworkCollision?.Invoke(other);
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
    _Collider.enabled = toggle;
  }

  //
  public void IgnoreCollisionsWith(CustomNetworkObject other, bool ignore = true)
  {
    Physics.IgnoreCollision(_Collider, other._Collider, ignore);
  }

  //
  public static CustomNetworkObject GetNetworkObjectFrom(Collider c)
  {
    return c.transform.parent.GetComponent<CustomNetworkObject>();
  }
  protected static CustomNetworkObject GetNetworkObjectFrom(GameObject g)
  {
    return g.GetComponent<CustomNetworkObject>();
  }

  //
  public static Transform SpawnNetworkObjectModel(ObjectType objectType, Transform parent)
  {
    return GameObject.Instantiate(Resources.Load<GameObject>($@"NetworkObjects/{objectType}"), parent).transform;
  }

  //
  [Server]
  public static CustomNetworkObject SpawnNetworkObject(ObjectType objectType, Vector3 posAt)
  {
    // Spawn new network object
    var mat = NetworkManager.singleton.spawnPrefabs[0];
    var newMat = GameObject.Instantiate(mat);

    newMat.transform.position = posAt;
    newMat.name = $"{objectType}";

    // Network spawn
    var networkObject = GetNetworkObjectFrom(newMat);
    networkObject._ObjectType = objectType;
    NetworkServer.Spawn(newMat);

    //
    return networkObject;
  }
}
