using UnityEngine;
using System.Collections.Generic;

namespace Cinemachine
{
    /// <summary>A singleton that manages complete lists of CinemachineBrain and,
    /// Cinemachine Virtual Cameras, and the priority queue.  Provides
    /// services to keeping track of whether Cinemachine Virtual Cameras have
    /// been updated each frame.</summary>
    public sealed class CinemachineCore
    {
        /// <summary>Data version string.  Used to upgrade from legacy projects</summary>
        public static readonly int kStreamingVersion = 20170927;

        /// <summary>Human-readable Cinemachine Version</summary>
        public static readonly string kVersionString = "2.1.11";

        /// <summary>
        /// Stages in the Cinemachine Component pipeline, used for
        /// UI organization>.  This enum defines the pipeline order.
        /// </summary>
        public enum Stage
        {
            /// <summary>Second stage: position the camera in space</summary>
            Body,

            /// <summary>Third stage: orient the camera to point at the target</summary>
            Aim,

            /// <summary>Final pipeline stage: apply noise (this is done separately, in the
            /// Correction channel of the CameraState)</summary>
            Noise,

            /// <summary>Not a pipeline stage.  This is invoked on all virtual camera 
            /// types, after the pipeline is complete</summary>
            Finalize
        };

        private static CinemachineCore sInstance = null;

        /// <summary>Get the singleton instance</summary>
        public static CinemachineCore Instance
        {
            get
            {
                if (sInstance == null)
                    sInstance = new CinemachineCore();
                return sInstance;
            }
        }

        /// <summary>
        /// If true, show hidden Cinemachine objects, to make manual script mapping possible.
        /// </summary>
        public static bool sShowHiddenObjects = false;

        /// <summary>Delegate for overriding Unity's default input system.  Returns the value
        /// of the named axis.</summary>
        public delegate float AxisInputDelegate(string axisName);

        /// <summary>Delegate for overriding Unity's default input system.
        /// If you set this, then your delegate will be called instead of
        /// System.Input.GetAxis(axisName) whenever in-game user input is needed.</summary>
        public static AxisInputDelegate GetInputAxis = UnityEngine.Input.GetAxis;

        /// <summary>This event will fire after a brain updates its Camera</summary>
        public static CinemachineBrain.BrainEvent CameraUpdatedEvent = new CinemachineBrain.BrainEvent();

        /// <summary>List of all active CinemachineBrains.</summary>
        private List<CinemachineBrain> mActiveBrains = new List<CinemachineBrain>();

        /// <summary>Access the array of active CinemachineBrains in the scene</summary>
        public int BrainCount { get { return mActiveBrains.Count; } }

        /// <summary>Access the array of active CinemachineBrains in the scene 
        /// without gebnerating garbage</summary>
        /// <param name="index">Index of the brain to access, range 0-BrainCount</param>
        /// <returns>The brain at the specified index</returns>
        public CinemachineBrain GetActiveBrain(int index)
        {
            return mActiveBrains[index];
        }

        /// <summary>Called when a CinemachineBrain is enabled.</summary>
        internal void AddActiveBrain(CinemachineBrain brain)
        {
            // First remove it, just in case it's being added twice
            RemoveActiveBrain(brain);
            mActiveBrains.Insert(0, brain);
        }

        /// <summary>Called when a CinemachineBrain is disabled.</summary>
        internal void RemoveActiveBrain(CinemachineBrain brain)
        {
            mActiveBrains.Remove(brain);
        }

        /// <summary>List of all active ICinemachineCameras.</summary>
        private List<CinemachineVirtualCameraBase> mActiveCameras = new List<CinemachineVirtualCameraBase>();

        /// <summary>
        /// List of all active Cinemachine Virtual Cameras for all brains.
        /// This list is kept sorted by priority.
        /// </summary>
        public int VirtualCameraCount { get { return mActiveCameras.Count; } }

        /// <summary>Access the array of active ICinemachineCamera in the scene 
        /// without gebnerating garbage</summary>
        /// <param name="index">Index of the camera to access, range 0-VirtualCameraCount</param>
        /// <returns>The virtual camera at the specified index</returns>
        public CinemachineVirtualCameraBase GetVirtualCamera(int index)
        {
            return mActiveCameras[index];
        }

