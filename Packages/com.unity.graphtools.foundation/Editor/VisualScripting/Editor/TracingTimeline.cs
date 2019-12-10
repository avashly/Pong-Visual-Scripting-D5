using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.VisualScripting.Editor
{
    class TracingTimeline
    {
        const int k_FrameRectangleWidth = 2;
        const int k_FrameRectangleHeight = 20;
        const int k_MinTimeVisibleOnTheRight = 30;

        readonly State m_VSWindowState;
        readonly IMGUIContainer m_ImguiContainer;
        TimeArea m_TimeArea;
        AnimEditorOverlay m_Overlay;
        TimelineState m_State;

        public bool Dirty { get; internal set; }

        public TracingTimeline(State vsWindowState, IMGUIContainer imguiContainer)
        {
            m_VSWindowState = vsWindowState;
            m_ImguiContainer = imguiContainer;
            m_TimeArea = new TimeArea();
            m_Overlay = new AnimEditorOverlay();
            m_State = new TimelineState(m_TimeArea);
            m_Overlay.state = m_State;
        }

        public void SyncVisible()
        {
            if (m_ImguiContainer.style.display.value == DisplayStyle.Flex != m_VSWindowState.EditorDataModel.TracingEnabled)
                m_ImguiContainer.style.display = m_VSWindowState.EditorDataModel.TracingEnabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void OnGUI(Rect timeRect)
        {
            // sync timeline and tracing toolbar state both ways
            m_State.CurrentTime = TimelineState.FrameToTime(m_VSWindowState.currentTracingFrame);

            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
            {
                if (m_State.MaxVisibleTime < m_State.CurrentTime + k_MinTimeVisibleOnTheRight)
                {
                    m_TimeArea.SetShownRange(m_State.CurrentTime + k_MinTimeVisibleOnTheRight - m_State.MaxVisibleTime);
                }
            }

            m_Overlay.HandleEvents();
            int timeChangedByTimeline = TimelineState.TimeToFrame(m_State.CurrentTime);
            // force graph update
            if (timeChangedByTimeline != m_VSWindowState.currentTracingFrame)
                m_VSWindowState.currentTracingStep = -1;
            m_VSWindowState.currentTracingFrame = timeChangedByTimeline;

            // time scales
            GUILayout.BeginArea(timeRect);
            m_TimeArea.Draw(timeRect);
            GUILayout.EndArea();
            GUI.BeginGroup(timeRect);

            DebuggerTracer.GraphTrace trace = DebuggerTracer.GetGraphData((m_VSWindowState?.AssetModel as Object)?.GetInstanceID() ?? -1, false);
            var frameHasDataColor = new Color32(68, 192, 255, 255);
            if (trace != null && trace.AllFrames.Count > 0)
            {
                float frameDeltaToPixel = m_TimeArea.FrameDeltaToPixel();
                DebuggerTracer.FrameData first = trace.AllFrames[0];
                DebuggerTracer.FrameData last = trace.AllFrames[trace.AllFrames.Count - 1];
                float start = m_TimeArea.FrameToPixel(first.Frame);
                float width = frameDeltaToPixel * Mathf.Max(last.Frame - first.Frame, 1);

                EditorGUI.DrawRect(new Rect(start, k_FrameRectangleHeight, width, k_FrameRectangleWidth), frameHasDataColor);
            }

            // playing head
            m_Overlay.OnGUI(timeRect, timeRect);
            GUI.EndGroup();
        }
    }
}
