using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    [CustomTimelineEditor(typeof(Timeline.Samples.DeactivationTrack))]
    class DeactivationTrackEditor : TrackEditor
    {
        const string ClipText = "Inactive";

        const string k_ErrorParentString = "The bound GameObject is a parent of the PlayableDirector.";
        const string k_ErrorString = "The bound GameObject contains the PlayableDirector.";

        public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
        {
            var options = base.GetTrackOptions(track, binding);
            options.errorText = GetErrorText(track, binding);
            return options;
        }

        string GetErrorText(TrackAsset track, Object binding)
        {
            var gameObject = binding as GameObject;
            var currentDirector = TimelineEditor.inspectedDirector;
            if (gameObject != null && currentDirector != null)
            {
                var director = gameObject.GetComponent<PlayableDirector>();
                if (currentDirector == director)
                    return k_ErrorString;

                if (currentDirector.gameObject.transform.IsChildOf(gameObject.transform))
                    return k_ErrorParentString;
            }

            return base.GetErrorText(track, binding, TrackBindingErrors.PrefabBound);
        }

        public override void OnCreate(TrackAsset track, TrackAsset copiedFrom)
        {
            if (copiedFrom == null)
            {
                var clip = track.CreateClip<Timeline.Samples.DeactivationPlayableAsset>();
                clip.displayName = ClipText;
                clip.duration = System.Math.Max(clip.duration, track.timelineAsset.duration * 0.5f);
            }
        }
    }
}