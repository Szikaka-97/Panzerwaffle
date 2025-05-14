using System;

namespace Panzerwaffle {
	public sealed class Kerfus : Component {
		private SkinnedModelRenderer model;
		private CameraComponent camera;

		[Property]
		private Vector3 currentLookAt;
		[Property]
		private float horizontalLookRange = 60;
		[Property]
		private float upLookRange = 60;
		[Property]
		private float downLookRange = 40;

		protected override void OnAwake() {
			this.model = this.GetComponent<SkinnedModelRenderer>();
			this.camera = this.GetComponentInChildren<CameraComponent>();

			this.currentLookAt = Vector3.Forward;
		}

		protected override void OnUpdate() {
			var movement = Input.AnalogLook.AsVector3();

			this.currentLookAt += movement;

			this.currentLookAt.x = Math.Clamp(this.currentLookAt.x, -this.upLookRange, this.downLookRange);
			this.currentLookAt.y = Math.Clamp(this.currentLookAt.y, -this.horizontalLookRange, this.horizontalLookRange);

			var lookAtTarget = this.WorldTransform.NormalToWorld(Rotation.From(new Angles(this.currentLookAt)) * Vector3.Forward);
			
			this.model.SetLookDirection("LookAtTarget", lookAtTarget);
			this.camera.WorldRotation = Rotation.LookAt(lookAtTarget, Vector3.Up);
		}
	}
}