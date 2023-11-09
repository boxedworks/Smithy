using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class BasicMaterial : CustomNetworkObject, IPickupable
{

  //
  bool _spawned;

  struct CustomMaterials
  {
    public MeshRenderer MeshRenderer;
    public MeshFilter MeshFilter;
    public Material[] Materials;
  }
  CustomMaterials _customMaterials;

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

    // Load mesh
    var newMesh = SpawnNetworkObjectModel(_ObjectType, transform);
    newMesh.transform.localPosition = Vector3.zero;

    _customMaterials.MeshRenderer = newMesh.GetComponent<MeshRenderer>();
    _customMaterials.MeshFilter = newMesh.GetComponent<MeshFilter>();

    _customMaterials.Materials = new Material[] { new Material(_customMaterials.MeshRenderer.sharedMaterials[0]) };
    _customMaterials.MeshRenderer.sharedMaterials = _customMaterials.Materials;

    // Init physics
    Init(_ObjectType, -1, -1);
  }

  //
  new void OnDestroy()
  {
    if (_customMaterials.Materials != null)
    {
      for (var i = _customMaterials.Materials.Length - 1; i >= 0; i--)
        GameObject.Destroy(_customMaterials.Materials[i]);
      _customMaterials.Materials = null;
    }

    //
    base.OnDestroy();
  }

  //
  public override void SetDimension(int dimension)
  {
    SetDimensionBase(dimension, ref _customMaterials.Materials, false);

    // Visibility
    var bounds = _customMaterials.MeshFilter.mesh.bounds;
    if (dimension > -1)
      bounds.Expand(150f);
    else
      bounds.Expand(-150f);
    _customMaterials.MeshFilter.mesh.bounds = bounds;
  }
  public override void SetDimensionOffset(Vector3 offset)
  {
    foreach (var material in _customMaterials.Materials)
      material.SetVector("_InclusionOffset", offset);
  }
  public override void ToggleDimension(bool toggle, bool left)
  {
    foreach (var material in _customMaterials.Materials)
    {
      material.SetInt("_InDimensions", toggle ? 1 : 0);
      material.SetInt("_DimensionRight", left ? 0 : 1);
    }
  }
  public override void SetDimensionMagic(float magic)
  {
    foreach (var material in _customMaterials.Materials)
    {
      material.SetFloat("_Magic", magic);
    }
  }

}
