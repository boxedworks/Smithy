using System.Collections.Generic;
using UnityEngine;

// SFX
// pool sfx sounds, using a limit for certain types and priority sounds for players etc
public static class SfxManager
{

  //
  public enum AudioPriority
  {
    NORMAL,
    HIGH
  }
  struct AudioData
  {
    public AudioSource _audioSource;
    public AudioPriority _audioPriority;
    public float _volume, _pitch;
  }

  //
  static Queue<AudioSource> s_audioSourcesAvailable;
  static List<AudioData> s_audioSourcesPlaying;
  static Dictionary<AudioPriority, int> s_audioPriorityAmounts, s_audioPriorityCounts;
  static Transform s_audioContainer;

  // Create audio pool
  public static void Init()
  {
    s_audioSourcesAvailable = new Queue<AudioSource>();
    s_audioSourcesPlaying = new List<AudioData>();
    s_audioContainer = new GameObject("AudioContainer").transform;

    var numAudioSources = 50;
    for (var i = 0; i < numAudioSources; i++)
    {
      var audioGameObject = new GameObject($"audio{i}");
      audioGameObject.transform.parent = s_audioContainer;

      var audioSource = audioGameObject.AddComponent<AudioSource>();
      audioSource.playOnAwake = false;
      audioSource.spatialBlend = 0.75f;

      s_audioSourcesAvailable.Enqueue(audioSource);
    }

    s_audioPriorityAmounts = new Dictionary<AudioPriority, int>();
    s_audioPriorityCounts = new Dictionary<AudioPriority, int>();
    s_audioPriorityAmounts.Add(AudioPriority.NORMAL, -1);
    s_audioPriorityCounts.Add(AudioPriority.NORMAL, 0);
    s_audioPriorityAmounts.Add(AudioPriority.HIGH, -1);
    s_audioPriorityCounts.Add(AudioPriority.HIGH, 0);
  }

  //
  public static void Reset()
  {
    if (s_audioSourcesPlaying.Count == 0)
    {
      return;
    }

    foreach (var audioData in s_audioSourcesPlaying)
    {
      var audioSource = audioData._audioSource;
      if (audioSource.clip.name == "rain") { continue; }
      audioSource.Stop();
      if (audioSource.loop)
      {
        audioSource.loop = false;
      }
    }
  }

  //
  static AudioSource GetAudioSource(Vector3 position, AudioClip clip, float volume, float pitch)
  {

    // Check if source available
    if (s_audioSourcesAvailable.Count == 0)
    {
      Debug.Log("No more audio sources!");
      return null;
    }

    // Get a new audio source
    var audioSource = s_audioSourcesAvailable.Dequeue();
    audioSource.transform.position = position;
    audioSource.clip = clip;
    audioSource.volume = volume;
    audioSource.pitch = pitch;

    //if (audioSource.loop)
    //  audioSource.loop = false;

    return audioSource;
  }

  //
  public static AudioSource GetAudioSource(Vector3 position, AudioClip clip, AudioPriority audioPriority, bool priority, float volume, float pitch)
  {

    // Check if max audio types playing for class or priority
    if (!priority)
    {
      var classCount = s_audioPriorityCounts[audioPriority];
      var classCountMax = s_audioPriorityAmounts[audioPriority];
      if (classCountMax != -1 && classCount >= classCountMax)
      {
        return null;
      }
    }

    // Gather and return audio
    return GetAudioSource(position, clip, volume, pitch);
  }

  //
  public static void PlayAudioSource(AudioSource audioSource, AudioPriority audioPriority)
  {
    s_audioSourcesPlaying.Add(new AudioData()
    {
      _audioSource = audioSource,
      _audioPriority = audioPriority,

      _volume = audioSource.volume,
      _pitch = audioSource.pitch
    });

#if UNITY_EDITOR
    audioSource.gameObject.name = audioSource.clip.name;
#endif
    //audioSource.volume *= (Settings._VolumeSFX / 5f);
    audioSource.Play();

    s_audioPriorityCounts[audioPriority]++;
  }
  static void StopAudioSource(int listIndex)
  {
    var audioData = s_audioSourcesPlaying[listIndex];
    if (audioData._audioSource.isPlaying)
      audioData._audioSource.Stop();
    s_audioSourcesPlaying.RemoveAt(listIndex);
    s_audioSourcesAvailable.Enqueue(audioData._audioSource);
    s_audioPriorityCounts[audioData._audioPriority]--;
  }

  public static void StopAudioSource(AudioSource a)
  {
    if (a == null) return;

    a.Stop();
  }

  public static AudioSource PlayAudioSourceSimple(Vector3 position, AudioClip clip, AudioPriority audioPriority, float volume, float pitch, bool priority = false)
  {
    var audioSource = GetAudioSource(position, clip, audioPriority, priority, volume, pitch);
    if (audioSource == null)
    {
      return null;
    }

    PlayAudioSource(audioSource, audioPriority);
    return audioSource;
  }
  public static AudioSource PlayAudioSourceSimple(Vector3 position, AudioClip clip, float volume, float pitchMin = 0.9f, float pitchMax = 1.1f, AudioPriority audioPriority = AudioPriority.NORMAL, bool priority = false, bool changePitch = true)
  {
    return PlayAudioSourceSimple(position, clip, audioPriority, volume, pitchMin != pitchMax ? Random.Range(pitchMin, pitchMax) : pitchMin, priority);
  }

  public static AudioSource PlayAudioSourceSimple(this Transform transform, AudioClip clip, AudioPriority audioPriority, float volume, float pitch, bool priority = false)
  {
    return PlayAudioSourceSimple(transform.position, clip, audioPriority, volume, pitch, priority);
  }


  // Update volume and pitch of SFX for settings and slowmso
  public static void Update()
  {
    if (s_audioSourcesPlaying.Count == 0)
    {
      return;
    }
    for (var i = s_audioSourcesPlaying.Count - 1; i >= 0; i--)
    {

      var audioData = s_audioSourcesPlaying[i];
      var audioSource = audioData._audioSource;

      // Remove audio that stopped playing; set defaults back
      if (!audioSource.isPlaying)
      {
        StopAudioSource(i);
        continue;
      }

      // Update volume and pitch
      //audioSource.volume = audioData._volume;// * (Settings._VolumeSFX / 5f);
      //if (audioData._changePitch)
      //  audioSource.pitch = audioData._pitch * pitch;
    }
  }
}
