using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class RainSystem : MonoBehaviour
{
    [SerializeField] private List<ParticleSystem> rainParticleSystems;
    [SerializeField] private Light dayLight;
    [SerializeField] private Color dayLightColor;
    [SerializeField] private Color rainLightColor;
    [SerializeField] private Light lightningLight;
    [SerializeField] private AudioSource rainAudioSource;
    [SerializeField] private AudioSource thunderAudioSource;
    [SerializeField] private List<AudioClip> thunderSounds;
    [SerializeField] public float colorTransitionDuration = 2.0f;
    [SerializeField] public float soundTransitionDuration = 2.0f;

    // Improvement 2: Initialize coroutines to null in their declaration
    private Coroutine thunderCoroutine = null;
    private Coroutine colorTransitionCoroutine = null;
    private Coroutine rainIntensityCoroutine = null;
    private Coroutine rainVolumeCoroutine = null;
    private Coroutine thunderVolumeCoroutine = null;

    private float targetRainVolume, targetThunderVolume;
    private ParticleSystem.MinMaxCurve targetRainIntensity;

    // Improvement 3: Constants for magic numbers
    private const float MinThunderInterval = 5f;
    private const float MaxThunderInterval = 15f;
    private const float LightningFlashDuration = 0.1f;
    private const float LightningFlashInterval = 0.1f;
    private const int MinLightningFlashes = 2;
    private const int MaxLightningFlashes = 4;

    private void Start()
    {
        // Improvement 5: Add null checks
        if (dayLight != null) dayLight.color = dayLightColor;
        if (rainAudioSource != null) targetRainVolume = rainAudioSource.volume;
        if (thunderAudioSource != null) targetThunderVolume = thunderAudioSource.volume;
        
        if (rainParticleSystems != null && rainParticleSystems.Count > 0)
        {
            targetRainIntensity = rainParticleSystems[0].emission.rateOverTime;
        }
        
        if (lightningLight != null) lightningLight.enabled = false;
        StopAllRainParticleSystems();
    }

    
    [ContextMenu("Start Rain")]
    public void StartRain()
    {
        // Improvement 5: Add null checks
        if (rainParticleSystems != null)
        {
            foreach (var rainParticleSystem in rainParticleSystems)
            {
                var emission = rainParticleSystem.emission;
                emission.rateOverTime = 0;
                rainParticleSystem.Play();
            }
        }

        if (colorTransitionCoroutine != null && dayLight != null)
        {
            StopCoroutine(colorTransitionCoroutine);
        }
        colorTransitionCoroutine = StartCoroutine(ChangeLightColor(dayLight, dayLight.color, rainLightColor, colorTransitionDuration));

        if (rainIntensityCoroutine != null)
        {
            StopCoroutine(rainIntensityCoroutine);
        }
        rainIntensityCoroutine = StartCoroutine(ChangeRainIntensity(targetRainIntensity, colorTransitionDuration));

        if (rainVolumeCoroutine != null && rainAudioSource != null)
        {
            StopCoroutine(rainVolumeCoroutine);
        }
        rainVolumeCoroutine = StartCoroutine(ChangeVolume(rainAudioSource, targetRainVolume, colorTransitionDuration * 2));
        rainAudioSource.Play();

        if (thunderVolumeCoroutine != null && thunderAudioSource != null)
        {
            StopCoroutine(thunderVolumeCoroutine);
        }
        thunderVolumeCoroutine = StartCoroutine(ChangeVolume(thunderAudioSource, targetThunderVolume, colorTransitionDuration * 2));
        

        if (lightningLight != null) lightningLight.enabled = false;
        
        if (thunderCoroutine == null)
        {
            thunderCoroutine = StartCoroutine(PlayThunder());
        }
    }

    
    [ContextMenu("Stop Rain")]
    public void StopRain()
    {
        if (colorTransitionCoroutine != null && dayLight != null)
        {
            StopCoroutine(colorTransitionCoroutine);
        }
        colorTransitionCoroutine = StartCoroutine(ChangeLightColor(dayLight, dayLight.color, dayLightColor, colorTransitionDuration));
        

        if (rainIntensityCoroutine != null)
        {
            StopCoroutine(rainIntensityCoroutine);
        }
        rainIntensityCoroutine = StartCoroutine(ChangeRainIntensity(0f, colorTransitionDuration));

        if (rainVolumeCoroutine != null && rainAudioSource != null)
        {

            StopCoroutine(rainVolumeCoroutine);
        }
        rainVolumeCoroutine = StartCoroutine(ChangeVolume(rainAudioSource, 0.0f, soundTransitionDuration));


        if (thunderVolumeCoroutine != null && thunderAudioSource != null)
        {
            StopCoroutine(thunderVolumeCoroutine);
        }
        thunderVolumeCoroutine = StartCoroutine(ChangeVolume(thunderAudioSource, 0.0f, soundTransitionDuration));
        

        if (thunderCoroutine != null)
        {
            StopCoroutine(thunderCoroutine);
            thunderCoroutine = null;
        }
        
        if (lightningLight != null) lightningLight.enabled = false;
    }

    private IEnumerator ChangeLightColor(Light light, Color startColor, Color endColor, float duration)
    {
        var elapsed = 0.0f;
        while (elapsed < duration)
        {
            var t = elapsed / duration;
            light.color = Color.Lerp(startColor, endColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        light.color = endColor;
    }

    private IEnumerator ChangeRainIntensity(ParticleSystem.MinMaxCurve targetRate, float duration)
    {
        if (rainParticleSystems == null || rainParticleSystems.Count == 0) yield break;

        var elapsed = 0.0f;
        var initialRates = new List<float>();
        
        foreach (var rainParticleSystem in rainParticleSystems)
        {
            initialRates.Add(rainParticleSystem.emission.rateOverTime.constant);
        }

        var targetRatef = targetRate.constant;

        while (elapsed < duration)
        {
            var t = elapsed / duration;
            for (var i = 0; i < rainParticleSystems.Count; i++)
            {
                var emission = rainParticleSystems[i].emission;
                emission.rateOverTime = Mathf.Lerp(initialRates[i], targetRatef, t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (var rainParticleSystem in rainParticleSystems)
        {
            var emission = rainParticleSystem.emission;
            emission.rateOverTime = targetRate;
            
            if (targetRate.constant == 0)
            {
                rainParticleSystem.Stop();
            }
        }
    }

    private IEnumerator ChangeVolume(AudioSource audioSource, float targetVolume, float duration)
    {
        if (audioSource == null) yield break;

        var startVolume = audioSource.volume;
        var elapsed = 0.0f;

        while (elapsed < duration)
        {
            var t = elapsed / duration;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        audioSource.volume = targetVolume;

        if (targetVolume == 0)
        {
            audioSource.Stop();
        }
    }

    private IEnumerator PlayThunder()
    {
        while (AnyRainParticleSystemPlaying())
        {
            yield return new WaitForSeconds(Random.Range(MinThunderInterval, MaxThunderInterval));
            
            var flashCount = Random.Range(MinLightningFlashes, MaxLightningFlashes);
            for (int i = 0; i < flashCount; i++)
            {
                if (lightningLight == null) continue;
                
                lightningLight.enabled = true;
                yield return new WaitForSeconds(LightningFlashDuration);
                lightningLight.enabled = false;
                yield return new WaitForSeconds(LightningFlashInterval);
            }

            if (thunderSounds == null || thunderSounds.Count <= 0 || thunderAudioSource == null) continue;
            
            thunderAudioSource.clip = thunderSounds[Random.Range(0, thunderSounds.Count)];
            thunderAudioSource.Play();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void StopAllRainParticleSystems()
    {
        if (rainParticleSystems == null) return;
        
        foreach (var rainParticleSystem in rainParticleSystems)
        {
            rainParticleSystem.Stop();
        }
    }

    private bool AnyRainParticleSystemPlaying()
    {
        if (rainParticleSystems == null) return false;
        
        foreach (var rainParticleSystem in rainParticleSystems)
        {
            if ( rainParticleSystem.isPlaying)
            {
                return true;
            }
        }
        return false;
    }
}