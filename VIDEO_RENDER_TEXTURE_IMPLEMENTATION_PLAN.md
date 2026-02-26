# Plan d'implémentation détaillé — Support Render Texture pour le sample Video Timeline

## Contexte

Le sample `Samples~/Customization/Video` ne supporte actuellement que :

- `VideoRenderMode.CameraFarPlane`
- `VideoRenderMode.CameraNearPlane`

Il **ne supporte pas** `VideoRenderMode.RenderTexture` et crée systématiquement un `VideoPlayer` caché par clip.

Objectif :

1. Supporter explicitement le mode **Render Texture**.
2. Permettre de piloter un **`VideoPlayer` déjà configuré dans la scène** (binding track), notamment en RenderTexture.
3. Conserver le fallback legacy (player caché) quand aucun binding n’est fourni.

---

## Résultat attendu

- Un `VideoTrack` peut être bindé à un `VideoPlayer` de scène via `PlayableDirector`.
- Si le player bindé est en `RenderTexture`, Timeline le pilote sans casser sa config (`targetTexture`, etc.).
- Sans binding, le sample continue de créer un player caché comme avant.
- Le player de scène n’est jamais détruit par les playables.

---

## Principes de design (obligatoires)

1. **Mode bound (player de scène) prioritaire**
   - Si un binding existe, on l’utilise.
   - Sinon, fallback sur instanciation interne.

2. **Préservation de la config de scène**
   - En mode bound, ne pas écraser :
     - `renderMode`
     - `targetTexture`
     - `targetCamera`
   - On pilote seulement lecture/sync/time/speed/clip.

3. **RenderTexture support dans le fallback**
   - Ajouter `RenderTexture` à l’enum du clip.
   - Ajouter une référence de texture cible (`ExposedReference<RenderTexture>`) pour le player interne.

4. **Préload désactivé sur player partagé**
   - Le preload peut provoquer des conflits si plusieurs clips contrôlent le même player.
   - En mode bound partagé : désactiver preload.

---

## Patch précis (fichier par fichier)

## 1) `Samples~/Customization/Video/VideoTrack.cs`

### Modifications

- Ajouter `using UnityEngine.Video;`
- Ajouter `[TrackBindingType(typeof(VideoPlayer))]`
- Dans `CreateTrackMixer(...)` :
  - récupérer `PlayableDirector` sur `go`
  - récupérer le binding `VideoPlayer` pour ce track
  - injecter ce player dans chaque `VideoPlayableAsset`
  - détecter overlap de clips si player partagé + logger warning

### Patch proposé

```csharp
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace Timeline.Samples
{
    [Serializable]
    [TrackClipType(typeof(VideoPlayableAsset))]
    [TrackBindingType(typeof(VideoPlayer))]
    [TrackColor(0.008f, 0.698f, 0.655f)]
    public class VideoTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            VideoPlayer boundVideoPlayer = null;
            if (go != null && go.TryGetComponent(out PlayableDirector director))
                boundVideoPlayer = director.GetGenericBinding(this) as VideoPlayer;

            var orderedClips = GetClips().OrderBy(c => c.start).ToArray();

            for (int i = 0; i < orderedClips.Length; i++)
            {
                var clip = orderedClips[i];
                var asset = clip.asset as VideoPlayableAsset;
                if (asset == null)
                    continue;

                asset.clipInTime = clip.clipIn;
                asset.startTime = clip.start;
                asset.boundSceneVideoPlayer = boundVideoPlayer;
            }

            if (boundVideoPlayer != null)
            {
                for (int i = 1; i < orderedClips.Length; i++)
                {
                    if (orderedClips[i].start < orderedClips[i - 1].end)
                    {
                        Debug.LogWarning(
                            $"[{nameof(VideoTrack)}] Overlapping clips detected while using a shared bound VideoPlayer on track '{name}'. " +
                            "Playback conflicts may occur. Prefer non-overlapping clips with shared-player mode.",
                            this);
                        break;
                    }
                }
            }

            return ScriptPlayable<VideoSchedulerPlayableBehaviour>.Create(graph, inputCount);
        }
    }
}
```

