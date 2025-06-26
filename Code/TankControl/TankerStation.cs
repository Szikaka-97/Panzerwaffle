namespace Panzerwaffle.TankControl {
    public abstract class TankerStation : Component {
        [Property]
        public virtual Kerfus Tanker {
            get;
            set;
        }

        public virtual bool LockView {
            get => false;
        }

        public virtual bool LockMovement {
            get => true;
        }
    }
}