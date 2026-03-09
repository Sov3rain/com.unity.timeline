using UnityEngine;

namespace UnityEditor.Timeline
{
    [CustomEditor(typeof(Timeline.Samples.DeactivationTrack))]
    class DeactivationTrackInspector : Editor
    {
        static class Styles
        {
            public static readonly GUIContent PostPlaybackStateText = new GUIContent("Post-playback state");
        }

        SerializedProperty m_PostPlaybackProperty;

        public override void OnInspectorGUI()
        {
            var deactivationTrack = target as Timeline.Samples.DeactivationTrack;
            bool isTrackLocked = deactivationTrack != null && deactivationTrack.lockedInHierarchy;

            using (new EditorGUI.DisabledScope(isTrackLocked))
            {
                serializedObject.Update();

                EditorGUI.BeginChangeCheck();

                if (m_PostPlaybackProperty != null)
                    EditorGUILayout.PropertyField(m_PostPlaybackProperty, Styles.PostPlaybackStateText);

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    if (deactivationTrack != null)
                        deactivationTrack.UpdateTrackMode();
                }
            }
        }

        void OnEnable()
        {
            m_PostPlaybackProperty = serializedObject.FindProperty("m_PostPlaybackState");
        }
    }
}