---

## 2) `Samples~/Customization/Video/VideoPlayableAsset.cs`

### Modifications

1. Étendre `RenderMode` avec `RenderTexture`.
2. Ajouter `ExposedReference<RenderTexture> targetTexture`.
3. Ajouter propriété runtime injectée par le track :
   - `internal VideoPlayer boundSceneVideoPlayer { get; set; }`
4. Dans `CreatePlayable(...)` :
   - utiliser player bound si disponible
   - fallback sinon
5. Ajouter configuration spécifique :
   - `ConfigureBoundVideoPlayer(...)` (ne touche pas render target config)
   - `CreateVideoPlayer(...)` supporte le mode RenderTexture
6. Passer des flags au behaviour : ownership/preload/alpha.

### Patch proposé

```csharp
using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace Timeline.Samples
{
    [Serializable]
    public class VideoPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        public enum RenderMode
        {
            CameraFarPlane,
            CameraNearPlane,
            RenderTexture
        }

        [Tooltip("The video clip to play.")]
        public VideoClip videoClip;

        [Tooltip("Mutes the audio from the video")]
        public bool mute;

        [Tooltip("Loops the video.")]
        public bool loop = true;

        [Tooltip("The amount of time before the video begins to start preloading the video stream.")]
        public double preloadTime = 0.3;

        [Tooltip("The aspect ratio of the video to playback.")]
        public VideoAspectRatio aspectRatio = VideoAspectRatio.FitHorizontally;

        [Tooltip("Where the video content will be drawn.")]
        public RenderMode renderMode = RenderMode.CameraFarPlane;

        [Tooltip("Specifies which camera to render to. If unassigned, the main camera will be used.")]
        public ExposedReference<Camera> targetCamera;

        [Tooltip("Target RenderTexture when Render Mode is RenderTexture.")]
        public ExposedReference<RenderTexture> targetTexture;

        [Tooltip("Specifies an optional audio source to output to.")]
        public ExposedReference<AudioSource> audioSource;

        // Injected by VideoTrack at graph build time (runtime only)
        internal VideoPlayer boundSceneVideoPlayer { get; set; }

        public double clipInTime { get; set; }
        public double startTime { get; set; }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
        {
            if (videoClip == null)
                return Playable.Create(graph);

            Camera camera = targetCamera.Resolve(graph.GetResolver());
            if (camera == null)
                camera = Camera.main;

            AudioSource resolvedAudioSource = audioSource.Resolve(graph.GetResolver());
            bool usingBoundScenePlayer = boundSceneVideoPlayer != null;

            VideoPlayer player = usingBoundScenePlayer
                ? ConfigureBoundVideoPlayer(boundSceneVideoPlayer)
                : CreateVideoPlayer(camera, targetTexture.Resolve(graph.GetResolver()), resolvedAudioSource);

            if (player == null)
                return Playable.Create(graph);

            var playable = ScriptPlayable<VideoPlayableBehaviour>.Create(graph);
            VideoPlayableBehaviour behaviour = playable.GetBehaviour();
            behaviour.videoPlayer = player;
            behaviour.preloadTime = preloadTime;
            behaviour.clipInTime = clipInTime;
            behaviour.startTime = startTime;

            behaviour.ownsVideoPlayer = !usingBoundScenePlayer;
            behaviour.allowPreload = !usingBoundScenePlayer;
            behaviour.applyCameraAlpha = !usingBoundScenePlayer;
            behaviour.isSharedBoundPlayer = usingBoundScenePlayer;

            return playable;
        }

        public override double duration => videoClip == null ? base.duration : videoClip.length;

        public ClipCaps clipCaps
        {
            get
            {
                var caps = ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;
                if (loop)
                    caps |= ClipCaps.Looping;
                return caps;
            }
        }

        VideoPlayer ConfigureBoundVideoPlayer(VideoPlayer videoPlayer)
        {
            if (videoPlayer == null || videoClip == null)
                return null;

            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
            videoPlayer.waitForFirstFrame = false;
            videoPlayer.skipOnDrop = true;
            videoPlayer.isLooping = loop;

            // Important: do NOT override renderMode/targetTexture/targetCamera here.
            // Scene configuration must remain authoritative in bound mode.

            if (mute)
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

            return videoPlayer;
        }

        VideoPlayer CreateVideoPlayer(Camera camera, RenderTexture renderTexture, AudioSource targetAudioSource)
        {
            if (videoClip == null)
                return null;

            GameObject gameObject = new GameObject(videoClip.name) { hideFlags = HideFlags.HideAndDontSave };
            VideoPlayer videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
            videoPlayer.waitForFirstFrame = false;
            videoPlayer.skipOnDrop = true;
            videoPlayer.aspectRatio = aspectRatio;
            videoPlayer.isLooping = loop;

            switch (renderMode)
            {
                case RenderMode.CameraFarPlane:
                    videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
                    videoPlayer.targetCamera = camera;
                    break;
                case RenderMode.CameraNearPlane:
                    videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
                    videoPlayer.targetCamera = camera;
                    break;
                case RenderMode.RenderTexture:
                    videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                    videoPlayer.targetTexture = renderTexture;
                    break;
            }

            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            if (mute)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }
            else if (targetAudioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                for (ushort i = 0; i < videoPlayer.clip.audioTrackCount; ++i)
                    videoPlayer.SetTargetAudioSource(i, targetAudioSource);
            }

            return videoPlayer;
        }
    }
}
```

