namespace cowsins
{
    public abstract class WeaponBaseState
    {
        protected WeaponStates _ctx;
        protected WeaponStateFactory _factory;
        protected WeaponBaseState _currentSuperState;

        protected WeaponBaseState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
        {
            _ctx = currentContext;
            _factory = playerStateFactory;
        }


        public abstract void EnterState();

        public abstract void UpdateState();

        public abstract void FixedUpdateState();

        public abstract void ExitState();

        public abstract void CheckSwitchState();

        void UpdateStates() { }

        protected void SwitchState(WeaponBaseState newState)
        {
            ExitState();

            newState.EnterState();

            _ctx.CurrentState = newState;
        }

        protected void SetSuperState(WeaponBaseState newSuperState)
        {
            _currentSuperState = newSuperState;
        }
    }
}