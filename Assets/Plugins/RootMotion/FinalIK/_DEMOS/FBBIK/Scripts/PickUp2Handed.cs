using UnityEngine;
using System.Collections;
using RootMotion;
using RootMotion.FinalIK;

namespace RootMotion.Demos {
	
	/// <summary>
	/// Picking up an arbitrary object with both hands.
	/// </summary>
	public abstract class PickUp2Handed : MonoBehaviour {
		
		// GUI for testing
		public int GUIspace;
		// Added for UntitledPoolGame: lets a subclass (see PickUpCue) hide
		// this demo's test buttons for real gameplay use, without touching
		// its actual pickup/drop logic (OnStart/OnPause/OnDrop, unaffected
		// by this flag).
		public bool showDebugGUI = true;

		void OnGUI() {
			if (!showDebugGUI) return;

			GUILayout.BeginHorizontal();
			GUILayout.Space(GUIspace);

			if (!holding) {

				if (GUILayout.Button("Pick Up " + obj.name)) {
					interactionSystem.StartInteraction(FullBodyBipedEffector.LeftHand, obj, false);
					interactionSystem.StartInteraction(FullBodyBipedEffector.RightHand, obj, false);
				}
				
			} else {
                GUILayout.BeginVertical();
                if (holdingRight)
                {
                    if (GUILayout.Button("Release Right"))
                    {
                        interactionSystem.ResumeInteraction(FullBodyBipedEffector.RightHand);
                    }
                }
                if (holdingLeft)
                {
                    if (GUILayout.Button("Release Left"))
                    {
                        interactionSystem.ResumeInteraction(FullBodyBipedEffector.LeftHand);
                    }
                }
				if (GUILayout.Button("Drop " + obj.name)) {
                    interactionSystem.ResumeAll();
                }
                GUILayout.EndVertical();
			}

			GUILayout.EndHorizontal();
		}

		protected abstract void RotatePivot();
		
		public InteractionSystem interactionSystem; // The InteractionSystem of the character
		public InteractionObject obj; // The object to pick up
		public Transform pivot; // The pivot point of the hand targets
		public Transform holdPoint; // The point where the object will lerp to when picked up
		public float pickUpTime = 0.3f; // Maximum lerp speed of the object. Decrease this value to give the object more weight

		// Added for UntitledPoolGame: which effector's Start/Pause events
		// drive the object's shared state (parenting/kinematic/holdPoint
		// sync, in OnPause/OnStart below) — originally hardcoded to
		// LeftHand, since this demo always starts both hands together and
		// it doesn't matter which one is treated as the trigger. Exposed so
		// a single-hand pickup (e.g. RightHand only) still attaches the
		// object correctly instead of silently doing nothing.
		public FullBodyBipedEffector primaryEffector = FullBodyBipedEffector.LeftHand;

		private float holdWeight, holdWeightVel;
		private Vector3 pickUpPosition;
		private Quaternion pickUpRotation;
		
		void Start() {
			// Listen to interaction events
			interactionSystem.OnInteractionStart += OnStart;
			interactionSystem.OnInteractionPause += OnPause;
			interactionSystem.OnInteractionResume += OnDrop;
		}
		
		// Called by the InteractionSystem when an interaction is paused (on trigger)
		private void OnPause(FullBodyBipedEffector effectorType, InteractionObject interactionObject) {
			if (effectorType != primaryEffector) return;
			if (interactionObject != obj) return;

			// Make the object inherit the character's movement
			obj.transform.parent = interactionSystem.transform;
			
			// Make the object kinematic
			var r = obj.GetComponent<Rigidbody>();
			if (r != null) r.isKinematic = true;

			// Set object pick up position and rotation to current
			pickUpPosition = obj.transform.position;
			pickUpRotation = obj.transform.rotation;
			holdWeight = 0f;
			holdWeightVel = 0f;
		}
		
		// Called by the InteractionSystem when an interaction starts
		private void OnStart(FullBodyBipedEffector effectorType, InteractionObject interactionObject) {
			if (effectorType != primaryEffector) return;
			if (interactionObject != obj) return;

			// Rotate the hold point so it matches the current rotation of the object
			holdPoint.rotation = obj.transform.rotation;

			// Rotate the pivot of the hand targets
			RotatePivot();
		}
		
		// Called by the InteractionSystem when an interaction is resumed from being paused
		private void OnDrop(FullBodyBipedEffector effectorType, InteractionObject interactionObject) {
            if (holding) return;
            if (interactionObject != obj) return;

            // Make the object independent of the character
			obj.transform.parent = null;
			
			// Turn on physics for the object
			if (obj.GetComponent<Rigidbody>() != null) obj.GetComponent<Rigidbody>().isKinematic = false;
		}
		
		void LateUpdate() {
			// Added for UntitledPoolGame: originally ran the Lerp below
			// unconditionally for as long as `holding` was true — not just
			// during the pickup transition, but for the ENTIRE time the
			// object was held. holdPoint is a child of the character's
			// spine, so it drifts slightly with body sway/lean while
			// walking or turning — and since the Lerp target was always
			// holdPoint's CURRENT pose, the held object kept re-snapping to
			// it every single frame, slowly wandering away from its correct
			// orientation the longer it was carried (reported: cue rotation
			// completely different after walking around with it than right
			// when it was picked up). Fixed by freezing the object's LOCAL
			// pose once the pickup transition has converged — it's already
			// parented to interactionSystem.transform (see OnPause), so it
			// still moves/turns rigidly with the player from then on, just
			// without continuing to chase holdPoint specifically.
			if (holding && holdWeight < 0.999f) {
				// Smoothing in the hold weight
				holdWeight = Mathf.SmoothDamp(holdWeight, 1f, ref holdWeightVel, pickUpTime);

				// Interpolation
				obj.transform.position = Vector3.Lerp(pickUpPosition, holdPoint.position, holdWeight);
				obj.transform.rotation = Quaternion.Lerp(pickUpRotation, holdPoint.rotation, holdWeight);
			}
		}
		
		// Are we currently holding the object?
		private bool holding {
			get {
				return holdingLeft || holdingRight;
			}
		}

        // Are we currently holding the object with left hand?
        private bool holdingLeft
        {
            get
            {
                return interactionSystem.IsPaused(FullBodyBipedEffector.LeftHand) && interactionSystem.GetInteractionObject(FullBodyBipedEffector.LeftHand) == obj;
            }
        }

        // Are we currently holding the object with right hand?
        private bool holdingRight
        {
            get
            {
                return interactionSystem.IsPaused(FullBodyBipedEffector.RightHand) && interactionSystem.GetInteractionObject(FullBodyBipedEffector.RightHand) == obj;
            }
        }

        // Clean up delegates
        void OnDestroy() {
			if (interactionSystem == null) return;

			interactionSystem.OnInteractionStart -= OnStart;
			interactionSystem.OnInteractionPause -= OnPause;
			interactionSystem.OnInteractionResume -= OnDrop;
		}
	}
}
