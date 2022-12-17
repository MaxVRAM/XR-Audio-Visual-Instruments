using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioUtils
{    
    public static float FreqToMel(float freq)
    {
        // ref: https://en.wikipedia.org/wiki/Mel_scale
        float mel = 2595 * Mathf.Log10(1 + freq / 700);
        return mel;
    }

    public static float MelToFreq(float mel)
    {
        // ref: https://en.wikipedia.org/wiki/Mel_scale
        float freq= 700 * ( Mathf.Pow(10, mel / 2595) - 1);
        return freq;
    }

    public static float FreqToNorm(float freq)
    {
        float norm = 2595 * Mathf.Log10(1 + freq / 700) / 3800;
        return Mathf.Clamp(norm, 0, 1);
    }

    public static float NormToFreq(float norm)
    {
        float freq = 700 * (Mathf.Pow(10, ( norm * 3800 ) / 2595) - 1);
        return Mathf.Clamp(freq, 20, 20000);
    }

    // TODO: Check if this is proportionally correct
    public static float SpeakerOffsetFactor(Vector3 target, Vector3 listener, Vector3 speaker)
    {
        float speakerDist = Mathf.Abs((listener - speaker).magnitude);
        float targetDist = Mathf.Abs((listener - target).magnitude);
        return speakerDist / targetDist;
    }

    // Inverse square attenuation for audio sources based on distance from the listener
    public static float ListenerDistanceVolume(Vector3 source, Vector3 target, float maxDistance)
    {
        float sourceDistance = Mathf.Clamp(Mathf.Abs((source - target).magnitude) / maxDistance, 0f, 1f);
        return Mathf.Clamp(Mathf.Pow(500, -0.5f * sourceDistance), 0f, 1f);
    }
    
    public static float ListenerDistanceVolume(float distance, float maxDistance)
    {
        float normalisedDistance = Mathf.Clamp(distance / maxDistance, 0f, 1f);
        return Mathf.Clamp(Mathf.Pow(500, -0.5f * normalisedDistance), 0f, 1f);
    }
    public static float ListenerDistanceVolume(float normalisedDistance)
    {
        normalisedDistance = Mathf.Clamp(normalisedDistance, 0f, 1f);
        return Mathf.Clamp(Mathf.Pow(500, -0.5f * normalisedDistance), 0f, 1f);
    }
}
