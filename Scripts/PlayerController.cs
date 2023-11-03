using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.InputSystem;

using Mirror;

public class PlayerController : CustomNetworkObject, ICanHold
{

  public static PlayerController _LocalPlayer;
  static System.Action _onLocalPlayerConnected;
  public static void OnLocalPlayerConnected(System.Action onConnected)
  {
    if (_LocalPlayer == null)
      _onLocalPlayerConnected += onConnected;
    else
      onConnected?.Invoke();
  }

  Vector2 _inputMovement, _inputMovementLast;
  Animator _playerModel;

  void Start()
  {

    //
    Init(ObjectType.ENTITY_PLAYER, 0);

    //
    _playerModel = transform.GetChild(1).GetComponent<Animator>();
    _playerModel.transform.parent = transform.parent;

    //
    _inputMovementLast = new Vector2(0f, 1f);

    //
    if (isServer)
    {

      //
      _OnNetworkCollision += (CustomNetworkObject networkObject) =>
      {
        Debug.Log($"Collided with network object: {networkObject.gameObject.name}");

        if (networkObject is IPickupable && networkObject._IsJustThrown)
          ContextualInteract(networkObject, false);
      };
    }

    // Register local player
    if (isLocalPlayer)
    {
      _LocalPlayer = this;

      _onLocalPlayerConnected?.Invoke();
      _onLocalPlayerConnected = null;
    }
  }

  // Clean up
  void OnDestroy()
  {

    // Drop on disconnect
    if (_holdData._IsHolding)
    {
      _holdData.Drop();
    }

    // Destroy model
    if (_playerModel != null)
      GameObject.Destroy(_playerModel.gameObject);
  }

  // Gather / Handle input
  Vector3 _inclusionOffset0, _inclusionOffset1;
  void Update()
  {

    if (isLocalPlayer)
    {

      // Input vars
      var inputMovement = Vector2.zero;
      var inputInteract = false;

      // Controller input
#if UNITY_EDITOR
      var gamepad = Gamepad.current;
      if (gamepad != null)
      {

        // Movement
        inputMovement = gamepad.leftStick.ReadValue();
        if (inputMovement.magnitude < 0.4f)
          inputMovement = Vector2.zero;

        // Direction
        var input2 = gamepad.rightStick.ReadValue();
        if (input2.magnitude > 0.4f)
        {

          _inclusionOffset0 += new Vector3(input2.x, 0f, input2.y) * Time.deltaTime * 5f;
          _inclusionOffset0.x = Mathf.Clamp(_inclusionOffset0.x, -9f, 0f);
          _inclusionOffset0.z = Mathf.Clamp(_inclusionOffset0.z, -0f, 0f);
          GameController.SetDimensionOffset(0, _inclusionOffset0);

          _inclusionOffset1 += new Vector3(input2.x, 0f, input2.y) * Time.deltaTime * 5f;
          _inclusionOffset1.x = Mathf.Clamp(_inclusionOffset1.x, -7f, 0f);
          _inclusionOffset1.z = Mathf.Clamp(_inclusionOffset1.z, -0f, 0f);
          GameController.SetDimensionOffset(1, _inclusionOffset1);

        }

        // Interact
        inputInteract = gamepad.buttonSouth.wasPressedThisFrame;
      }

#else

      // Keyboard input
      var keyboard = Keyboard.current;
      if (keyboard != null)
      {

        // Movement
        if (keyboard.leftArrowKey.isPressed)
          inputMovement.x += -1f;
        if (keyboard.rightArrowKey.isPressed)
          inputMovement.x += 1f;
        if (keyboard.upArrowKey.isPressed)
          inputMovement.y += 1f;
        if (keyboard.downArrowKey.isPressed)
          inputMovement.y += -1f;

        if (inputMovement.magnitude > 1f)
          inputMovement = inputMovement.normalized;

        // Interact
        inputInteract = keyboard.spaceKey.wasPressedThisFrame;
      }
#endif

      // Handle input
      if (_inputMovement != inputMovement)
      {
        if (inputMovement != Vector2.zero)
          _inputMovementLast = inputMovement;
        _inputMovement = inputMovement;

        CmdSetInput(_inputMovement);
      }

      if (inputInteract)
        CmdInteract();

    }

    // Handle model
    var modelDistance = (new Vector3(transform.position.x, 0f, transform.position.z) - new Vector3(_playerModel.transform.position.x, 0f, _playerModel.transform.position.z)) * Time.deltaTime * 11f;
    _animDistance = Mathf.Clamp(_animDistance + modelDistance.magnitude * 1.3f - Time.deltaTime * 2f, 0f, 1f);
    _playerModel.SetFloat("MovementSpeed", _animDistance);
    _playerModel.transform.position += modelDistance;
    if (isLocalPlayer)
      _playerModel.transform.rotation = Quaternion.Lerp(_playerModel.transform.rotation, Quaternion.LookRotation(new Vector3(_inputMovementLast.x, 0f, _inputMovementLast.y)), Time.deltaTime * 3f);
    else
      _playerModel.transform.rotation = Quaternion.Lerp(_playerModel.transform.rotation, _Rb.rotation, Time.deltaTime * 3f);

    // Sfx
    _footstepDistance += _animDistance * 0.04f;
    if (_footstepDistance > 1f)
    {
      _footstepDistance -= 1f;

      PlayAudioSourceAt(
        SfxType.FOOTSTEPS,
        new SfxPlayData(transform.position)
        {
          Volume = 0.6f,
          PitchLower = 0.87f,
          PitchHigher = 1.13f
        });
    }

  }
  float _animDistance, _footstepDistance;

