using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

// Timeline track to play videos.
// This sample demonstrates the following
//  * Using built in blending, speed and clip-in capabilities in custom clips.
//  * Using ClipEditors to customize clip drawing.
//  * Using a mixer PlayableBehaviour to perform look-ahead operations.
//  * Managing UnityEngine.Object lifetime (VideoPlayer) with a PlayableBehaviour.
//  * Using ExposedReferences to reference Components in the scene from a PlayableAsset.
[Serializable]
[TrackClipType(typeof(VideoPlayableAsset))]
[TrackBindingType(typeof(VideoPlayer))]
[TrackColor(0.008f, 0.698f, 0.655f)]
public class VideoTrack : TrackAsset
{
    // Called to create a PlayableBehaviour instance to represent the instance of the track, commonly referred
    // to as a Mixer playable.
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        VideoPlayer boundVideoPlayer = null;
        if (go != null)
        {
            var director = go.GetComponent<PlayableDirector>();
            if (director != null)
                boundVideoPlayer = director.GetGenericBinding(this) as VideoPlayer;
        }

        // This is called immediately before CreatePlayable on VideoPlayableAsset.
        // Each playable asset needs to be updated to the last clip values.
        var orderedClips = GetClips().OrderBy(c => c.start).ToArray();
        foreach (var clip in orderedClips)
        {
            var asset = clip.asset as VideoPlayableAsset;
            if (asset != null)
            {
                asset.clipInTime = clip.clipIn;
                asset.startTime = clip.start;
                asset.boundSceneVideoPlayer = boundVideoPlayer;
            }
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
