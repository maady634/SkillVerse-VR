using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace UnityEngine.XR.Content.Interaction
{
    public class XRLeverWithAxis : XRBaseInteractable
    {
        public enum RotationAxis { X, Y, Z }

        const float k_LeverDeadZone = 0.1f;

        [SerializeField] private Transform m_Handle;
        [SerializeField] private bool m_Value = false;
        [SerializeField] private bool m_LockToValue;
        [SerializeField][Range(-90f, 90f)] private float m_MaxAngle = 90f;
        [SerializeField][Range(-90f, 90f)] private float m_MinAngle = -90f;
        [SerializeField] private RotationAxis m_RotationAxis = RotationAxis.X;

        [SerializeField] private UnityEvent m_OnLeverActivate = new UnityEvent();
        [SerializeField] private UnityEvent m_OnLeverDeactivate = new UnityEvent();

        private IXRSelectInteractor m_Interactor;

        public RotationAxis rotationAxis
        {
            get => m_RotationAxis;
            set => m_RotationAxis = value;
        }

        public bool value
        {
            get => m_Value;
            set => SetValue(value, true);
        }

        void Start()
        {
            SetValue(m_Value, true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            m_Interactor = args.interactorObject;
        }

        void EndGrab(SelectExitEventArgs args)
        {
            SetValue(m_Value, true);
            m_Interactor = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && isSelected)
                UpdateValue();
        }

        Vector3 GetLookDirection()
        {
            Vector3 direction = m_Interactor.GetAttachTransform(this).position - m_Handle.position;
            direction = transform.InverseTransformDirection(direction);
            direction = ZeroOutUnusedAxis(direction);
            return direction.normalized;
        }

        Vector3 ZeroOutUnusedAxis(Vector3 input)
        {
            switch (m_RotationAxis)
            {
                case RotationAxis.X:
                    input.x = 0;
                    break;
                case RotationAxis.Y:
                    input.y = 0;
                    break;
                case RotationAxis.Z:
                    input.z = 0;
                    break;
            }
            return input;
        }

        void UpdateValue()
        {
            Vector3 lookDirection = GetLookDirection();
            float lookAngle = 0f;

            switch (m_RotationAxis)
            {
                case RotationAxis.X:
                    lookAngle = Mathf.Atan2(lookDirection.z, lookDirection.y) * Mathf.Rad2Deg;
                    break;
                case RotationAxis.Y:
                    lookAngle = Mathf.Atan2(lookDirection.x, lookDirection.z) * Mathf.Rad2Deg;
                    break;
                case RotationAxis.Z:
                    lookAngle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg;
                    break;
            }

            lookAngle = Mathf.Clamp(lookAngle, Mathf.Min(m_MinAngle, m_MaxAngle), Mathf.Max(m_MinAngle, m_MaxAngle));

            float maxDist = Mathf.Abs(m_MaxAngle - lookAngle);
            float minDist = Mathf.Abs(m_MinAngle - lookAngle);

            if (m_Value) maxDist *= (1f - k_LeverDeadZone);
            else minDist *= (1f - k_LeverDeadZone);

            bool newValue = maxDist < minDist;

            SetHandleAngle(lookAngle);
            SetValue(newValue);
        }

        void SetValue(bool isOn, bool forceRotation = false)
        {
            if (m_Value == isOn)
            {
                if (forceRotation)
                    SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
                return;
            }

            m_Value = isOn;
            if (m_Value)
                m_OnLeverActivate.Invoke();
            else
                m_OnLeverDeactivate.Invoke();

            if (!isSelected && (m_LockToValue || forceRotation))
                SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
        }

        void SetHandleAngle(float angle)
        {
            if (m_Handle == null) return;

            Vector3 rotation = Vector3.zero;

            switch (m_RotationAxis)
            {
                case RotationAxis.X:
                    rotation = new Vector3(angle, 0f, 0f);
                    break;
                case RotationAxis.Y:
                    rotation = new Vector3(0f, angle, 0f);
                    break;
                case RotationAxis.Z:
                    rotation = new Vector3(0f, 0f, angle);
                    break;
            }

            m_Handle.localRotation = Quaternion.Euler(rotation);
        }

        void OnDrawGizmosSelected()
        {
            if (m_Handle == null) return;

            Vector3 origin = m_Handle.position;
            Vector3 maxDirection = Vector3.zero;
            Vector3 minDirection = Vector3.zero;
            float length = 0.25f;

            switch (m_RotationAxis)
            {
                case RotationAxis.X:
                    maxDirection = transform.TransformDirection(Quaternion.Euler(m_MaxAngle, 0f, 0f) * Vector3.up);
                    minDirection = transform.TransformDirection(Quaternion.Euler(m_MinAngle, 0f, 0f) * Vector3.up);
                    break;
                case RotationAxis.Y:
                    maxDirection = transform.TransformDirection(Quaternion.Euler(0f, m_MaxAngle, 0f) * Vector3.up);
                    minDirection = transform.TransformDirection(Quaternion.Euler(0f, m_MinAngle, 0f) * Vector3.up);
                    break;
                case RotationAxis.Z:
                    maxDirection = transform.TransformDirection(Quaternion.Euler(0f, 0f, m_MaxAngle) * Vector3.up);
                    minDirection = transform.TransformDirection(Quaternion.Euler(0f, 0f, m_MinAngle) * Vector3.up);
                    break;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + maxDirection * length);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + minDirection * length);
        }

        void OnValidate()
        {
            SetHandleAngle(m_Value ? m_MaxAngle : m_MinAngle);
        }
    }
}
