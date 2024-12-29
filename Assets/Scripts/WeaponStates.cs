using UnityEngine;

namespace cowsins
{
    public class WeaponStates : MonoBehaviour
    {
        private WeaponBaseState _currentState;
        private WeaponStateFactory _states;

        public WeaponBaseState CurrentState
        {
            get { return _currentState; }
            set { _currentState = value; }
        }

        public WeaponStateFactory _States
        {
            get { return _states; }
            set { _states = value; }
        }

        public CanvasGroup inspectionUI;

        private void Awake()
        {
            _states = new WeaponStateFactory(this);
            _currentState = _states.Default();
            _currentState.EnterState();
        }

        private void Update()
        {
            _currentState.UpdateState();
        }

        private void FixedUpdate()
        {
            _currentState.FixedUpdateState();
        }
    }
}