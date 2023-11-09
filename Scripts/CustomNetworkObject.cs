using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using System.ComponentModel.Design;

public class CustomNetworkObject : NetworkBehaviour
{

  static int s_id;
  public int _Id;

  // Physics
  [System.NonSerialized]
  public Rigidbody _Rb;
  [System.NonSerialized]
  public Collider _Collider;

  // Dimension info
  protected int _dimensionIndex;

  // Sfx
  int _sfxProfileIndex;
  SfxProfile _sfxProfile { get { return GameController.s_Singleton._SfxProfiles[_sfxProfileIndex]; } }

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
  protected void Init(ObjectType objectType, int dimensionIndex, int sfxProfileIndex)
  {

    // Local
    _Id = s_id++;
    _ObjectType = objectType;

    // Physics
    _Rb = GetComponent<Rigidbody>();
    _Collider = transform.GetChild(0).GetComponent<Collider>();

    //
    _dimensionIndex = dimensionIndex;

    // Sfx
    _sfxProfileIndex = sfxProfileIndex;

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

  public void OnDestroy()
  {
    //
    if (_dimensionIndex > -1)
      DimensionController.RemoveFromDimension(_dimensionIndex, this);
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
  protected struct SfxPlayData
  {
    public Vector3 PosAt;
    public float Volume, PitchLower, PitchHigher;
    public bool Loop;

    public SfxPlayData(Vector3 posAt)
    {
      PosAt = posAt;

      Volume = 1f;
      PitchLower = 0.9f;
      PitchHigher = 1.1f;

      Loop = false;
    }
  }
  protected AudioSource PlayAudioSourceAt(int sfxProfileIndex, SfxPlayData sfxPlayData)
  {
    var newAudioSource = SfxManager.PlayAudioSourceSimple(
      sfxPlayData.PosAt,

      _sfxProfile._audioClips[sfxProfileIndex],
      sfxPlayData.Volume,
      sfxPlayData.PitchLower, sfxPlayData.PitchHigher,

      SfxManager.AudioPriority.NORMAL
    );
    newAudioSource.loop = sfxPlayData.Loop;

    return newAudioSource;
  }

  protected void StopAudioSource(AudioSource source)
  {
    SfxManager.StopAudioSource(source);
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
  public virtual void SetDimension(int dimension)
  {

  }
  protected void SetDimensionBase(int dimensionId, ref Material[] materials, bool setPosition)
  {
    switch (dimensionId)
    {

      // Exit dimension
      case -1:

        // Set position back to origin
        if (setPosition)
        {

          // Last dimension data
          var dimensionLast = DimensionController.GetDimension(_dimensionIndex);
          var dimensionOriginLast = dimensionLast.Origin;
          var dimensionOffsetLast = dimensionLast.Offset;
          var dimensionLeftLast = dimensionLast.DimensionLeft;

          transform.position = new Vector3(
            transform.position.x - (dimensionOriginLast.x + (dimensionLeftLast ? 13f : 0f)),
            1.2f,
            4.989f
          ) + dimensionOffsetLast;
        }

        // Set material properties
        for (var i = materials.Length - 1; i >= 0; i--)
        {
          var material = materials[i];
          material.SetInt("_InDimensions", 0);
          material.SetFloat("_Magic", 1f);
        }

        break;

      // Enter left / right dimension
      default:

        // Dimension data
        var dimension = DimensionController.GetDimension(dimensionId);
        var dimensionOrigin = dimension.Origin;
        var dimensionOffset = dimension.Offset;
        var dimensionLeft = dimension.DimensionLeft;

        // Set position to entrance of dimension
        if (setPosition)
        {
          transform.position = new Vector3(
            transform.position.x + (dimensionOrigin.x + (dimensionLeft ? 13f : 0f)),
            1.2f,
            dimensionOrigin.z + 5.49f
          ) - dimensionOffset;
        }

        // Set material properties
        for (var i = materials.Length - 1; i >= 0; i--)
        {
          var material = materials[i];
          material.SetInt("_InDimensions", 1);
          material.SetInt("_DimensionRight", dimensionLeft ? 0 : 1);
          material.SetVector("_Offset", new Vector3(dimensionOrigin.x, dimensionOrigin.y, dimensionOrigin.z));
          material.SetVector("_InclusionOffset", dimensionOffset);
        }

        break;
    }

    //
    if (_dimensionIndex == -1)
      DimensionController.AddToDimension(dimensionId, this);
    else
      DimensionController.RemoveFromDimension(_dimensionIndex, this);
    _dimensionIndex = dimensionId;
  }
  public virtual void SetDimensionOffset(Vector3 offset)
  {

  }
  public virtual void SetDimensionMagic(float magic){

  }

  public virtual void ToggleDimension(bool toggle, bool left)
  {

  }

  //
  public void IgnoreCollisionsWith(CustomNetworkObject other, bool ignore = true)
  {
    Physics.IgnoreCollision(_Collider, other._Collider, ignore);
  }

  //
  public static Vector3 GetDimensionOffset(Vector3 position, int dimension, Vector2 dimensionOffset)
  {
    return position - new Vector3(dimensionOffset.x + (dimension == 0 ? 13f : 0f), 0f, dimensionOffset.y);
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
