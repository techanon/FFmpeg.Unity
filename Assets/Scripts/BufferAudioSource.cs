using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BufferAudioSource : MonoBehaviour
{
    private float[] RingBuffer = null;
    private int RingBufferPosition = 0;
    private int PlaybackPosition = 0;

    private bool shouldStop = false;
    private int stopTime = 0;
    private int lastTimeSamples = 0;
    private int maxEmptyReads = 0;
    private AudioClip clip = null;
    private AudioSource audioSource;
    public float bufferDelay = 1f;

    private float[] spectrum = new float[1024];

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (clip == null)
            return;
        int currentDeltaSamples = audioSource.timeSamples - lastTimeSamples;
        if (audioSource.timeSamples < lastTimeSamples)
            currentDeltaSamples = clip.samples - audioSource.timeSamples;
        stopTime -= currentDeltaSamples;
        if (stopTime <= 0)
            shouldStop = true;
        lastTimeSamples = audioSource.timeSamples;

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.Rectangular);
        for (int i = 1; i < spectrum.Length - 1; i++)
        {
            Debug.DrawLine(new Vector3(i - 1, spectrum[i] + 10, 0), new Vector3(i, spectrum[i + 1] + 10, 0), Color.red);
            Debug.DrawLine(new Vector3(i - 1, Mathf.Log(spectrum[i - 1]) + 10, 2), new Vector3(i, Mathf.Log(spectrum[i]) + 10, 2), Color.cyan);
            Debug.DrawLine(new Vector3(Mathf.Log(i - 1), spectrum[i - 1] - 10, 1), new Vector3(Mathf.Log(i), spectrum[i] - 10, 1), Color.green);
            Debug.DrawLine(new Vector3(Mathf.Log(i - 1), Mathf.Log(spectrum[i - 1]), 3), new Vector3(Mathf.Log(i), Mathf.Log(spectrum[i]), 3), Color.blue);
        }

        if (shouldStop && audioSource.isPlaying)
        {
            // audioSource.Stop();
        }
    }

    private void PcmCallback(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = RingBuffer[PlaybackPosition];
            RingBuffer[PlaybackPosition] = 0f;
            PlaybackPosition = (PlaybackPosition + 1) % RingBuffer.Length;
        }
    }

    private void AddRingBuffer(float[] pcm)
    {
        if (RingBuffer == null)
            return;
        stopTime += pcm.Length;
        for (int i = 0; i < pcm.Length; i++)
        {
            RingBuffer[RingBufferPosition] = pcm[i];
            RingBufferPosition = (RingBufferPosition + 1) % RingBuffer.Length;
        }
    }

    public void AddQueue(float[] pcm, int channels, int frequency)
    {
        bool newClip = false;
        if (clip == null || clip.channels != channels || clip.frequency != frequency)
        {
            maxEmptyReads = (int)(frequency * channels * bufferDelay);
            newClip = true;
        }
        AddRingBuffer(pcm);
        if (newClip)
        {
            shouldStop = false;
            RingBuffer = new float[maxEmptyReads];
            clip = AudioClip.Create("BufferAudio", RingBuffer.Length, channels, frequency, true, PcmCallback);
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.Stop();
            RingBufferPosition = maxEmptyReads / 2;
            PlaybackPosition = 0;
            stopTime = RingBufferPosition + pcm.Length;
            AddRingBuffer(pcm);
            // clip.SetData(RingBuffer, 0);
            audioSource.Play();
            AudioConfiguration conf = AudioSettings.GetConfiguration();
            // audioSource.PlayDelayed((float)conf.dspBufferSize / conf.sampleRate);
        }
        if (shouldStop && !audioSource.isPlaying)
        {
            shouldStop = false;
            RingBuffer = new float[clip.samples];
            audioSource.Stop();
            RingBufferPosition = maxEmptyReads / 2;
            PlaybackPosition = 0;
            stopTime = RingBufferPosition + pcm.Length;
            AddRingBuffer(pcm);
            // clip.SetData(RingBuffer, 0);
            audioSource.Play();
            AudioConfiguration conf = AudioSettings.GetConfiguration();
            // audioSource.PlayDelayed((float)conf.dspBufferSize / conf.sampleRate);
        }
    }
}