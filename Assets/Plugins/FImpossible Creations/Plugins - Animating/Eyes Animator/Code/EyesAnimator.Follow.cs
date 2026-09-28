using UnityEngine;

namespace FIMSpace.FEyes
{
    public partial class FEyesAnimator
    {
        public float HeadToTargetAngle { get; private set; }
        public Vector3 HeadRotation { get; private set; }
        public Vector3 TargetLookDirection { get; private set; }
        public Vector3 LookRotationBase { get; private set; }
        public Vector3 DeltaVector { get; private set; }
        private Vector2 clampDelta;
        public Vector3 LookStartPositionBase { get; private set; }
        public Vector2 LookDeltaAngles { get; private set; }
        public Vector2 LookDeltaAnglesClamped { get; private set; }
        private Quaternion headReferenceBaseRotation = Quaternion.identity;
        private Vector2 lookRotationBaseAngles;

        private void ComputeBaseRotations()
        {
            // Look position referencing from middle of head for unsquinted look rotation
            LookStartPositionBase = GetStartLookPosition();
            TargetLookDirection = targetLookPosition - LookStartPositionBase;

            HeadToTargetAngle = Vector3.Angle( HeadReference.transform.TransformDirection( headForward ), TargetLookDirection.normalized );

            Vector3 targetDirectionLocal = GetHeadReferenceLocalDirection( TargetLookDirection );
            Vector2 targetLookAngles = GetLookAngles( targetDirectionLocal );
            float followBlend = FollowTargetAmount * conditionalFollowBlend;

            if( followBlend <= 0f )
                targetDirectionLocal = Vector3.forward;
            else if( followBlend < 1f )
                targetDirectionLocal = Vector3.Slerp( Vector3.forward, targetDirectionLocal, followBlend ).normalized;

            lookRotationBaseAngles = GetLookAngles( targetDirectionLocal );

            Quaternion defaultLookRot = HeadReference.rotation * headReferenceBaseRotation;
            Quaternion fadedRot = GetHeadReferenceLookRotation( lookRotationBaseAngles );
            LookRotationBase = fadedRot.eulerAngles;
            HeadRotation = defaultLookRot.eulerAngles;

            LookDeltaAngles = targetLookAngles;
            LookDeltaAnglesClamped = ClampLocalLookAngles( targetLookAngles, EyesClampHorizontal, EyesClampVertical, false );

            CheckLookRanges();

        }

        private Vector3 GetHeadReferenceLocalDirection( Vector3 worldDirection )
        {
            if( worldDirection.sqrMagnitude < 0.000001f ) return Vector3.forward;

            Quaternion worldReferenceRotation = HeadReference.rotation * headReferenceBaseRotation;
            return ( Quaternion.Inverse( worldReferenceRotation ) * worldDirection.normalized ).normalized;
        }

        private Vector2 GetHeadReferenceLookAngles( Vector3 worldDirection )
        {
            return GetLookAngles( GetHeadReferenceLocalDirection( worldDirection ) );
        }

        private static Vector2 GetLookAngles( Vector3 localDirection )
        {
            float horizontalMagnitude = Mathf.Sqrt( localDirection.x * localDirection.x + localDirection.z * localDirection.z );
            float pitch = -Mathf.Atan2( localDirection.y, horizontalMagnitude ) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2( localDirection.x, localDirection.z ) * Mathf.Rad2Deg;
            return new Vector2( pitch, yaw );
        }

        private Quaternion GetHeadReferenceLookRotation( Vector2 lookAngles )
        {
            return HeadReference.rotation * headReferenceBaseRotation * Quaternion.Euler( lookAngles.x, lookAngles.y, 0f );
        }

        private Quaternion AlignLookRotationToHeadUp( Quaternion lookRotation )
        {
            Vector3 forward = lookRotation * Forward;
            Vector3 up = Vector3.ProjectOnPlane( headUpDynamic, forward );

            if( up.sqrMagnitude < 0.000001f ) up = Vector3.ProjectOnPlane( lookRotation * Up, forward );
            if( up.sqrMagnitude < 0.000001f ) up = Vector3.Cross( forward, HeadReference.right );

            return Quaternion.LookRotation( forward, up.normalized );
        }

        private Vector2 ClampLocalLookAngles( Vector2 lookAngles, Vector2 clampHor, Vector2 clampVert, bool updateState )
        {
            if( updateState )
            {
                DeltaVector = -lookAngles;
                Vector3 localLookRotation = new Vector3( lookAngles.x, lookAngles.y, 0f );
                ClampDetection( DeltaVector, ref localLookRotation, Vector3.zero, clampHor, clampVert );
                return new Vector2( localLookRotation.x, localLookRotation.y );
            }

            if( lookAngles.y < clampHor.x )
            {
                lookAngles.y = clampHor.x;
            }
            else if( lookAngles.y > clampHor.y )
            {
                lookAngles.y = clampHor.y;
            }

            if( lookAngles.x < clampVert.x )
            {
                lookAngles.x = clampVert.x;
            }
            else if( lookAngles.x > clampVert.y )
            {
                lookAngles.x = clampVert.y;
            }

            return lookAngles;
        }

