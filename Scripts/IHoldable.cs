using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IHoldable
{

  public CustomNetworkObject _Holdee { get; set; }
  public bool _IsHolding { get; }

  public void Hold(CustomNetworkObject other);
  public void Throw();

}
