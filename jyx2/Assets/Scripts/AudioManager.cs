/*
 * 金庸群侠传3D重制版
 * https://github.com/jynew/jynew
 *
 * 这是本开源项目文件头，所有代码均使用MIT协议。
 * 但游戏内资源和第三方插件、dll等请仔细阅读LICENSE相关授权协议文档。
 *
 * 金庸老先生千古！
 */

using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class AudioManager
{
    //JYX2
    public static void PlayMusic(int id)
    {
        Init();
        PlayMusicAtPath("Assets/BuildSource/Musics/" + id + ".mp3").Forget();
    }

    public static bool PlayMusic(AssetReference asset)
    {
        Init();
        if (string.IsNullOrEmpty(asset.AssetGUID))
            return false;
        DoPlayMusic(asset).Forget();
        return true;
    }

    private static async UniTask DoPlayMusic(AssetReference asset)
    {
        var audioClip = await Addressables.LoadAssetAsync<AudioClip>(asset);
        if (audioClip != null)
        {
            PlayMusic(audioClip);
        }
    }

    public static void PlayMusic(AudioClip audioClip)
    {
        Init();
        if (audioClip != bgmAudioSource.clip)
        {
            bgmAudioSource.clip = audioClip;
            bgmAudioSource.Play();    
        }
    }

    public static async UniTask PlayMusicAtPath(string path)
    {
        Init();
        if (path == null)
        {
            return;
        }

        if(_currentPlayMusic == path)
        {
            return;
        }

        var audioClip = await Addressables.LoadAssetAsync<AudioClip>(path).Task;
        if (audioClip != null)
        {
            bgmAudioSource.clip = audioClip;
            bgmAudioSource.Play();
            _currentPlayMusic = path;
        }
    }

    private static string _currentPlayMusic;

    private static AudioSource _bgmAudioSource = null;

    private static AudioSource bgmAudioSource
    {
        get
        {
            if (_bgmAudioSource == null)
            {
                GameObject obj = new GameObject("[AudioManager]");
                GameObject.DontDestroyOnLoad(obj);
                _bgmAudioSource = obj.AddComponent<AudioSource>();
                _bgmAudioSource.loop = true;
            }

            return _bgmAudioSource;
        }
    }

    private static bool _hasInitialized = false;

    private static void Init()
    {
        if (_hasInitialized)
            return;
        
        GameSettingManager.SubscribeEnforceEvent(
            GameSettingManager.Catalog.Volume, (volume) =>
            {
                bgmAudioSource.volume = (float)volume; 
            }, 
            true);
        
        _hasInitialized = true;
    }

    public static AudioClip GetCurrentMusic()
    {
        Init();
        return bgmAudioSource.clip;
    }

    public static void PlayClipAtPoint(AudioClip clip, Vector3 position)
    {
        Init();
        var soundEffectVolume = GameSettingManager.settings[GameSettingManager.Catalog.SoundEffect];
        AudioSource.PlayClipAtPoint(clip, position, (float)soundEffectVolume);
    }
}
