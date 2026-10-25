using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NeonHive;

/// <summary>Headless scene setup + WebGL build. Usage:
/// Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
/// (no -quit; this calls EditorApplication.Exit). NH_DEV=1 makes a development build in Builds/WebGLDev.</summary>
public static class BuildWeb {
  const string ScenePath = "Assets/Scenes/Main.unity", Gen = "Assets/Generated";

  static T Load<T>(string path) where T : Object { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (!a) Debug.LogWarning("[Setup] missing " + path); return a; }
  static Sprite[] Frames(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();

  static UniversalRenderPipelineAsset EnsureURP(){
    var path = Gen + "/URP_Asset.asset";
    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
    if (!asset){
      var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
      AssetDatabase.CreateAsset(rd, Gen + "/URP_Renderer.asset");
      asset = UniversalRenderPipelineAsset.Create(rd);
      AssetDatabase.CreateAsset(asset, path);
    }
    asset.supportsHDR = true; EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
    GraphicsSettings.defaultRenderPipeline = asset;
    for (int i = 0; i < QualitySettings.names.Length; i++){ QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
    return asset;
  }
  static VolumeProfile EnsureBloom(){
    var path = Gen + "/NeonVolume.asset";
    var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    if (!p){ p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
    if (!p.TryGet<Bloom>(out var b)){ b = p.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(b, p); }
    b.active = true;
    b.threshold.Override(0.75f); b.intensity.Override(1.6f); b.scatter.Override(0.7f); b.highQualityFiltering.Override(false);
    EditorUtility.SetDirty(b); EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
    return p;
  }
  static Material Mat(string name, Texture tex, Color c, float useVC, float fadeStart, float fadeEnd){
    var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    var sh = Shader.Find("NeonHive/Additive");
    if (!m){ m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
    m.shader = sh; m.SetTexture("_MainTex", tex); m.SetColor("_Color", c); m.SetFloat("_UseVC", useVC);
    m.SetFloat("_FadeStart", fadeStart); m.SetFloat("_FadeEnd", fadeEnd); EditorUtility.SetDirty(m); return m;
  }

  [MenuItem("Neon Hive/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory(Gen); Directory.CreateDirectory("Assets/Scenes");
    EnsureURP(); var profile = EnsureBloom();
    var ringMesh = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tunnel/ion_drift_ring.obj").OfType<Mesh>().FirstOrDefault();
    if (!ringMesh) throw new System.Exception("ring mesh missing: Assets/Art/Tunnel/ion_drift_ring.obj");
    var ringMat = Mat("RingMat", Load<Texture2D>("Assets/Art/Tunnel/ion_drift_wall.png"), Color.white, 0f, 45f, 95f);
    var spriteMat = Mat("SpriteGlow", Texture2D.whiteTexture, new Color(2.2f, 2.2f, 2.2f, 1f), 1f, 1000f, 2000f);
    AssetDatabase.SaveAssets();

    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black; RenderSettings.fog = false;
    var bg = new Color32(0x04, 0x03, 0x0A, 255);

    var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
    var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg;
    cam.fieldOfView = 70; cam.nearClipPlane = 0.2f; cam.farClipPlane = 140; cam.allowHDR = true;
    var cd = camGo.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.renderShadows = false;
    camGo.AddComponent<AudioListener>();
    var vol = new GameObject("PostFX").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile;

    var tunnel = new GameObject("Tunnel").AddComponent<Tunnel>(); tunnel.ringMesh = ringMesh; tunnel.ringMat = ringMat;

    var g = new GameObject("Game").AddComponent<Game>();
    g.tunnel = tunnel; g.cam = cam; g.spriteMat = spriteMat;
    g.shipSprite = Load<Sprite>("Assets/Art/Ship/player_ship.png"); g.miteSprite = Load<Sprite>("Assets/Art/Enemies/bug_mite.png");
    g.bulletSprite = Load<Sprite>("Assets/Art/FX/fx_bullet.png");
    g.explosion = Frames("Assets/Art/FX/fx_explosion_sheet.png"); g.spark = Frames("Assets/Art/FX/fx_hit_spark_sheet.png");
    g.iconLife = Load<Texture2D>("Assets/Art/UI/ui_icon_life.png"); g.iconScore = Load<Texture2D>("Assets/Art/UI/ui_icon_score.png");

    var ga = new GameObject("GameAudio").AddComponent<GameAudio>();
    ga.music = Load<AudioClip>("Assets/Audio/Music/theme1_loop.ogg");
    ga.shot = Load<AudioClip>("Assets/Audio/SFX/player_shot.ogg"); ga.bugHit = Load<AudioClip>("Assets/Audio/SFX/bug_hit.ogg");
    ga.bugExplode = Load<AudioClip>("Assets/Audio/SFX/bug_explode.ogg"); ga.playerHit = Load<AudioClip>("Assets/Audio/SFX/player_hit.ogg");
    ga.lifeLost = Load<AudioClip>("Assets/Audio/SFX/life_lost.ogg");

    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[]{ new EditorBuildSettingsScene(ScenePath, true) };
    Debug.Log($"[Setup] scene saved; explosion frames={g.explosion.Length} spark frames={g.spark.Length} ringMesh verts={ringMesh.vertexCount}");
  }

  /// <summary>Page hardening (same as Tidewatch / Free Flight): canvas touch-action:none + preventDefault on touch events,
  /// no context menu on the canvas, Space / arrow keys don't scroll the page. Unity still receives all events.</summary>
  static void PatchIndexHtml(string outDir){
    var p = Path.Combine(outDir, "index.html"); if (!File.Exists(p)) return;
    var html = File.ReadAllText(p); if (html.Contains("nh-input-guard")) return;
    const string css = "<style id=\"nh-input-guard-css\">html,body{overscroll-behavior:none;background:#04030A}#unity-canvas{touch-action:none;-ms-touch-action:none;user-select:none;-webkit-user-select:none;-webkit-touch-callout:none;-webkit-tap-highlight-color:transparent;outline:none}</style>";
    const string js = @"<script id=""nh-input-guard"">(function(){
  var c=document.getElementById('unity-canvas'); if(!c) return;
  var stop=function(e){ if(e.cancelable) e.preventDefault(); };
  ['touchstart','touchmove','touchend','touchcancel'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  ['gesturestart','gesturechange','gestureend','dblclick','contextmenu'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  document.addEventListener('contextmenu',function(e){ if(e.target===c) e.preventDefault(); });
  window.addEventListener('keydown',function(e){
    var k=e.key||'', tag=(e.target&&e.target.tagName)||'';
    if((k===' '||k==='Spacebar'||k.indexOf('Arrow')===0) && !/INPUT|TEXTAREA|SELECT/.test(tag)) e.preventDefault();
  },{passive:false});
  if(!c.hasAttribute('tabindex')) c.setAttribute('tabindex','-1');
  c.addEventListener('pointerdown',function(){ try{ c.focus(); }catch(_){} });
})();</script>";
    html = html.Replace("</head>", css + "\n  </head>").Replace("</body>", js + "\n  </body>");
    File.WriteAllText(p, html);
  }

  [MenuItem("Neon Hive/Build WebGL to docs")]
  public static void Build(){
    int code = 1;
    try {
      SetupScene();
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
      PlayerSettings.WebGL.decompressionFallback = true;
      PlayerSettings.WebGL.threadsSupport = false;
      PlayerSettings.productName = "Neon Hive"; PlayerSettings.companyName = "mikenev";
      PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
      bool dev = System.Environment.GetEnvironmentVariable("NH_DEV") == "1";
      if (dev){ PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full); }
      else { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly); }
      var outDir = Path.GetFullPath(dev ? "Builds/WebGLDev" : "docs");
      var r = BuildPipeline.BuildPlayer(new[]{ ScenePath }, outDir, BuildTarget.WebGL, dev ? BuildOptions.Development : BuildOptions.None);
      Debug.Log("BUILD RESULT: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded){ File.WriteAllText(Path.Combine(outDir, ".nojekyll"), ""); PatchIndexHtml(outDir); code = 0; }
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}
