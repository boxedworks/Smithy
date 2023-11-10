using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public interface IPickupable
{

  public enum PickupType
  {
    NONE,


  }
  public bool _PickedUp { get; set; }


}
