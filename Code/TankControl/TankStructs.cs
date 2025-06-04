namespace Panzerwaffle.TankControl {
    record RotatableWheel(GameObject wheelObject, float diameter, float trackWidth) {
		public Vector3 Axis {
			get => wheelObject.WorldTransform.Right;
		}

		public Rotation Rotation {
			get => wheelObject.LocalRotation;
			set => wheelObject.LocalRotation = value;
		}

		public float Radius {
			get => diameter / 2;
		}

		public Vector3 WorldPosition {
			get => wheelObject.WorldPosition;
		}

		public Vector3 CenterPosition {
			get => wheelObject.WorldPosition - this.Axis * (trackWidth / 2);
		}
	}

	class SuspensionArm {
		public GameObject armBone {
			get;
			init;
		}

		public RotatableWheel wheel {
			get;
			init;
		}

		public float Rotation {
			get => _rotation;
			set {
				this.armBone.LocalRotation = baseRotation * global::Rotation.FromYaw((_rotation = value) * this.side);
			}
		}

		private Rotation baseRotation;
		private float side;
		private float _rotation;

		public float armLength {
			get => armBone.Children[0].LocalPosition.Length;
		}

		public SuspensionArm(GameObject armBone, RotatableWheel attachedWheel) {
			this.armBone = armBone;
			this.baseRotation = armBone.LocalRotation;
			this.wheel = attachedWheel;

			var controller = armBone.GetComponentInParent<TankController>().WorldTransform;

			this.side = Vector3.Dot(controller.Right, controller.Position - this.armBone.WorldPosition);

			if (side > 0) {
				side = 1;
			}
			else {
				side = -1;
			}
		}
	}
}