        /// <summary>Called when a Cinemachine Virtual Camera is enabled.</summary>
        internal void AddActiveCamera(CinemachineVirtualCameraBase vcam)
        {
            // Bring it to the top of the list
            RemoveActiveCamera(vcam);

            // Keep list sorted by priority
            int insertIndex;
            for (insertIndex = 0; insertIndex < mActiveCameras.Count; ++insertIndex)
                if (vcam.Priority >= mActiveCameras[insertIndex].Priority)
                    break;

            mActiveCameras.Insert(insertIndex, vcam);
        }

        /// <summary>Called when a Cinemachine Virtual Camera is disabled.</summary>
        internal void RemoveActiveCamera(CinemachineVirtualCameraBase vcam)
        {
            mActiveCameras.Remove(vcam);
        }

        // Registry of all vcams that are present, active or not
        private List<List<CinemachineVirtualCameraBase>> mAllCameras 
            = new List<List<CinemachineVirtualCameraBase>>();

        /// <summary>Called when a vcam is awakened.</summary>
        internal void CameraAwakened(CinemachineVirtualCameraBase vcam)
        {
            int parentLevel = 0;
            for (ICinemachineCamera p = vcam.ParentCamera; p != null; p = p.ParentCamera)
                ++parentLevel;
            while (mAllCameras.Count <= parentLevel)
                mAllCameras.Add(new List<CinemachineVirtualCameraBase>());
            mAllCameras[parentLevel].Add(vcam);
        }

        /// <summary>Called when a vcam is destroyed.</summary>
        internal void CameraDestroyed(CinemachineVirtualCameraBase vcam)
        {
            for (int i = 0; i < mAllCameras.Count; ++i)
                mAllCameras[i].Remove(vcam);
        }

        CinemachineVirtualCameraBase mRoundRobinVcamLastFrame = null;

        static float mLastUpdateTime;
        static int FixedFrameCount { get; set; } // Current fixed frame count

        /// <summary>Update all the active vcams in the scene, in the correct dependency order.</summary>
        internal void UpdateAllActiveVirtualCameras(int layerMask, Vector3 worldUp, float deltaTime)
        {
            // Setup for roundRobin standby updating
            var filter = CurrentUpdateFilter;
            bool canUpdateStandby = (filter != UpdateFilter.SmartFixed); // never in smart fixed
            bool didRoundRobinUpdate = false;
            CinemachineVirtualCameraBase currentRoundRobin = mRoundRobinVcamLastFrame;

            // Update the fixed frame count
            float now = Time.time;
            if (now != mLastUpdateTime)
            {
                mLastUpdateTime = now;
                if ((filter & ~UpdateFilter.Smart) == UpdateFilter.Fixed)
                    ++FixedFrameCount;
            }

            // Update the leaf-most cameras first
            for (int i = mAllCameras.Count-1; i >= 0; --i)
            {
                var sublist = mAllCameras[i];
                for (int j = sublist.Count - 1; j >= 0; --j)
                {
                    bool doRoundRobinUpdateNow = false;
                    var vcam = sublist[j];
                    if (!IsLive(vcam))
                    {
                        // Don't ever update subframes if not live
                        if (!canUpdateStandby)
                            continue;

                        if (vcam.m_StandbyUpdate == CinemachineVirtualCameraBase.StandbyUpdateMode.Never)
                            continue;

                        if (!vcam.isActiveAndEnabled)
                            continue;

                        // Handle round-robin
                        if (vcam.m_StandbyUpdate == CinemachineVirtualCameraBase.StandbyUpdateMode.RoundRobin)
                        {
                            if (currentRoundRobin != null)
                            {
                                if (currentRoundRobin == vcam)
                                    currentRoundRobin = null; // Take the next vcam for round-robin
                                continue;
                            }
                            doRoundRobinUpdateNow = true;
                            currentRoundRobin = vcam;
                        }
                    }
                    // Unless this is a round-robin update, we skip this vcam if it's 
                    // not on the layer mask
                    if (!doRoundRobinUpdateNow && ((1 << vcam.gameObject.layer) & layerMask) == 0)
                        continue;

                    bool updated = UpdateVirtualCamera(vcam, worldUp, deltaTime);
                    if (canUpdateStandby && vcam == currentRoundRobin)
                    {
                        // Did the previous roundrobin go live this frame?
                        if (!doRoundRobinUpdateNow)
                            currentRoundRobin = null; // yes, take the next vcam for round-robin
                        else if (updated)
                            didRoundRobinUpdate = true;
                        else
                            currentRoundRobin = mRoundRobinVcamLastFrame; // We tried to update but it didn't happen - keep the old one for next time
                    }
                }
            }
            // Finally, if the last roundrobin update candidate no longer exists, get rid of it
            if (canUpdateStandby && !didRoundRobinUpdate)
                currentRoundRobin = null; // take the first vcam for next round-robin
            mRoundRobinVcamLastFrame = currentRoundRobin; 
        }

