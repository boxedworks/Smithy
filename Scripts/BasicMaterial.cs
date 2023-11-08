using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class BasicMaterial : CustomNetworkObject, IPickupable
{

  //
  bool _spawned;
  MeshRenderer _meshRenderer;
  MeshFilter _meshFilter;
  Material[] _materials;

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

    _meshRenderer = newMesh.GetComponent<MeshRenderer>();
    _materials = new Material[] { new Material(_meshRenderer.sharedMaterials[0]) };
    _meshRenderer.sharedMaterials = _materials;

    _meshFilter = newMesh.GetComponent<MeshFilter>();

    // Init physics
    Init(_ObjectType, -1, -1);
  }

  //
  new void OnDestroy()
  {
    if (_materials != null)
    {
      for (var i = _materials.Length - 1; i >= 0; i--)
        GameObject.Destroy(_materials[i]);
      _materials = null;
    }

    //
    base.OnDestroy();
  }

  //
  public override void SetDimension(int dimension)
  {
    SetDimensionBase(dimension, ref _materials, false);

    // Visibility
    var bounds = _meshFilter.mesh.bounds;
    if (dimension > -1)
    {
      bounds.Expand(150f);
    }
    else
    {
      bounds.Expand(-150f);
    }
    _meshFilter.mesh.bounds = bounds;
  }
  public override void SetDimensionOffset(int dimension, Vector3 offset)
  {
    if (_dimensionIndex != dimension) return;

    foreach (var material in _materials)
      material.SetVector("_InclusionOffset", offset);
  }

}
