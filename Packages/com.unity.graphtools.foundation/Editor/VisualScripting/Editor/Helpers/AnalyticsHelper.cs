using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.VisualScripting.Editor
{
    [Serializable]
    struct VSUserActions
    {
        [FormerlySerializedAs("vs_user_search_query")]
        public string vsUserSearchQuery;
        [FormerlySerializedAs("vs_user_create_node")]
        public string vsUserCreateNode;
        [FormerlySerializedAs("vs_version")]
        public string vsVersion;
        [FormerlySerializedAs("vs_searcher_context")]
        public string vsSearcherContext;
        [FormerlySerializedAs("vs_searcher_cancelled")]
        public string vsSearcherCancelled;

        public void SetResult(NodeCreationEvent e)
        {
            vsSearcherCancelled = e.ToString();
        }

        public override string ToString()
        {
            return $"{nameof(vsUserSearchQuery)}: {vsUserSearchQuery}\n{nameof(vsUserCreateNode)}: {vsUserCreateNode}\n{nameof(vsVersion)}: {vsVersion}\n{nameof(vsSearcherContext)}: {vsSearcherContext}\n{nameof(vsSearcherCancelled)}: {vsSearcherCancelled}";
        }
    }

    public enum NodeCreationEvent
    {
        Keep,
        UndoOrDelete,
        Cancel,
    }

    class AnalyticsHelper
    {
        class PendingNodeCreationEvent
        {
            public readonly GUID GUID;
            public readonly string NodeTitle;
            public VSUserActions? Action;
            public bool SkipNextFlush;
            public readonly double CreationTime;

            public PendingNodeCreationEvent(GUID guid, string nodeTitle)
            {
                GUID = guid;
                NodeTitle = nodeTitle;
                CreationTime = EditorApplication.timeSinceStartup;
            }
        }

        public enum UserActionKind
        {
            SendImmediately,
            WaitForConfirmation,
        }

        static bool s_LogEnabled = false;
        static AnalyticsHelper s_Instance;
        const string k_VisualScriptingVersion = "0.5";
        const int k_NodeCreationFlushTimeSeconds = 30;
        PendingNodeCreationEvent m_LastNodeCreation;
        Queue<VSUserActions> m_UserActionEvents;
        double m_EditorTimeSinceStartup = EditorApplication.timeSinceStartup;

        AnalyticsHelper() {}

        public static AnalyticsHelper Instance
        {
            get
            {
                if (s_Instance != null)
                {
                    return s_Instance;
                }

                s_Instance = new AnalyticsHelper();
                EditorAnalytics.RegisterEventWithLimit("vsUserActions", 1000, 1000, "unity.vsUserActions", 3);
                EditorApplication.update += s_Instance.Update;
                return s_Instance;
            }
        }

        public void AddUserActionEvent(string searchKeywords, SearcherContext context, UserActionKind kind)
        {
            if (m_LastNodeCreation == null) // TODO: context is not handled yet, ie. edge-to-stack
                return;

            if (m_UserActionEvents == null)
            {
                m_UserActionEvents = new Queue<VSUserActions>();
            }

            var vsUserActions = new VSUserActions
            {
                vsUserSearchQuery = searchKeywords,
                vsUserCreateNode = m_LastNodeCreation.NodeTitle,
                vsVersion = k_VisualScriptingVersion,
                vsSearcherContext = context.ToString(),
            };
            vsUserActions.SetResult(m_LastNodeCreation.NodeTitle == null ? NodeCreationEvent.Cancel : NodeCreationEvent.Keep);
            Log($"AddUserActionEvent {kind} {vsUserActions}");

            switch (kind)
            {
                case UserActionKind.SendImmediately:
                    m_UserActionEvents.Enqueue(vsUserActions);
                    break;
                case UserActionKind.WaitForConfirmation:
                    m_LastNodeCreation.Action = vsUserActions;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public void FlushNodeCreationEvent(NodeCreationEvent e, IEnumerable<GUID> deletedNodeIds = null)
        {
            if (m_LastNodeCreation?.Action.HasValue == true)
            {
                if (m_LastNodeCreation.SkipNextFlush)
                {
                    m_LastNodeCreation.SkipNextFlush = false;
                    return;
                }

                VSUserActions vsUserActions = m_LastNodeCreation.Action.Value;

                if (e == NodeCreationEvent.UndoOrDelete && (deletedNodeIds == null || deletedNodeIds.Contains(m_LastNodeCreation.GUID)))
                    vsUserActions.SetResult(NodeCreationEvent.UndoOrDelete);

                m_UserActionEvents.Enqueue(vsUserActions);
                Log($"Flush last node creation {m_LastNodeCreation.GUID} result: {vsUserActions.vsSearcherCancelled}");

                m_LastNodeCreation = null;
            }
        }

        public void SetLastNodeCreated(GUID nodeId, string nodeTitle)
        {
            FlushNodeCreationEvent(NodeCreationEvent.Keep);
            Log($"Set last node created: {nodeId}");
            m_LastNodeCreation = new PendingNodeCreationEvent(nodeId, nodeTitle){SkipNextFlush = true};
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup - m_EditorTimeSinceStartup > 3600)
            {
                m_EditorTimeSinceStartup = EditorApplication.timeSinceStartup;
                SendAllUserActions();
            }

            if (m_LastNodeCreation != null && EditorApplication.timeSinceStartup - m_LastNodeCreation.CreationTime > k_NodeCreationFlushTimeSeconds)
            {
                FlushNodeCreationEvent(NodeCreationEvent.Keep);
            }
        }

        public void SendAllUserActionsIncludingPendingNodeCreation()
        {
            if (m_LastNodeCreation?.Action.HasValue == true)
            {
                m_UserActionEvents.Enqueue(m_LastNodeCreation.Action.Value);
                m_LastNodeCreation = null;
            }

            SendAllUserActions();
        }

        void SendAllUserActions()
        {
            if (m_UserActionEvents != null)
            {
                while (m_UserActionEvents.Count != 0)
                {
                    EditorAnalytics.SendEventWithLimit("vsUserActions", m_UserActionEvents.Dequeue(), 3);
                }
            }
        }

        static void Log(string msg)
        {
            if (s_LogEnabled)
                Debug.Log("[Analytics] " + msg);
        }
    }
}
