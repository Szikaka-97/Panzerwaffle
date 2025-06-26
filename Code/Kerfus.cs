using System;
using Panzerwaffle.TankControl;

namespace Panzerwaffle {
	public sealed class Kerfus : Component {
		private SkinnedModelRenderer model;
		private CameraComponent camera;
		[Property]
		private TankerStation station;

		[Property]
		private Vector3 currentLookAt;
		[Property]
		private float horizontalLookRange = 60;
		[Property]
		private float upLookRange = 60;
		[Property]
		private float downLookRange = 40;

		public void LookAt(Vector3 point, float maxDelta = float.PositiveInfinity) {
			Vector3 targetRot = Rotation.FromToRotation(Vector3.Forward, WorldTransform.NormalToLocal(point - this.camera.WorldPosition)).Angles().AsVector3();

			this.currentLookAt = MathY.MathY.MoveTowards(this.currentLookAt, targetRot, maxDelta);
		}

		protected override void OnAwake() {
			this.model = this.GetComponent<SkinnedModelRenderer>();
			this.camera = this.GetComponentInChildren<CameraComponent>();

			this.currentLookAt = Vector3.Forward;
		}

		protected override void OnUpdate() {

			if (!this.station.LockView) {
				var headMovement = Input.AnalogLook.AsVector3();

				this.currentLookAt += headMovement;
			}

			this.currentLookAt.x = Math.Clamp(this.currentLookAt.x, -this.upLookRange, this.downLookRange);
			this.currentLookAt.y = Math.Clamp(this.currentLookAt.y, -this.horizontalLookRange, this.horizontalLookRange);

			var lookAtTarget = this.WorldTransform.NormalToWorld(Rotation.From(new Angles(this.currentLookAt)) * Vector3.Forward);
			
			this.model.SetLookDirection("LookAtTarget", lookAtTarget);
			this.camera.WorldRotation = Rotation.LookAt(lookAtTarget, Vector3.Up);
		}
	}
}