---

## 3) `Samples~/Customization/Video/VideoPlayableBehaviour.cs`

### Modifications

- Ajouter flags runtime :
  - `ownsVideoPlayer`
  - `allowPreload`
  - `applyCameraAlpha`
  - `isSharedBoundPlayer`
- Désactiver preload si `allowPreload == false`
- N’appliquer `targetCameraAlpha` que si `applyCameraAlpha`
- En shared-bound mode, ne pas détruire le GO du player

### Patch proposé

```csharp
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Video;

namespace Timeline.Samples
{
    public sealed class VideoPlayableBehaviour : PlayableBehaviour
    {
        public VideoPlayer videoPlayer;

        public double preloadTime;
        public double clipInTime;
        public double startTime;

        public bool ownsVideoPlayer;
        public bool allowPreload = true;
        public bool applyCameraAlpha = true;
        public bool isSharedBoundPlayer;

        private bool preparing;

        public void PrepareVideo()
        {
            if (!allowPreload)
                return;

            if (videoPlayer == null || videoPlayer.isPrepared || preparing)
                return;

            if (applyCameraAlpha)
                videoPlayer.targetCameraAlpha = 0.0f;

            videoPlayer.time = clipInTime;
            videoPlayer.Prepare();
            preparing = true;
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            if (videoPlayer == null || videoPlayer.clip == null)
                return;

            bool shouldBePlaying = info.evaluationType == FrameData.EvaluationType.Playback;
            if (!videoPlayer.isLooping && playable.GetTime() >= videoPlayer.clip.length)
                shouldBePlaying = false;

            if (shouldBePlaying)
            {
                videoPlayer.timeReference = VideoTimeReference.ExternalTime;
                if (!videoPlayer.isPlaying)
                    videoPlayer.Play();
                videoPlayer.externalReferenceTime = playable.GetTime() / videoPlayer.playbackSpeed;
            }
            else
            {
                videoPlayer.timeReference = VideoTimeReference.Freerun;
                if (!videoPlayer.isPaused)
                    videoPlayer.Pause();
                SyncVideoToPlayable(playable);
            }

            if (applyCameraAlpha)
                videoPlayer.targetCameraAlpha = info.effectiveWeight;

            if (!isSharedBoundPlayer && videoPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
            {
                for (ushort i = 0; i < videoPlayer.clip.audioTrackCount; ++i)
                    videoPlayer.SetDirectAudioVolume(i, info.effectiveWeight);
            }
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (videoPlayer == null)
                return;

            SyncVideoToPlayable(playable);
            videoPlayer.playbackSpeed = Mathf.Clamp(info.effectiveSpeed, 1 / 10f, 10f);
            videoPlayer.Play();
            preparing = false;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (videoPlayer == null)
                return;

            preparing = false;
            if (info.effectiveWeight <= 0)
                videoPlayer.Stop();
            else
                videoPlayer.Pause();
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            if (videoPlayer == null)
                return;

            videoPlayer.Stop();

            if (!ownsVideoPlayer)
                return;

            if (Application.isPlaying)
                Object.Destroy(videoPlayer.gameObject);
            else
                Object.DestroyImmediate(videoPlayer.gameObject);
        }

        private void SyncVideoToPlayable(Playable playable)
        {
            if (videoPlayer == null || videoPlayer.clip == null)
                return;

            if (videoPlayer.isLooping)
                videoPlayer.time = playable.GetTime() % videoPlayer.clip.length;
            else
                videoPlayer.time = System.Math.Min(playable.GetTime(), videoPlayer.clip.length);
        }
    }
}
```

