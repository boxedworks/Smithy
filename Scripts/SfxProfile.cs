using UnityEngine;

// This attribute allows you to create a new instance of the ScriptableObject via the Unity Editor.
[CreateAssetMenu(fileName = "SfxProfile", menuName = "ScriptableObjects/SfxProfile", order = 1)]
public class SfxProfile : ScriptableObject
{
  public AudioClip[] _audioClips;
}