  void FixedUpdate()
  {

    //
    if (!isServer)
      return;

    // Add force to player
    _Rb.AddForce(new Vector3(1f * _inputMovement.x, 0f, 1f * _inputMovement.y) * 17f);

    // Rotate
    if (_inputMovementLast != Vector2.zero)
      _Rb.MoveRotation(Quaternion.LookRotation(new Vector3(_inputMovementLast.x, 0f, _inputMovementLast.y)));

    // Check hold
    if (_holdData._IsHolding)
      _holdData._Holdee._Rb.MovePosition(_Rb.position + new Vector3(0f, 2.5f, 0f));

    // Check dimension
    if (!_inDemension)
    {
      if (transform.position.z > 5.5f)
      {
        _inDemension = true;

        var dimension = transform.position.x > 0f ? 1 : 0;
        SetShaderDimension(dimension);

        Debug.Log($"In demension {dimension}");
      }
    }
    else
    {

      var teleporterPosition = GameObject.Find("Teleporter").transform.position;

      if (transform.position.z < teleporterPosition.z)
      {
        _inDemension = false;
        Debug.Log("Out demension!");

        SetShaderDimension(-1);
      }
    }

  }

  bool _inDemension;
  public static Vector2 s_DimensionPos0 = new Vector2(27f, -15f), s_DimensionPos1 = new Vector2(27f, 0f);
  void SetShaderDimension(int dimension)
  {

    var meshRenderer = _playerModel.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>();
    switch (dimension)
    {

      case -1:

        var playerDiffPos = transform.position.x - (GameController._PlayerDimension == 0 ? s_DimensionPos0.x + 13f : s_DimensionPos1.x);
        var lastDimensionOffset = GameController._PlayerDimension == 0 ? _inclusionOffset0 : _inclusionOffset1;

        transform.position = _playerModel.transform.position = new Vector3(playerDiffPos, 1.2f, 4.989f) + lastDimensionOffset;

        for (var i = 0; i < meshRenderer.sharedMaterials.Length; i++)
        {
          meshRenderer.sharedMaterials[i].SetInt("_InDimensions", 0);
        }

        break;

      case 0:
      case 1:

        var levelOffset = dimension == 0 ? s_DimensionPos0 : s_DimensionPos1;
        var dimensionOffset = dimension == 0 ? _inclusionOffset0 : _inclusionOffset1;

        transform.position = _playerModel.transform.position = new Vector3(levelOffset.x + (transform.position.x + (dimension == 0 ? 13f : 0f)), 1.2f, levelOffset.y + 5.5f - 0.01f) - dimensionOffset;
        GameObject.Find("Teleporter").transform.position = new Vector3(levelOffset.x + 3f, 1.2f, levelOffset.y + 5.5f - 0.5f);

        for (var i = 0; i < meshRenderer.sharedMaterials.Length; i++)
        {
          meshRenderer.sharedMaterials[i].SetInt("_InDimensions", 1);
          meshRenderer.sharedMaterials[i].SetInt("_DimensionRight", dimension);
          meshRenderer.sharedMaterials[i].SetVector("_Offset", new Vector3(levelOffset.x, 0f, levelOffset.y));
          meshRenderer.sharedMaterials[i].SetVector("_InclusionOffset", dimensionOffset);
        }

        break;

    }

    //
    GameController._PlayerDimension = dimension;

  }

  public void SetShaderOffset(Vector3 offset)
  {
    var meshRenderer = _playerModel.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>();
    for (var i = 0; i < meshRenderer.sharedMaterials.Length; i++)
    {
      meshRenderer.sharedMaterials[i].SetVector("_InclusionOffset", offset);
    }
  }

  // Set player input on server
  [Command]
  void CmdSetInput(Vector2 input)
  {
    if (input != Vector2.zero)
      _inputMovementLast = input;
    _inputMovement = input;
  }

