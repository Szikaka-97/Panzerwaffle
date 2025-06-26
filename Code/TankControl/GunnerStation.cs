using System;
using ReAction;
using ReAction.Consts.Int;

namespace Panzerwaffle.TankControl {
	public class GunnerStation : TankerStation {
		[Property]
		private float MaxJoystickMovement { get; init; } = 50;
		[Property]
		private float Deadzone { get; init; } = 0.05f;
		[Property]
		private GameObject ScreenCenter { get; init; }

		[Property]
		private TurretController Turret { get; init; }

		private bool JoystickEnabled { get => ReAction.ReAction.ActionTriggered(ReActionConsts.TurretFuckstick); }

		public override bool LockView => JoystickEnabled || ReAction.ReAction.ActionTriggered(ReActionConsts.LookAtScreen);

		private Vector2 joystickPosition = Vector2.Zero;

		protected override void OnUpdate() {
			if (!JoystickEnabled) {
				this.joystickPosition = Vector2.Zero;
			}
			else {
				this.joystickPosition += (Vector2) Input.AnalogLook.AsVector3();
			}

			this.joystickPosition.x = Math.Clamp(this.joystickPosition.x, -MaxJoystickMovement, MaxJoystickMovement);
			this.joystickPosition.y = Math.Clamp(this.joystickPosition.y, -MaxJoystickMovement, MaxJoystickMovement);

			Vector2 relativeMove = this.joystickPosition / MaxJoystickMovement;

			if (Math.Abs(relativeMove.x) < Deadzone) {
				relativeMove.x = 0;
			}
			if (Math.Abs(relativeMove.y) < Deadzone) {
				relativeMove.y = 0;
			}

			relativeMove.x *= relativeMove.x * -Math.Sign(relativeMove.x);
			relativeMove.y *= relativeMove.y * -Math.Sign(relativeMove.y);

			Turret.Rotation += Turret.RotationSpeed * Time.Delta * relativeMove.y;
			Turret.Elevation += Turret.Cannon.ElevationSpeed * Time.Delta * relativeMove.x;

			if (Turret.Cannon.ReadyToFire && ReAction.ReAction.ActionTriggered(ReActionConsts.CannonFire)) {
				Turret.Cannon.Fire();
			}

			if (ScreenCenter != null && ReAction.ReAction.ActionTriggered(ReActionConsts.LookAtScreen)) {
				Tanker.LookAt(ScreenCenter.WorldPosition, 100 * Time.Delta);
			}
		}
	}
}