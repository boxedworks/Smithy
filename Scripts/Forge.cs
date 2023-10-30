using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class Forge : CustomNetworkObject
{

  // All network data for the forge
  struct ForgeData
  {

    // Holds ingredients
    public List<StoredForgeData> StoredObjects;

    // Cook time
    public float CookTimeStart, CookTimeEnd;

    // Holds finished product
    public ObjectType ObjectResult;
  }
  ForgeData _forgeData;

  // Data about ingredients
  struct StoredForgeData
  {
    public ObjectType ObjectType;

    public Transform DisplayModel;
  }

  // Local
  UnityEngine.UI.Slider _slider;

  ParticleSystem _particlesSmoke;
  AudioSource _audioForge;

  // Start is called before the first frame update
  void Start()
  {
    Init(ObjectType.BLOCK_FORGE);

    _slider = GameController.SliderManager.GetSlider();
    SetSliderPos();

    _particlesSmoke = transform.GetChild(2).GetComponent<ParticleSystem>();

    //
    _forgeData = new()
    {
      StoredObjects = new()
    };
    if (isServer)
    {

      _OnNetworkCollision += (CustomNetworkObject networkObject) =>
      {

        // Make sure is material
        if (!(networkObject is BasicMaterial) || !networkObject._IsJustThrown)
          return;

        //
        if (networkObject._ObjectType != ObjectType.OBJECT_ORE_IRON)
          return;

        // Store object
        if (_forgeData.StoredObjects.Count < 2)
        {
          RpcStoreObject(networkObject._ObjectType, networkObject.transform.position);
          networkObject.NetworkDestroy();
        }
      };
    }

    // Sync network data if not server
    else
    {
      PlayerController.OnLocalPlayerConnected(() =>
      {
        RequestSync(PlayerController._LocalPlayer.connectionToClient);
      });
    }
  }

  //
  void OnDestroy()
  {
    if (isClient)
      GameController.SliderManager.RemoveSlider(_slider);
  }

  //
  [Command(requiresAuthority = false)]
  void RequestSync(NetworkConnectionToClient networkConnection)
  {
    TargetSyncData(networkConnection, _forgeData);
  }
  [TargetRpc]
  void TargetSyncData(NetworkConnectionToClient target, ForgeData forgeData)
  {

    _forgeData = forgeData;
    _forgeData.StoredObjects = new();

    // Sync contents
    foreach (var forgeDat in forgeData.StoredObjects)
      StoreObject(forgeDat.ObjectType);

    // Sync slider
    if (_forgeData.ObjectResult != ObjectType.NONE)
      _slider.value = 1f;
  }

  // Update is called once per frame
  void Update()
  {

    // Check cook timer
    if (_forgeData.CookTimeStart != 0f)
    {

      var normalizedTime = 1f;

      // Cooking
      var cooking = false;
      if ((float)NetworkTime.time < _forgeData.CookTimeEnd)
      {
        cooking = true;

        normalizedTime = 1f + ((float)NetworkTime.time - _forgeData.CookTimeEnd) / (_forgeData.CookTimeEnd - _forgeData.CookTimeStart);
      }

      // Done!
      else
      {
        OnCooked();
      }

      // Cook fx
      _slider.value = normalizedTime;
      if (cooking)
      {
        if (!_particlesSmoke.isPlaying)
        {
          _particlesSmoke.Play();

          if (_audioForge == null)
          {
            _audioForge = SfxManager.PlayAudioSourceSimple(
              transform.position,
              GameController.s_Singleton._SfxProfiles[1]._audioClips[0],
              0.6f,
              0.95f, 1.05f,
              SfxManager.AudioPriority.NORMAL
            );
            _audioForge.loop = true;
          }
          else
            _audioForge.Play();
        }
      }
      else
      {
        if (_particlesSmoke.isPlaying)
        {
          _particlesSmoke.Stop();
          _audioForge?.Stop();
          _audioForge = null;
        }
      }
    }

    // Transform slider
    SetSliderPos();
  }

  //
  [Server]
  void OnFull()
  {
    // Rpc cook time to clients
    RpcSetCookTime((float)NetworkTime.time + 10f);
  }
  [ClientRpc]
  void RpcSetCookTime(float endTime)
  {
    _forgeData.CookTimeStart = (float)NetworkTime.time;
    _forgeData.CookTimeEnd = endTime;
  }

  //
  [Server]
  void OnCooked()
  {
    _forgeData.ObjectResult = ObjectType.OBJECT_BAR_IRON;

    RpcOnCooked();

    // Spawn replacement (temp)
    SpawnNetworkObject(ObjectType.OBJECT_ORE_IRON, new Vector3(0f, 20f, 0f));
    SpawnNetworkObject(ObjectType.OBJECT_ORE_IRON, new Vector3(0f, 20f, 0f));
  }
  [ClientRpc]
  void RpcOnCooked()
  {
    _forgeData.CookTimeStart = _forgeData.CookTimeEnd = 0f;
  }

  //
  [Server]
  public void TryRetrieveObject(PlayerController player)
  {

    // Check done cooking (can retrieve before done to get materials back??)
    if (_forgeData.ObjectResult == ObjectType.NONE)
    {

      if (_forgeData.StoredObjects.Count == 0) return;

      // Spawn ingredient and remove from forge
      var ingredientSpawn = _forgeData.StoredObjects[0].ObjectType;
      if (_forgeData.StoredObjects.Count == 2)
      {
        ingredientSpawn = _forgeData.StoredObjects[1].ObjectType;
        RpcRemoveObject(1);
      }
      else
      {
        RpcRemoveObject(0);
      }
      player.SpawnAndHold(ingredientSpawn);

      _forgeData.CookTimeStart = _forgeData.CookTimeEnd = 0;

      return;
    }

    // Spawn result and give to player
    player.SpawnAndHold(_forgeData.ObjectResult);

    // Reset
    RpcCleanup();
    _forgeData.ObjectResult = ObjectType.NONE;
  }

  // Reset forge to default state
  [ClientRpc]
  void RpcCleanup()
  {
    _slider.value = 0f;

    foreach (var forgeData in _forgeData.StoredObjects)
      GameObject.Destroy(forgeData.DisplayModel.gameObject);
    _forgeData.StoredObjects = new();
  }

  // Update all clients' forges FX when storing an object
  [ClientRpc]
  void RpcStoreObject(ObjectType objectType, Vector3 fxPosition)
  {

    StoreObject(objectType);

    // FX
    GameController.ParticleManager.PlayParticlesAt(fxPosition, GameController.ParticleManager.ParticleType.CLOUD_SIMPLE);

    //
    if (isServer)
      OnStoreObject();
  }
  void StoreObject(ObjectType objectType)
  {
    _forgeData.StoredObjects.Add(new StoredForgeData()
    {
      ObjectType = objectType,

      DisplayModel = SpawnNetworkObjectModel(objectType, transform)
    });

    // Steal mesh for UI display
    var meshSteal = _forgeData.StoredObjects[^1].DisplayModel;
    meshSteal.position = transform.position + new Vector3(_forgeData.StoredObjects.Count == 1 ? -0.6f : 0.6f, 1.5f, -0.5f);
    meshSteal.rotation = Quaternion.identity;
  }

  //
  [ClientRpc]
  void RpcRemoveObject(int index)
  {
    GameObject.Destroy(_forgeData.StoredObjects[index].DisplayModel.gameObject);
    _forgeData.StoredObjects.RemoveAt(index);

    _forgeData.CookTimeStart = _forgeData.CookTimeEnd = 0;
    _slider.value = 0;

    if (_particlesSmoke.isPlaying)
    {
      _particlesSmoke.Stop();
      _audioForge?.Stop();
      _audioForge = null;
    }
  }

  //
  [Server]
  void OnStoreObject()
  {
    // Check full
    if (_forgeData.StoredObjects.Count == 2)
    {
      OnFull();
    }
  }

  //
  void SetSliderPos()
  {
    _slider.transform.position = transform.position + new Vector3(0f, 0.8f, -0.5f);
  }

  //

}
