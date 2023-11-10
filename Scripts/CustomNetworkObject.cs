using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using UnityEngine.VFX;

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
  protected int _dimensionId;
  protected bool _inDemension { get { return _dimensionId > -1; } }

  protected Material[] _dimensionMaterials;
  protected VisualEffect[] _effects, _dimensionEffects;

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
    _dimensionId = dimensionIndex;

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
    if (_dimensionId > -1)
      DimensionController.RemoveFromDimension(_dimensionId, this);
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
  public struct SetDimensionData
  {
    public int DimensionId;
    public bool SetPosition;
  }
  public virtual void SetDimension(SetDimensionData setDimensionData)
  {

  }

  protected void SetDimensionBase(SetDimensionData setDimensionData)
  {
    var dimensionId = setDimensionData.DimensionId;
    var setPosition = setDimensionData.SetPosition && NetworkEventManager.s_Singleton.isServer;

    if (_ObjectType == ObjectType.OBJECT_ORE_IRON)
      Debug.Log($"[{Time.time}] Setting dim: {dimensionId} setpos: {setPosition}, current [{_dimensionId}]");

    //
    if (_dimensionId == setDimensionData.DimensionId)
    {
      Debug.LogError($"Set dimension base to same dimension {_dimensionId}");
    }

    //
    switch (dimensionId)
    {

      // Exit dimension
      case -1:

        // Set position back to origin
        if (setPosition)
        {

          // Last dimension data
          var dimensionLast = DimensionController.GetDimension(_dimensionId);
          var dimensionOriginLast = dimensionLast.Origin;
          var dimensionOffsetLast = dimensionLast.Offset;
          var dimensionLeftLast = dimensionLast.DimensionLeft;

          transform.position = new Vector3(
            transform.position.x - (dimensionOriginLast.x + (dimensionLeftLast ? 13f : 0f)),
            transform.position.y,
            4.989f
          ) + dimensionOffsetLast;
        }

        // Set material properties
        foreach (var material in _dimensionMaterials)
        {
          material.SetInt("_InDimensions", 0);
          material.SetFloat("_Magic", 1f);
        }

        // Effects
        if (_effects != null)
        {
          foreach (var effect in _dimensionEffects)
            effect.Stop();
          foreach (var effect in _effects)
            effect.Play();
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
            transform.position.y,
            dimensionOrigin.z + 5.49f
          ) - dimensionOffset;
        }

        // Set material properties
        foreach (var material in _dimensionMaterials)
        {
          material.SetInt("_InDimensions", 1);
          material.SetInt("_DimensionRight", dimensionLeft ? 0 : 1);
          material.SetVector("_Offset", new Vector3(dimensionOrigin.x, dimensionOrigin.y, dimensionOrigin.z));
          material.SetVector("_InclusionOffset", dimensionOffset);
        }

        // Set effect properties
        if (_effects != null)
        {
          foreach (var effect in _effects)
            effect.Stop();
          foreach (var effect in _dimensionEffects)
          {
            effect.SetBool("InDimensions", true);
            effect.SetBool("DimensionRight", !dimensionLeft);
            effect.SetVector3("Offset", new Vector3(dimensionOrigin.x, dimensionOrigin.y, dimensionOrigin.z));
            effect.SetVector3("InclusionOffset", dimensionOffset);

            effect.Play();
          }
        }

        break;
    }

    //
    if (_dimensionId == -1)
      DimensionController.AddToDimension(dimensionId, this);
    else
      DimensionController.RemoveFromDimension(_dimensionId, this);
    _dimensionId = dimensionId;
    if (_ObjectType == ObjectType.OBJECT_ORE_IRON)
      Debug.Log($"Set dimension: {dimensionId}");
  }
  public void SetDimensionOffset(Vector3 offset)
  {
    foreach (var material in _dimensionMaterials)
      material.SetVector("_InclusionOffset", offset);
    if (_effects != null)
      foreach (var effect in _dimensionEffects)
        effect.SetVector3("InclusionOffset", offset);
  }
  public void SetDimensionMagic(float magic)
  {
    foreach (var material in _dimensionMaterials)
      material.SetFloat("_Magic", magic);
    if (_effects != null)
      foreach (var effect in _dimensionEffects)
        effect.SetFloat("Magic", magic);
  }

  public void ToggleDimension(bool toggle, bool left)
  {
    foreach (var material in _dimensionMaterials)
    {
      material.SetInt("_InDimensions", toggle ? 1 : 0);
      material.SetInt("_DimensionRight", left ? 0 : 1);
    }
    if (_effects != null)
      foreach (var effect in _dimensionEffects)
      {
        effect.SetBool("InDimensions", toggle);
        effect.SetBool("DimensionRight", !left);
      }
  }

  //
  public void IgnoreCollisionsWith(CustomNetworkObject other, bool ignore = true)
  {
    Physics.IgnoreCollision(_Collider, other._Collider, ignore);
  }

  //
  protected void CheckDimensionChanged()
  {
    // Check dimension
    if (!_inDemension)
    {
      if (transform.position.z > 5.5f)
      {
        var dimensionLeft = transform.position.x < 0f;
        var dimensionId = dimensionLeft ? DimensionController.s_dimensionLeftId : DimensionController.s_dimensionRightId;
        if (dimensionId > -1)
        {
          CmdSetDimension(new SetDimensionData()
          {
            DimensionId = dimensionId,
            SetPosition = true
          });
        }
      }

    }
    else
    {
      var dimensionEntrance = DimensionController.GetDimension(_dimensionId).EntrancePosition;
      if (transform.position.z < dimensionEntrance.z)
      {
        CmdSetDimension(new SetDimensionData()
        {
          DimensionId = -1,
          SetPosition = true
        });
      }
    }
  }

  [Command(requiresAuthority = false)]
  public virtual void CmdSetDimension(SetDimensionData setDimensionData)
  {
    RpcSetDimension(setDimensionData);
  }
  [ClientRpc]
  public virtual void RpcSetDimension(SetDimensionData setDimensionData)
  {
    SetDimension(setDimensionData);
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
    return GameObject.Instantiate(Resources.Load<GameObject>($@"NetworkObjects/{objectType}")).transform;
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
