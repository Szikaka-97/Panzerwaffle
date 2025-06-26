
namespace Panzerwaffle.TankControl {
	public sealed class TankDriverStation : Component {
		[Property]
		private TankController tank;

		protected override void OnUpdate() {
			this.tank.Throttle = Input.Keyboard.Down("W") ? 1 : 0;
			this.tank.Throttle = Input.Keyboard.Down("S") ? this.tank.Throttle - 1 : this.tank.Throttle;

			this.tank.BrakeForceLeft = Input.Keyboard.Down("A") ? 1 : 0;
			this.tank.BrakeForceRight = Input.Keyboard.Down("D") ? 1 : 0;
		}
	}
}