        /// <summary>
        /// Update a single Cinemachine Virtual Camera if and only if it
        /// hasn't already been updated this frame.  Always update vcams via this method.
        /// Calling this more than once per frame for the same camera will have no effect.
        /// </summary>
        internal bool UpdateVirtualCamera(
            CinemachineVirtualCameraBase vcam, Vector3 worldUp, float deltaTime)
        {
            bool isSmartUpdate = (CurrentUpdateFilter & UpdateFilter.Smart) == UpdateFilter.Smart;
            UpdateTracker.UpdateClock updateClock 
                = (UpdateTracker.UpdateClock)(CurrentUpdateFilter & ~UpdateFilter.Smart);

            // If we're in smart update mode and the target moved, then we must examine
            // how the target has been moving recently in order to figure out whether to
            // update now
            bool updateNow = !isSmartUpdate;
            if (isSmartUpdate)
            {
                Transform updateTarget = GetUpdateTarget(vcam);
                if (updateTarget == null)
                    updateNow = (updateClock == UpdateTracker.UpdateClock.Late); // no target
                else
                    updateNow = UpdateTracker.GetPreferredUpdate(updateTarget) == updateClock;
            }
            if (!updateNow)
                return false;

            // Have we already been updated this frame?
            if (mUpdateStatus == null)
                mUpdateStatus = new Dictionary<CinemachineVirtualCameraBase, UpdateStatus>();

            UpdateStatus status;
            if (!mUpdateStatus.TryGetValue(vcam, out status))
            {
                status = new UpdateStatus();
                mUpdateStatus.Add(vcam, status);
            }

            int frameDelta = (updateClock == UpdateTracker.UpdateClock.Late)
                ? Time.frameCount - status.lastUpdateFrame
                : FixedFrameCount - status.lastUpdateFixedFrame;
            if (frameDelta == 0)
                return false; // already updated
            if (frameDelta != 1)
                deltaTime = -1; // multiple frames - kill the damping
            if (updateClock == UpdateTracker.UpdateClock.Late)
                status.lastUpdateFrame = Time.frameCount;
            else
                status.lastUpdateFixedFrame = FixedFrameCount;

//Debug.Log((vcam.ParentCamera == null ? "" : vcam.ParentCamera.Name + ".") + vcam.Name + ": frame " + Time.frameCount + "/" + status.lastUpdateFixedFrame + ", " + CurrentUpdateFilter + ", deltaTime = " + deltaTime);
            vcam.InternalUpdateCameraState(worldUp, deltaTime);
            status.lastUpdateMode = updateClock;
            return true;
        }

        class UpdateStatus
        {
            public int lastUpdateFrame;
            public int lastUpdateFixedFrame;
            public UpdateTracker.UpdateClock lastUpdateMode;
            public UpdateStatus()
            {
                lastUpdateFrame = -2;
                lastUpdateFixedFrame = 0;
                lastUpdateMode = UpdateTracker.UpdateClock.Late;
            }
        }
        static Dictionary<CinemachineVirtualCameraBase, UpdateStatus> mUpdateStatus;

