using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FFmpeg.Unity;
using UnityEngine;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeExplode;
using YoutubeExplode.Exceptions;

public class FFTest2 : MonoBehaviour
{
    public FFPlayUnity ffmpeg;
    public string contentUrl;
    public bool stream = false;
    private int id;
    private Rect windowRect = new Rect(0, 0, 250, 300);
    private Vector2 scrollPosition;
    public MeshRenderer mesh;
    public AudioSource source;

    private void Start()
    {
        id = GetInstanceID();
        ffmpeg.texturePlayer.OnDisplay = OnDisplay;
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
        source.volume = GUILayout.HorizontalSlider(source.volume, 0f, 1f);
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
        // GUILayout.Toggle(!ffmpeg.CanSeek, "Live Stream");
        GUILayout.Label($"{ffmpeg.PlaybackTime:0.0}/{ffmpeg.GetLength():0.0} seconds");
        GUILayout.Label($"{1 / Time.deltaTime:0} FPS");
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
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
        // ffmpeg.CanSeek = !contentUrl.StartsWith("rtmp://");
        if (stream)
        {
            PlayStream(contentUrl);
            return;
        }

#if true
        Debug.Log("Start");
        if (!Directory.Exists(Application.streamingAssetsPath))
            Directory.CreateDirectory(Application.streamingAssetsPath);
        var ytdlPath = Path.Combine(Application.streamingAssetsPath, "yt-dlp.exe");
        await Utils.DownloadYtDlp(Application.streamingAssetsPath);
        var ytdl = new YoutubeDL();
        ytdl.YoutubeDLPath = ytdlPath;
        ytdl.OutputFolder = Application.streamingAssetsPath;
        var res = await ytdl.RunVideoDataFetch(contentUrl);
        if (res.Success)
        {
            if (string.IsNullOrEmpty(res.Data.Url))
            {
                var video = await ytdl.RunVideoDownload(contentUrl, "bestvideo[height<=?1080]/best");
                var audio = await ytdl.RunAudioDownload(contentUrl);
                ffmpeg.Play(video.Data, audio.Data);
            }
            else
            {
                ffmpeg.Play(res.Data.Url);
            }
        }
        else
            ffmpeg.Play(contentUrl);
        Debug.Log("Done");
#else
        // ffmpeg.CanSeek = false;
        var yt = new YoutubeClient();
        Debug.Log("Start");
        try
        {
            var video = await yt.Videos.Streams.GetManifestAsync(contentUrl);
            var ytVideoStream = video.GetVideoStreams().OrderByDescending(x => x.VideoResolution.Height /** x.VideoQuality.Framerate*/).FirstOrDefault(x => x.VideoResolution.Height <= 1080);
            var ytAudioStream = video.GetAudioStreams()/*.OrderByDescending(x => x.Bitrate)*/.FirstOrDefault();
            if (ytVideoStream == null && ytAudioStream == null)
            {
                // ffmpeg.CanSeek = !contentUrl.StartsWith("rtmp://");
                ffmpeg.Play(contentUrl, contentUrl);
                return;
            }
            // ffmpeg.CanSeek = true;
            ffmpeg.Play(ytVideoStream.Url, ytAudioStream.Url);
        }
        catch (VideoUnplayableException)
        {
            var live = await yt.Videos.Streams.GetHttpLiveStreamUrlAsync(contentUrl);
            // ffmpeg.CanSeek = false;
            ffmpeg.Play(live, live);
        }
        catch (ArgumentException)
        {
            // ffmpeg.CanSeek = !contentUrl.StartsWith("rtmp://");
            ffmpeg.Play(contentUrl, contentUrl);
        }
#endif
    }
}