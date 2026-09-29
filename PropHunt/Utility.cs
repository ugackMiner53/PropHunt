using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Reactor.Utilities;
using UnityEngine;

namespace PropHunt;

public static class Utility
{
    public static Console FindClosestConsole(GameObject origin, float radius)
    {
        if (ShipStatus.Instance == null) return null;
        Console bestConsole = null;
        float bestDist = 9999;
        foreach (Console console in ShipStatus.Instance.AllConsoles)
        {
            if (console == null) continue;
            float dist = Vector2.Distance(origin.transform.position, console.transform.position);
            if (dist <= radius && dist < bestDist)
            {
                bestConsole = console;
                bestDist = dist;
            }
        }
        return bestConsole;
    }

    public static System.Collections.IEnumerator KillConsoleAnimation()
    {
        if (Constants.ShouldPlaySfx())
        {
            SoundManager.Instance.PlaySound(ShipStatus.Instance.SabotageSound, false, 0.8f);
            HudManager.Instance.FullScreen.color = new Color(1f, 0f, 0f, 0.375f);
            HudManager.Instance.FullScreen.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            HudManager.Instance.FullScreen.gameObject.SetActive(false);
        }
        yield break;
    }

    public static unsafe Texture2D LoadTextureFromPath(string path)
    {
        try
        {
            Texture2D texture = new(2, 2, TextureFormat.ARGB32, true);
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            long length = stream.Length;
            Il2CppStructArray<byte> textureBytes = new Il2CppStructArray<byte>(length);
            stream.Read(new Span<byte>(IntPtr.Add(textureBytes.Pointer, IntPtr.Size * 4).ToPointer(), (int)length));
            ImageConversion.LoadImage(texture, textureBytes, false);
            Logger<PropHuntPlugin>.Info("Correctly loaded " + path);
            return texture;
        }
        catch
        {
            Logger<PropHuntPlugin>.Error("Failed loading " + path);
        }
        return null;
    }

    public static readonly Dictionary<string, Sprite> _spriteCache = new();
    public static Sprite LoadSprite(string path, float ppu)
    {
        if (_spriteCache.TryGetValue(path, out var c)) return c;
        try
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            if (s == null) return null;
            var t = new Texture2D(0, 0, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            using var m = new System.IO.MemoryStream(); s.CopyTo(m);
            t.LoadImage(m.ToArray(), false);
            var sp = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu);
            sp.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            _spriteCache[path] = sp; return sp;
        }
        catch { return null; }
    }
}