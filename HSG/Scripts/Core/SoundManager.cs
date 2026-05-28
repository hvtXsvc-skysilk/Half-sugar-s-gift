using Cpp2IL.Core.Extensions;
using Nebula.Modules;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Virial;
using Virial.Attributes;
using Vector2 = UnityEngine.Vector2;

[NebulaRPCHolder]
public static class SoundManagers
{
    private static readonly Dictionary<string, AudioClip> _cache = new();

    private static AudioClip? LoadOggFromResource(string resourcePath)
    {
        var resource = NebulaAPI.AddonAsset.GetResource(resourcePath);
        if (resource == null)
        {
            HsgDebug.Log($"[SoundManager] Resource not found: {resourcePath}");
            return null;
        }

        byte[] audioData;
        using (var stream = resource.AsStream())
        {
            if (stream == null) return null;
            audioData = stream.ReadBytes();
        }

        string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".ogg");
        try
        {
            File.WriteAllBytes(tempFile, audioData);

            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempFile, AudioType.OGGVORBIS);
            try
            {
                var op = www.SendWebRequest();
                while (!op.isDone) { }
                if (www.result == UnityWebRequest.Result.Success)
                {
                    var clip = DownloadHandlerAudioClip.GetContent(www);
                    clip.name = Path.GetFileNameWithoutExtension(resourcePath);
                    return clip;
                }
                else
                {
                    HsgDebug.Log($"[SoundManager] Load failed: {www.error}");
                    return null;
                }
            }
            finally
            {
                // 尝试释放 UnityWebRequest
                (www as IDisposable)?.Dispose();
            }
        }
        catch (Exception e)
        {
            HsgDebug.Log($"[SoundManager] Exception: {e.Message}");
            return null;
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static AudioClip? GetSound(string resourcePath)
    {
        if (_cache.TryGetValue(resourcePath, out var clip))
            return clip;
        clip = LoadOggFromResource(resourcePath);
        if (clip != null)
            _cache[resourcePath] = clip;
        return clip;
    }

    [NebulaRPC]
    public static void RpcPlayGlobal(string resourcePath, float volume = 1f)
    {
        var clip = GetSound(resourcePath);
        if (clip == null) return;
        SoundManager.Instance.PlaySound(clip, false, volume, null);
    }

    [NebulaRPC]
    public static void RpcPlayPositional(string resourcePath, Vector2 position, float volume = 1f)
    {
        var clip = GetSound(resourcePath);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    [NebulaRPC]
    public static void RpcPlayPrivate(string resourcePath, float volume = 1f)
    {
        if (PlayerControl.LocalPlayer == null) return;
        var clip = GetSound(resourcePath);
        if (clip == null) return;
        SoundManager.Instance.PlaySound(clip, false, volume, null);
    }
}