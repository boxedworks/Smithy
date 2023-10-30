using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICanHold
{

  public CustomNetworkObject _Holdee { get; set; }
  public bool _IsHolding { get; }

  public void Hold(CustomNetworkObject other);
  public void Throw();
  public void Drop();

}
