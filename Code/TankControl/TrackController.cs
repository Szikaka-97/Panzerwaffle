namespace Panzerwaffle.TankControl {
	class TrackController : Component, Component.ExecuteInEditor {
		[Property, ReadOnly]
		private TankController tank;
		[Property, ReadOnly, InlineEditor]
		private List<RotatableWheel> orderedWheels;
		[Property, ReadOnly]
		private BBox bounds;
		[Property, ReadOnly]
		private float trackThickness;
		[Property, ReadOnly]
		private float TrackWidth;

		protected override void OnAwake() {
			CheckChange();
		}

		protected override void OnUpdate() {
			// CheckChange();
		}

		private void CheckChange() {
			if (Application.IsEditor || this.tank == null) {
				var foundTank = GetComponentInParent<TankController>();

				if (foundTank != null) {
					this.tank = foundTank;

					this.tank.RegisterTrackController(this);
				}
			}

		}

		public void Initialize(RotatableWheel[] wheels, float trackThickness, float trackWidth, Transform worldTransform) {
			this.trackThickness = trackThickness;
			this.TrackWidth = trackWidth;

			float halfTrackWidth = trackWidth / 2;

			this.bounds = BBox.FromPositionAndSize(wheels[0].CenterPosition, new Vector3(
				wheels[0].diameter + trackThickness,
				trackWidth,
				wheels[0].diameter + trackThickness
			));

			for (int i = 1; i < wheels.Length; i++) {
				this.bounds.AddBBox(BBox.FromPositionAndSize(wheels[i].CenterPosition, new Vector3(
					wheels[i].diameter + trackThickness,
					trackWidth,
					wheels[i].diameter + trackThickness
				)));
			}

			var topWheels = wheels
				.Where( wheel => Vector3.Dot(wheel.WorldPosition, worldTransform.Up) > this.bounds.Center.z )
				.OrderBy( wheel => Vector3.Dot(wheel.WorldPosition, worldTransform.Forward));

			var bottomWheels = wheels
				.Where( wheel => Vector3.Dot(wheel.WorldPosition, worldTransform.Up) < this.bounds.Center.z )
				.OrderBy( wheel => Vector3.Dot(wheel.WorldPosition, worldTransform.Backward));
			
			this.orderedWheels = topWheels.Concat(bottomWheels).ToList();
		}
	}
}