        [RuntimeInitializeOnLoadMethod]
        static void InitializeModule() 
        { 
            mUpdateStatus = new Dictionary<CinemachineVirtualCameraBase, UpdateStatus>(); 
        }

        /// <summary>Internal use only</summary>
        internal enum UpdateFilter 
        { 
            Fixed = UpdateTracker.UpdateClock.Fixed, 
            Late = UpdateTracker.UpdateClock.Late,
            Smart = 8, // meant to be or'ed with the others
            SmartFixed = Smart | Fixed,
            SmartLate = Smart | Late
        }
        internal UpdateFilter CurrentUpdateFilter { get; set; }

        private static Transform GetUpdateTarget(CinemachineVirtualCameraBase vcam)
        {
            if (vcam == null || vcam.gameObject == null)
                return null;
            Transform target = vcam.LookAt;
            if (target != null)
                return target;
            target = vcam.Follow;
            if (target != null)
                return target;
            // If no target, use the vcam itself
            return vcam.transform;
        }

        /// <summary>Internal use only - inspector</summary>
        internal UpdateTracker.UpdateClock GetVcamUpdateStatus(CinemachineVirtualCameraBase vcam)
        {
            UpdateStatus status;
            if (mUpdateStatus == null || !mUpdateStatus.TryGetValue(vcam, out status))
                return UpdateTracker.UpdateClock.Late;
            return status.lastUpdateMode;
        }

        /// <summary>
        /// Is this virtual camera currently actively controlling any Camera?
        /// </summary>
        public bool IsLive(ICinemachineCamera vcam)
        {
            if (vcam != null)
            {
                for (int i = 0; i < BrainCount; ++i)
                {
                    CinemachineBrain b = GetActiveBrain(i);
                    if (b != null && b.IsLive(vcam))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Signal that the virtual has been activated.
        /// If the camera is live, then all CinemachineBrains that are showing it will
        /// send an activation event.
        /// </summary>
        public void GenerateCameraActivationEvent(ICinemachineCamera vcam)
        {
            if (vcam != null)
            {
                for (int i = 0; i < BrainCount; ++i)
                {
                    CinemachineBrain b = GetActiveBrain(i);
                    if (b != null && b.IsLive(vcam))
                        b.m_CameraActivatedEvent.Invoke(vcam);
                }
            }
        }

        /// <summary>
        /// Signal that the virtual camera's content is discontinuous WRT the previous frame.
        /// If the camera is live, then all CinemachineBrains that are showing it will send a cut event.
        /// </summary>
        public void GenerateCameraCutEvent(ICinemachineCamera vcam)
        {
            if (vcam != null)
            {
                for (int i = 0; i < BrainCount; ++i)
                {
                    CinemachineBrain b = GetActiveBrain(i);
                    if (b != null && b.IsLive(vcam))
                        b.m_CameraCutEvent.Invoke(b);
                }
            }
        }

        /// <summary>
        /// Try to find a CinemachineBrain to associate with a
        /// Cinemachine Virtual Camera.  The first CinemachineBrain
        /// in which this Cinemachine Virtual Camera is live will be used.
        /// If none, then the first active CinemachineBrain with the correct 
        /// layer filter will be used.  
        /// Brains with OutputCamera == null will not be returned.
        /// Final result may be null.
        /// </summary>
        /// <param name="vcam">Virtual camera whose potential brain we need.</param>
        /// <returns>First CinemachineBrain found that might be
        /// appropriate for this vcam, or null</returns>
        public CinemachineBrain FindPotentialTargetBrain(CinemachineVirtualCameraBase vcam)
        {
            if (vcam != null)
            {
                int numBrains = BrainCount;
                for (int i = 0; i < numBrains; ++i)
                {
                    CinemachineBrain b = GetActiveBrain(i);
                    if (b != null && b.OutputCamera != null && b.IsLive(vcam))
                        return b;
                }
                int layer = 1 << vcam.gameObject.layer;
                for (int i = 0; i < numBrains; ++i)
                {
                    CinemachineBrain b = GetActiveBrain(i);
                    if (b != null && b.OutputCamera != null && (b.OutputCamera.cullingMask & layer) != 0)
                        return b;
                }
            }
            return null;
        }
    }
}
