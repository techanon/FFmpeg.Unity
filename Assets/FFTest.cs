using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CobaltSharp;
using FFmpeg.Unity;
using UnityEngine;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;

public class FFTest : MonoBehaviour
{
    public FFUnity ffmpeg;
    public string contentUrl;
    public bool stream = false;
    private int id;
    private Rect windowRect = new Rect(0, 0, 250, 300);
    private Vector2 scrollPosition;
    public MeshRenderer mesh;

    private void Start()
    {
        id = GetInstanceID();
        ffmpeg.OnDisplay = OnDisplay;
        Play();
    }

    private void OnDisplay(Texture2D tex)
    {
        mesh.material.mainTexture = tex;
        mesh.material.SetTexture("_EmissionMap", tex);
        mesh.UpdateGIMaterials();
    }

    private void OnGUI()
    {
        windowRect = GUI.Window(id, windowRect, OnWindow, name);
    }

    private void OnWindow(int wid)
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        {
            GUILayout.Label("URL:");
            contentUrl = GUILayout.TextField(contentUrl);
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Play"))
            {
                Play();
            }
            if (ffmpeg.IsPaused)
            {
                if (GUILayout.Button("Resume"))
                {
                    ffmpeg.Resume();
                }
            }
            else
            {
                if (GUILayout.Button("Pause"))
                {
                    ffmpeg.Pause();
                }
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Volume:");
        ffmpeg.source.volume = GUILayout.HorizontalSlider(ffmpeg.source.volume, 0f, 1f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<<"))
        {
            ffmpeg.Seek(ffmpeg.PlaybackTime - 10d);
        }
        if (GUILayout.Button("<"))
        {
            ffmpeg.Seek(ffmpeg.PlaybackTime - 5d);
        }
        if (GUILayout.Button(">"))
        {
            ffmpeg.Seek(ffmpeg.PlaybackTime + 5d);
        }
        if (GUILayout.Button(">>"))
        {
            ffmpeg.Seek(ffmpeg.PlaybackTime + 10d);
        }
        GUILayout.EndHorizontal();
        /*ffmpeg.CanSeek = !*/GUILayout.Toggle(!ffmpeg.CanSeek, "Live Stream");
        GUILayout.Label($"{1 / Time.deltaTime:0} FPS");
        GUILayout.Label($"DisplayTime: {ffmpeg?.PlaybackTime:0.0}");
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label($"Time: {ffmpeg?._elapsedOffset:0.0}");
        GUILayout.Label($"VideoTime: {ffmpeg?._elapsedOffsetVideo:0.0}");
        GUILayout.Label($"Diff: {(ffmpeg?._elapsedOffset - ffmpeg?.PlaybackTime):0.0}");
        GUILayout.Label($"DiffVideo: {(ffmpeg?._elapsedOffsetVideo - ffmpeg?.PlaybackTime):0.0}");
        GUILayout.Label($"Skipped Frames: {(ffmpeg?.skippedFrames)}");
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
    }

    public void Play(string url)
    {
        HttpClient client = new HttpClient();
        HttpRequestMessage requestVideo = new HttpRequestMessage(HttpMethod.Get, url);
        HttpContent contentVideo = client.SendAsync(requestVideo, HttpCompletionOption.ResponseHeadersRead).Result.Content;
        try
        {
            Stream video = contentVideo.ReadAsStreamAsync().Result;
            ffmpeg.Play(video, video);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void PlayStream(string url)
    {
        ffmpeg.Play(url, url);
    }

    [ContextMenu(nameof(Play))]
    public async void Play()
    {
        if (string.IsNullOrEmpty(contentUrl))
            return;
        ffmpeg.CanSeek = !contentUrl.StartsWith("rtmp://");
        if (stream)
        {
            PlayStream(contentUrl);
            return;
        }

        ffmpeg.CanSeek = false;
        var yt = new YoutubeClient();
        Debug.Log("Start");
        // var video = await yt.Videos.GetAsync(contentUrl);
        // Debug.Log(video.Url);
        try
        {
            var video = await yt.Videos.Streams.GetManifestAsync(contentUrl);
            var ytVideoStream = video.GetVideoStreams().OrderByDescending(x => x.VideoResolution.Height /** x.VideoQuality.Framerate*/).FirstOrDefault(x => x.VideoResolution.Height <= 1080);
            var ytAudioStream = video.GetAudioStreams()/*.OrderByDescending(x => x.Bitrate)*/.FirstOrDefault();
            if (ytVideoStream == null && ytAudioStream == null)
            {
                ffmpeg.CanSeek = !contentUrl.StartsWith("rtmp://");
                ffmpeg.Play(contentUrl, contentUrl);
                return;
            }
            ffmpeg.CanSeek = true;
            ffmpeg.Play(ytVideoStream.Url, ytAudioStream.Url);
        }
        catch (VideoUnplayableException)
        {
            var live = await yt.Videos.Streams.GetHttpLiveStreamUrlAsync(contentUrl);
            ffmpeg.CanSeek = false;
            ffmpeg.Play(live, live);
        }
        catch (ArgumentException)
        {
            ffmpeg.CanSeek = !contentUrl.StartsWith("rtmp://");
            ffmpeg.Play(contentUrl, contentUrl);
        }
        return;

        Cobalt cobalt = new Cobalt();
        GetMedia getMedia = new GetMedia()
        {
            url = contentUrl,
            vQuality = VideoQuality.q360,
        };
        MediaResponse mediaResponse = cobalt.GetMedia(getMedia);
        if (mediaResponse.status == Status.Error)
        {
            PlayStream(contentUrl);
            return;
        }
        if (mediaResponse.status == Status.Picker)
        {
            foreach (PickerItem item in mediaResponse.picker)
            {
                Debug.Log(item.url);
            }
        }
        else
        {
            Play(mediaResponse.url);
        }
    }
}