        /// <summary>
        /// Calculate angle between two directions around defined axis
        /// </summary>
        public static float AngleAroundAxis( Vector3 firstDirection, Vector3 secondDirection, Vector3 axis )
        {
            // Projecting to orthogonal target axis plane
            firstDirection = firstDirection - Vector3.Project( firstDirection, axis );
            secondDirection = secondDirection - Vector3.Project( secondDirection, axis );
            float angle = Vector3.Angle( firstDirection, secondDirection );
            return angle * ( Vector3.Dot( axis, Vector3.Cross( firstDirection, secondDirection ) ) < 0 ? -1 : 1 );
        }


        public bool OutOfRange { get; private set; }
        public bool OutOfDistance { get; private set; }
        private bool? forceOutOfMaxDistance = null;

        /// <summary>
        /// Checking if look target is not out of follow angle range or distance
        /// </summary>
        private void CheckLookRanges()
        {
            if( StopLookAbove >= 180 )
            {
                OutOfRange = false;
            }
            else
            {
                // Range blending out eyes animation
                if( Mathf.Abs( HeadToTargetAngle ) < StopLookAbove )
                {
                    OutOfRange = false;
                }
                else
                {
                    if( Mathf.Abs( HeadToTargetAngle ) > StopLookAbove * 1.2f + 10 ) OutOfRange = true;
                }
            }

            if( MaxTargetDistance > 0f )
            {
                float distance = Vector3.Distance( LookStartPositionBase, targetLookPosition );

                if( distance < MaxTargetDistance )
                {
                    OutOfDistance = false;
                }
                else
                {
                    if( distance > MaxTargetDistance + MaxTargetDistance * GoOutFactor ) OutOfDistance = true;
                }
            }
            else
            {
                OutOfDistance = false;
            }


            bool outOfRange;
            if( forceOutOfMaxDistance == null )
            {
                outOfRange = OutOfRange || OutOfDistance;
            }
            else outOfRange = forceOutOfMaxDistance.Value;

            if( outOfRange )
            {
                conditionalFollowBlend = Mathf.Max( 0f, conditionalFollowBlend - deltaTime * 5f );
            }
            else
            {
                conditionalFollowBlend = Mathf.Min( 1f, conditionalFollowBlend + deltaTime * 5f );
            }
        }


        /// <summary>
        /// Computing rotation for single eye using shared variables
        /// </summary>
        private void ComputeLookingRotation( ref Quaternion lookRotationBase, EyeSetup eyeSetup, int randomIndex = 0, int lagId = 0 )
        {
            Vector2 lookAngles = lookRotationBaseAngles;

            if( eyesData[randomIndex].randomDir != Vector3.zero )
            {
                Vector3 randomOffset = Vector3.Lerp( Vector3.zero, eyesData[randomIndex].randomDir, EyesRandomMovement );
                lookAngles += new Vector2( randomOffset.x, randomOffset.y );
            }

            if( !IndividualClamping )
                lookAngles = ClampLocalLookAngles( lookAngles, EyesClampHorizontal, EyesClampVertical, true );
            else
                lookAngles = ClampLocalLookAngles( lookAngles, eyeSetup.IndividualClampingHorizontal, eyeSetup.IndividualClampingVertical, true );

            lookRotationBase = GetHeadReferenceLookRotation( lookAngles );
        }



        Vector3 GetStartLookPosition()
        {
            Vector3 lookStartPositionBase;

            if( StaticLookStartPosition )
            {
                lookStartPositionBase = BaseTransform.position;
                lookStartPositionBase.y = HeadReference.position.y;
            }
            else
                lookStartPositionBase = HeadReference.transform.position;

            lookStartPositionBase += HeadReference.TransformVector( StartLookOffset );

            return lookStartPositionBase;
        }

        public int clampedHorizontal = 0;
        public int clampedVertical = 0;
        public bool IsClamping { get; private set; }

        protected virtual void ClampDetection( Vector2 deltaVector, ref Vector3 lookRotation, Vector3 rootOffset, Vector2 clampHor, Vector2 clampVert )
        {
            clampDelta = deltaVector;

            // Limit when looking left or right
            if( deltaVector.y > -clampHor.x )
            {
                clampDelta.y = -clampHor.x;
                lookRotation.y = rootOffset.y + clampHor.x;
                clampedHorizontal = -1;
            }
            else if( deltaVector.y < -clampHor.y )
            {
                clampDelta.y = -clampHor.y;
                lookRotation.y = rootOffset.y + clampHor.y;
                clampedHorizontal = 1;
            }
            else clampedHorizontal = 0;

            // Limit when looking up or down
            if( deltaVector.x > clampVert.y )
            {
                clampDelta.x = clampVert.y;
                clampedVertical = 1;
                lookRotation.x = rootOffset.x - clampVert.y;
            }
            else if( deltaVector.x < clampVert.x )
            {
                clampDelta.x = clampVert.x;
                clampedVertical = -1;
                lookRotation.x = rootOffset.x - clampVert.x;
            }
            else clampedVertical = 0;

            deltaV = deltaVector;

            if( clampedHorizontal != 0 || clampedVertical != 0 ) IsClamping = true;
        }

    }
}
