namespace cowsins
{
    public class PlayerStateFactory
    {
        private readonly PlayerStates _context;

        public PlayerStateFactory(PlayerStates currentContext)
        {
            _context = currentContext;
        }

        public PlayerBaseState Default()
        {
            return new PlayerDefaultState(_context, this);
        }

        public PlayerBaseState Jump()
        {
            return new PlayerJumpState(_context, this);
        }

        public PlayerBaseState Die()
        {
            return new PlayerDeadState(_context, this);
        }
    }
}