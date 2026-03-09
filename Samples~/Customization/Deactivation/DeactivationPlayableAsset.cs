using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Timeline.Samples
{
    /// <summary>
    /// Playable asset used by <see cref="DeactivationTrack"/> clips.
    /// </summary>
    [Serializable]
    public class DeactivationPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>
        /// Deactivation clips do not support blending, extrapolation, looping, speed, or clip-in.
        /// </summary>
        public ClipCaps clipCaps => ClipCaps.None;

        /// <summary>
        /// Creates an empty playable used as a clip presence signal for the mixer.
        /// </summary>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            return Playable.Create(graph);
        }
    }
}