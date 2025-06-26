using System;
using Sandbox;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Panzerwaffle.TankControl {
	public class TurretController : Component {
		public class Gun {
			public enum BreechBlockState {
				Open,
				Closed
			}

			[Hide]
			private float _angle = 0;
			[Hide]
			private float _currentAngle = 0;
			[Hide]
			private float _currentRecoilMovement = 0;
			[Hide]
			private float _currentRecoilForce = 0;
			[Hide]
			private float _currentRecoilVelocity = 0;
			[ReadOnly]
			Rotation initialRotation;
			[ReadOnly]
			Vector3 initialPosition;

			[Hide]
			public bool RoundLoaded { get; private set; }
			[Hide]
			public BreechBlockState BreechState { get; set; }
			[ReadOnly, Order(500)]
			public float Angle {
				get => Angles.Clamp(_angle);
				set => _angle = Angles.Clamp(value);
			}
			[ReadOnly, Order(500)]
			public float ActualAngle {
				get => CannonBone.LocalRotation.Roll();
			}
			[ReadOnly, Order(500)]
			public bool Recoiling {
				get => _currentRecoilMovement > 0 || _currentRecoilForce != 0;
			}
			[ReadOnly, Order(500)]
			public bool ReadyToFire {
				get => !Recoiling;
			}

			[Property]
			public GameObject CannonBone { get; init;}
			[Property]
			public GameObject BreechBone { get; init; }
			[Property]
			public FloatRange Angles { get; init; }
			[Property]
			public float ElevationSpeed { get; init; } = 1;
			[Property]
			public float RecoilStroke { get; init; }

			public void ResetRotation() {
				if (CannonBone == null) {
					return;
				}

				this.initialRotation = this.CannonBone.LocalRotation;
				this.initialPosition = this.CannonBone.LocalPosition;
			}

			public void UpdateElevation() {
				if (CannonBone == null) {
					return;
				}

				this._currentAngle = this._currentAngle.Approach(Angle, Time.Delta * ElevationSpeed);

				CannonBone.LocalRotation = this.initialRotation * global::Rotation.FromRoll(this._currentAngle);
			}

			public void Fire() {
				_currentRecoilForce = 4000;
			}

			public void UpdateRecoil() {
				_currentRecoilVelocity += _currentRecoilForce * Time.Delta;
				_currentRecoilMovement += _currentRecoilVelocity * Time.Delta;

				if (_currentRecoilMovement >= RecoilStroke) {
					_currentRecoilMovement = RecoilStroke;
					_currentRecoilVelocity = 0;
					_currentRecoilForce = -250;
				}
				if (_currentRecoilMovement < 0) {
					_currentRecoilMovement = 0;
					_currentRecoilVelocity = 0;
					_currentRecoilForce = 0;
				}

				this.CannonBone.LocalPosition = initialPosition + CannonBone.LocalTransform.Right * _currentRecoilMovement;
			}
		}

		private float _elevation = 0;
		private float _currentTrain = 0;
		private float _train = 0;

		private bool _vertical_stab = true;
		private bool _horizontal_stab = true;

		[Property, InlineEditor]
		public Gun Cannon { get; init; } = new Gun();
		[Property]
		public GunnerSight Sight { get; init; }
		[Property]
		public FloatRange ElevationRange { get; init; } = new FloatRange(-20, 20);
		[Property]
		public float Elevation {
			get => ElevationRange.Clamp(this._elevation);
			set => this._elevation = ElevationRange.Clamp(value);
		}
		[Property]
		public float RotationSpeed { get; init; } = 60;
		[Property]
		public float Rotation {
			get => this._train;
			set => this._train = (value + 360) % 360;
		}
		[Property]
		public float SightingRange { get; set; } = 1000;
		[Property]
		public bool VerticallyStabilized {
			get => _vertical_stab;
			set {
				if (_vertical_stab == value) {
					return;
				}

				if (value) {
					Elevation -= HullPitch();
				}
				else {
					Elevation += HullPitch();
				}

				_vertical_stab = value;
			}
		}
		[Property]
		public bool HorizontallyStabilized {
			get => _horizontal_stab;
			set {
				if (_horizontal_stab == value) {
					return;
				}

				if (value) {
					Rotation -= HullAngle();
				}
				else {
					Rotation += HullAngle();
				}

				_horizontal_stab = value;
			}
		}

		private float HullPitch() {
			return -WorldRotation.Pitch();
		}

		private float HullAngle() {
			return ((GameObject.Parent.WorldRotation.Yaw() + 360) % 360) - 180;
		}

		private float ApproachWrapped(float current, float target, float maxDelta, float lap = 360) {
			if (target == current) {
				return current;
			}

			if (target > current && target - current > Math.Abs(target - lap - current)) {
				current += lap;
			}
			else if (target < current && current - target > Math.Abs(current - lap - target)) {
				current -= lap;
			}

			return current.Approach(target, maxDelta);
		}

		protected override void OnAwake() {
			Cannon.Angle = 0;
			Cannon.ResetRotation();
		}

		protected override void OnUpdate() {
			Vector3 aimPoint = Sight.SightCamera.WorldPosition + (Sight.SightCamera.WorldTransform.Forward * SightingRange);
			Vector3 aimPointDelta = aimPoint - Cannon.CannonBone.WorldPosition;

			float x = Vector3.Dot(aimPointDelta, WorldTransform.Forward);
			float y = Vector3.Dot(aimPointDelta, Vector3.Up);
			float angle = MathX.RadianToDegree((float) Math.Atan2(y, -x));

			if (VerticallyStabilized) {

				Cannon.Angle = angle + HullPitch();
				Sight.LocalRotation = global::Rotation.FromYaw(-Elevation - HullPitch());
			}
			else {
				Cannon.Angle = angle;
				Sight.LocalRotation = global::Rotation.FromYaw(-Elevation);
			}

			Cannon.UpdateElevation();

			if (HorizontallyStabilized) {
				this._currentTrain = ApproachWrapped(this._currentTrain, Rotation + HullAngle(), RotationSpeed * Time.Delta);

				LocalRotation = global::Rotation.FromPitch(-this._currentTrain);
			}
			else {
				this._currentTrain = ApproachWrapped(this._currentTrain, Rotation, RotationSpeed * Time.Delta);

				LocalRotation = global::Rotation.FromPitch(-this._currentTrain);
			}

			this._currentTrain %= 360;

			Sight.UpdateDisplay(
				turretRotation: this._currentTrain,
				rangeMonitorReadout: (uint) MathX.InchToMeter(SightingRange)
			);

			if (Cannon.Recoiling) {
				Cannon.UpdateRecoil();
			}
		}
	}
}