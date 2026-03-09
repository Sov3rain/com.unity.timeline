using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Timeline.Samples
{
    /// <summary>
    /// Track that can be used to control the inactive state of a GameObject.
    /// While a clip is active, the bound GameObject is deactivated.
    /// </summary>
    [Serializable]
    [TrackClipType(typeof(DeactivationPlayableAsset))]
    [TrackBindingType(typeof(GameObject))]
    public class DeactivationTrack : TrackAsset
    {
        [SerializeField]
        PostPlaybackState m_PostPlaybackState = PostPlaybackState.LeaveAsIs;
        DeactivationMixerPlayable m_DeactivationMixer;

        /// <summary>
        /// Specify what state to leave the GameObject in after the Timeline has finished playing.
        /// </summary>
        public enum PostPlaybackState
        {
            /// <summary>
            /// Set the GameObject to active.
            /// </summary>
            Active,

            /// <summary>
            /// Set the GameObject to inactive.
            /// </summary>
            Inactive,

            /// <summary>
            /// Revert the GameObject to the state it was in before Timeline started.
            /// </summary>
            Revert,

            /// <summary>
            /// Leave the GameObject in the state it was when Timeline stopped.
            /// </summary>
            LeaveAsIs
        }

        /// <summary>
        /// Specifies what state to leave the GameObject in after the Timeline has finished playing.
        /// </summary>
        public PostPlaybackState postPlaybackState
        {
            get => m_PostPlaybackState;
            set
            {
                m_PostPlaybackState = value;
                UpdateTrackMode();
            }
        }

        /// <inheritdoc/>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = DeactivationMixerPlayable.Create(graph, inputCount);
            m_DeactivationMixer = mixer.GetBehaviour();

            UpdateTrackMode();

            return mixer;
        }

        internal void UpdateTrackMode()
        {
            if (m_DeactivationMixer != null)
                m_DeactivationMixer.postPlaybackState = m_PostPlaybackState;
        }

        /// <inheritdoc/>
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            var gameObject = director.GetGenericBinding(this) as GameObject;
            if (gameObject != null)
            {
                driver.AddFromName(gameObject, "m_IsActive");
            }

            base.GatherProperties(director, driver);
        }

        /// <inheritdoc/>
        protected override void OnCreateClip(TimelineClip clip)
        {
            clip.displayName = "Inactive";
            base.OnCreateClip(clip);
        }
    }
}