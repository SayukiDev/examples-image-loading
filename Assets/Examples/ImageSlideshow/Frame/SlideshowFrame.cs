using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Image;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SlideshowFrame : UdonSharpBehaviour
{
    [SerializeField, Tooltip("URLs of images to load")]
    private VRCUrl[] imageUrls;
    
    [SerializeField, Tooltip("Renderer to show downloaded images on.")]
    private new Renderer renderer;
    
    [SerializeField, Tooltip("Duration in seconds until the next image is shown.")]
    private float slideDurationSeconds = 10f;
    
    [SerializeField, Tooltip("上書きするMainTexture以外のテクスチャのproperty name")]
    private string[] otherTexture;
    
    private int _loadedIndex = -1;
    private VRCImageDownloader _imageDownloader;
    private IUdonEventReceiver _udonEventReceiver;
    private string[] _captions = new string[0];
    private Texture2D[] _downloadedTextures;
    private bool _loading;
    
    private void Start()
    {
        // Downloaded textures will be cached in a texture array.
        _downloadedTextures = new Texture2D[imageUrls.Length];
        
        // It's important to store the VRCImageDownloader as a variable, to stop it from being garbage collected!
        _imageDownloader = new VRCImageDownloader();
        
        // To receive Image and String loading events, 'this' is casted to the type needed
        _udonEventReceiver = (IUdonEventReceiver)this;
        
        // Load the next image. Then do it again, and again, and...
        LoadNextRecursive();
    }

    public void LoadNextRecursive()
    {
        LoadNext();
        SendCustomEventDelayedSeconds(nameof(LoadNextRecursive), slideDurationSeconds);
    }

    private void setOtherTeture(Texture2D t)
    {
        for (int i = 0; i < otherTexture.Length; i++)
        {
            renderer.sharedMaterial.SetTexture(otherTexture[i], t);
        }
    }
    
    private void LoadNext()
    {
        if (_loading)
        {
            return;
        }
        // All clients share the same server time. That's used to sync the currently displayed image.
        _loadedIndex = (int)(Networking.GetServerTimeInMilliseconds() / 1000f / slideDurationSeconds) % imageUrls.Length;

        var nextTexture = _downloadedTextures[_loadedIndex];
        
        if (nextTexture != null)
        {
            // Image already downloaded! No need to download it again.
            renderer.sharedMaterial.mainTexture = nextTexture;
            setOtherTeture(nextTexture);
        }
        else
        {
            var rgbInfo = new TextureInfo();
            rgbInfo.GenerateMipMaps = true;
            _loading = true;
            _imageDownloader.DownloadImage(imageUrls[_loadedIndex], renderer.material, _udonEventReceiver, rgbInfo);
        }
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        Debug.Log($"Image loaded: {result.SizeInMemoryBytes} bytes.");
        
        _downloadedTextures[_loadedIndex] = result.Result;
        setOtherTeture(result.Result);
        _loading = false;
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        Debug.Log($"Image not loaded: {result.Error.ToString()}: {result.ErrorMessage}.");
        _loading = false;
    }

    private void OnDestroy()
    {
        _imageDownloader.Dispose();
    }
}
