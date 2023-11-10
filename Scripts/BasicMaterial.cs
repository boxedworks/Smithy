using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using Unity.VisualScripting;

public class BasicMaterial : CustomNetworkObject, IPickupable
{

  //
  bool _spawned;

  struct CustomMaterials
  {
    public MeshRenderer MeshRenderer;
    public MeshFilter MeshFilter;
  }
  CustomMaterials _customMaterials;

  //
  public override void OnStartClient()
  {
    SyncServerData();
  }

  //
  Transform _newMesh;
  protected override void Spawn()
  {

    if (_spawned)
      return;
    _spawned = true;

    // Load mesh
    var meshData = SpawnNetworkObjectModel(_ObjectType, transform);
    _newMesh = meshData.GetChild(0);

    var collider = meshData.GetChild(1);
    collider.parent = transform;
    collider.localPosition = Vector3.zero;

    if (isServer)
    {
      _newMesh.parent = transform;
      _newMesh.localPosition = Vector3.zero;
    }
    else
    {
      _newMesh.parent = transform.parent;
    }
    GameObject.Destroy(meshData.gameObject);

    _customMaterials.MeshRenderer = _newMesh.GetComponent<MeshRenderer>();
    _customMaterials.MeshFilter = _newMesh.GetComponent<MeshFilter>();

    _dimensionMaterials = new Material[] { new Material(_customMaterials.MeshRenderer.sharedMaterials[0]) };
    _customMaterials.MeshRenderer.sharedMaterials = _dimensionMaterials;

    // Init physics
    Init(_ObjectType, -1, -1);
  }

  void Update()
  {
    //
    if (isServer && !_PickupData._PickedUp)
      CheckDimensionChanged();
  }

  Vector3 _lastPosition;
  void LateUpdate()
  {

    if (!_spawned || isServer) return;

    var distance = transform.position - _lastPosition;
    if (distance.magnitude > 10f)
    {

      if (_queuedDimension != -2)
      {
        var dimensionId = _queuedDimension;
        SetDimensionBase(new SetDimensionData()
        {
          DimensionId = dimensionId,
          SetPosition = false
        });

        // Visibility
        var bounds = _customMaterials.MeshFilter.mesh.bounds;
        if (dimensionId > -1)
          bounds.Expand(150f);
        else
          bounds.Expand(-150f);
        _customMaterials.MeshFilter.mesh.bounds = bounds;

        _newMesh.position = transform.position;
        _newMesh.rotation = transform.rotation;

        _lastPosition = transform.position;

        _queuedDimension = -2;
      }
    }
    else
    {
      _newMesh.position = transform.position;
      _newMesh.rotation = transform.rotation;

      _lastPosition = transform.position;
    }
  }

  void FixedUpdate()
  {

    //
    if (!isServer)
      return;

    // Check fall
    if (transform.position.y < -10f)
    {

      if (_inDemension)
        CmdSetDimension(new SetDimensionData()
        {
          DimensionId = -1,
          SetPosition = false
        });

      transform.position = new Vector3(0f, 10f, 0f);
      return;
    }
  }

  //
  new void OnDestroy()
  {
    if (_dimensionMaterials != null)
    {
      for (var i = _dimensionMaterials.Length - 1; i >= 0; i--)
        GameObject.Destroy(_dimensionMaterials[i]);
      _dimensionMaterials = null;
    }

    //
    base.OnDestroy();
  }

  //
  int _queuedDimension = -2;
  public override void SetDimension(SetDimensionData setDimensionData)
  {

    if (!isServer)
    {
      _queuedDimension = setDimensionData.DimensionId;
      return;
    }

    var dimensionId = setDimensionData.DimensionId;
    SetDimensionBase(setDimensionData);

    // Visibility
    var bounds = _customMaterials.MeshFilter.mesh.bounds;
    if (dimensionId > -1)
      bounds.Expand(150f);
    else
      bounds.Expand(-150f);
    _customMaterials.MeshFilter.mesh.bounds = bounds;
  }


  // Pickup info
  public IPickupable _PickupData { get { return this; } }
  bool IPickupable._PickedUp { get; set; }

}