---

## 4) `Samples~/Customization/Video/VideoSchedulerPlayableBehaviour.cs`

### Modifications

- Skip preload si le behaviour indique `allowPreload == false`.

### Patch proposé

```csharp
using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Timeline.Samples
{
    public sealed class VideoSchedulerPlayableBehaviour : PlayableBehaviour
    {
        public override void PrepareFrame(Playable playable, FrameData info)
        {
            var timelineTime = playable.GetGraph().GetRootPlayable(0).GetTime();
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInput(i).GetPlayableType() != typeof(VideoPlayableBehaviour))
                    continue;

                if (playable.GetInputWeight(i) > 0.0f)
                    continue;

                var scriptPlayable = (ScriptPlayable<VideoPlayableBehaviour>)playable.GetInput(i);
                VideoPlayableBehaviour behaviour = scriptPlayable.GetBehaviour();

                if (!behaviour.allowPreload)
                    continue;

                double preloadTime = Math.Max(0.0, behaviour.preloadTime);
                double clipStart = behaviour.startTime;

                if (timelineTime > clipStart - preloadTime && timelineTime <= clipStart)
                    behaviour.PrepareVideo();
            }
        }
    }
}
```

---

## Plan de test manuel

## A. Non-régression mode legacy

1. Aucun binding track.
2. Lire la timeline.
3. Vérifier playback identique à l’existant.
4. Vérifier destruction des players cachés à la fin.

## B. Binding player de scène en RenderTexture

1. Créer un `VideoPlayer` dans la scène :
   - `renderMode = RenderTexture`
   - `targetTexture` assignée
2. Binder ce player au `VideoTrack` via `PlayableDirector`.
3. Lire timeline + scrub.
4. Vérifier :
   - lecture vidéo OK
   - `targetTexture` inchangée
   - pas de destruction du GO de scène

## C. Fallback RenderTexture (sans player bound)

1. Sur le clip, choisir `renderMode = RenderTexture`.
2. Assigner `targetTexture` via `ExposedReference`.
3. Lire timeline.
4. Vérifier affichage dans la texture.

## D. Overlap avec player partagé

1. Deux clips qui se chevauchent sur un track bound.
2. Vérifier warning dans Console.
3. Confirmer comportement documenté comme non garanti.

---

## Critères d’acceptation

- [ ] `VideoTrack` supporte un binding `VideoPlayer`.
- [ ] Le sample supporte `RenderTexture` en fallback et en mode bound.
- [ ] En mode bound, `targetTexture/targetCamera/renderMode` ne sont pas écrasés.
- [ ] Le GO de scène n’est jamais détruit par `OnPlayableDestroy`.
- [ ] Le preload est désactivé en mode player partagé.
- [ ] Le mode legacy reste fonctionnel.

---

## Notes de handoff pour un autre agent IA

- Implémenter les patches dans l’ordre : `VideoTrack` -> `VideoPlayableAsset` -> `VideoPlayableBehaviour` -> `VideoSchedulerPlayableBehaviour`.
- Compiler dans Unity immédiatement après chaque fichier pour isoler les erreurs.
- Si l’équipe veut éviter tout conflit en mode bound : retirer `ClipCaps.Blending` lorsque `boundSceneVideoPlayer != null` (optionnel, non inclus dans le patch de base).