  //
  [Command]
  void CmdInteract()
  {

    CustomNetworkObject interactOther = null;

    // Raycast to object
    gameObject.layer = 2;

    var raycastInfo = new RaycastHit();
    if (Physics.SphereCast(new Ray(_Rb.position + new Vector3(0f, 0.4f, 0f) + transform.forward * 0.1f, transform.forward * 5f), 0.2f, out raycastInfo, 2f))
      if (GetNetworkObjectFrom(raycastInfo.collider) == null)
        raycastInfo = new();
    //Debug.DrawRay(_Rb.position + new Vector3(0f, 1.5f, 0f) + transform.forward * 0.1f, transform.forward * 5f, Color.red, 3f);

    // Recast if no found
    if (raycastInfo.collider == null)
    {
      if (!Physics.SphereCast(new Ray(_Rb.position + new Vector3(0f, 1.5f, 0f) + transform.forward * 0.1f, transform.forward * 5f), 0.2f, out raycastInfo, 2f))
        raycastInfo = new();

      //Debug.DrawRay(_Rb.position + new Vector3(0f, 0.6f, 0f) + transform.forward * 0.1f, transform.forward * 5f, Color.blue, 3f);
    }

    // Handle
    if (raycastInfo.collider != null)
    {

      //Debug.Log($"{raycastInfo.collider.name} {raycastInfo.distance}");
      //Debug.DrawLine(_Rb.position, raycastInfo.collider.transform.position, Color.magenta, 3f);

      var networkObject = GetNetworkObjectFrom(raycastInfo.collider);
      if (networkObject != null)
      {
        interactOther = networkObject;
      }

    }

    gameObject.layer = 0;

    // Try interact
    ContextualInteract(interactOther);
  }

  //
  enum NetworkEvent
  {
    NONE,

    OBJECT_PICKUP,
    OBJECT_THROW
  }
  [ClientRpc]
  void RpcHandleNetworkEvent(NetworkEvent networkFlag)
  {

    switch (networkFlag)
    {

      case NetworkEvent.OBJECT_PICKUP:

        PlayAudioSourceAt(
          SfxType.OBJECT_PICKUP,
          new SfxPlayData(transform.position)
          {
            Volume = 0.7f,
          });

        break;

      case NetworkEvent.OBJECT_THROW:

        PlayAudioSourceAt(
          SfxType.OBJECT_THROW,
          new SfxPlayData(transform.position)
          {
            Volume = 1f,
          });

        break;

    }

  }

  // Sfx wrapper
  enum SfxType
  {
    FOOTSTEPS,
    OBJECT_PICKUP,
    OBJECT_THROW,
  }
  AudioSource PlayAudioSourceAt(SfxType sfxType, SfxPlayData sfxPlayData)
  {
    return PlayAudioSourceAt((int)sfxType, sfxPlayData);
  }

  // Hold info
  ICanHold _holdData { get { return this; } }
  CustomNetworkObject ICanHold._Holdee { get; set; }
  bool ICanHold._IsHolding { get { return _holdData._Holdee != null; } }
  void ICanHold.Hold(CustomNetworkObject other)
  {
    _holdData._Holdee = other;

    other._Rb.isKinematic = true;
    other.ToggleCollider(false);

    IgnoreCollisionsWith(other);

    // Fx
    RpcHandleNetworkEvent(NetworkEvent.OBJECT_PICKUP);
  }
  public void Hold(CustomNetworkObject other)
  {
    _holdData.Hold(other);
  }

  void ICanHold.Throw()
  {
    _holdData._Holdee._Rb.isKinematic = false;
    _holdData._Holdee.ToggleCollider(true);
    _holdData._Holdee._Rb.AddForce(transform.forward * 250f);

    var thrownComponent = _holdData._Holdee.gameObject.AddComponent<ThrownPickupable>();
    thrownComponent._Self = _holdData._Holdee;
    thrownComponent._Thrower = this;

    _holdData._Holdee = null;

    // Fx
    RpcHandleNetworkEvent(NetworkEvent.OBJECT_THROW);
  }

  void ICanHold.Drop()
  {
    _holdData._Holdee.ToggleCollider(true);
    _holdData._Holdee._Rb.isKinematic = false;

    _holdData._Holdee = null;
  }

  //
  [Server]
  public void SpawnAndHold(ObjectType objectType)
  {
    var newObject = SpawnNetworkObject(objectType, new Vector3(0f, 20f, 0f));
    newObject._OnInit += () =>
    {
      Hold(newObject);
    };
  }

  // Do something depending on what is in front of player
  void ContextualInteract(CustomNetworkObject other, bool tryThrow = true)
  {

    // Throw
    if (_holdData._IsHolding)
    {
      if (tryThrow)
        _holdData.Throw();
      return;
    }

    // Interact
    if (other != null)
    {

      // Hold
      if (other is IPickupable)
      {
        Hold(other);
      }

      // Forge
      else if (other is Forge)
      {
        (other as Forge).TryRetrieveObject(this);
      }

      // Unhandled tnteraction
      else
      {
        Debug.Log($"Unhandled interaction between {_ObjectType} and {other._ObjectType}");
      }
    }
  }

}
