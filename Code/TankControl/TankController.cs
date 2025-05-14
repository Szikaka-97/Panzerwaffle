#pragma warning disable IDE0044 // Add readonly modifier

using System;
using System.Diagnostics;
using Sandbox.Rendering;
using Sandbox.Utility;

namespace Panzerwaffle.TankControl {
	public class TankController : Component {
		readonly float TorsionBarReturnSpeed = 3f;

		private float brakeForceLeft = 0;
		private float brakeForceRight = 0;
		private Transform localWheelbaseCenterLeft;
		private Transform localWheelbaseCenterRight;
		private Transform localPivotPoint;
		private List<RotatableWheel> wheels = new List<RotatableWheel>();
		private List<SuspensionArm> suspensionArms = new List<SuspensionArm>();
		private TrackController leftTrack;
		private TrackController rightTrack;

		private float Width => Vector3.DistanceBetween(localWheelbaseCenterLeft.Position, localWheelbaseCenterRight.Position);

		private Vector3 WheelPivot => (localWheelbaseCenterLeft.Position + localWheelbaseCenterRight.Position) / 2;

		public float Throttle {
			get => this.Engine.Throttle;
			set {
				this.Engine.Throttle = value;
			}
		}

		public float BrakeForceLeft {
			get => this.brakeForceLeft;
			set {
				this.brakeForceLeft = Math.Clamp(value, 0, 1);
			}
		}

		public float BrakeForceRight {
			get => this.brakeForceRight;
			set {
				this.brakeForceRight = Math.Clamp(value, 0, 1);
			}
		}

		[Property(Title = "Road Wheels")]
		public List<GameObject> WheelsObjects {get; private set;} = new List<GameObject>();
		[Property]
		public float WheelDiameter {get; private set;} = 0;
		[Property]
		public List<GameObject> Sprockets {get; private set;} = null;
		[Property]
		public float SprocketDiameter {get; private set;} = 0;
		[Property]
		public List<GameObject> IdlerWheels {get; private set;} = null;
		[Property]
		public float IdlerDiameter {get; private set;} = 0;
		[Property(Title = "Suspension Arms")]
		public List<GameObject> SuspensionArmObjects {get; private set;} = new List<GameObject>();
		[Property]
		public float TrackWidth {get; private set;} = 0;
		[Property]
		public float TrackThickness {get; private set;} = 0;
		[Property]
		public PowerPack Engine {get; private set;} = null;

		[Property, Group("Debug")]
		private bool visualizeWheels;
		[Property, Group("Debug")]
		private bool visualizeSuspensionBounds;

		private void RecalculateWheelPositions() {
			this.wheels.Clear();

			this.localWheelbaseCenterRight = new Transform();
			this.localWheelbaseCenterLeft = new Transform();

			int rightWheelCount = 0;
			int leftWheelCount = 0;

			foreach (var wheel in WheelsObjects) {
				if (Vector3.Dot(wheel.WorldPosition - WorldPosition, WorldTransform.Right) > 0) {
					this.localWheelbaseCenterRight.Position += WorldTransform.PointToLocal(wheel.WorldPosition);
					rightWheelCount++;
				}
				else {
					this.localWheelbaseCenterLeft.Position += WorldTransform.PointToLocal(wheel.WorldPosition);

					leftWheelCount++;
				}

				this.wheels.Add(new RotatableWheel(wheel, WheelDiameter, TrackWidth));
			}

			this.localWheelbaseCenterLeft.Position /= rightWheelCount;
			this.localWheelbaseCenterLeft.Position /= leftWheelCount;

			this.localPivotPoint = new Transform() {
				Position = (localWheelbaseCenterLeft.Position + this.localWheelbaseCenterRight.Position) / 2
			};

			foreach (var sprocket in Sprockets) {
				this.wheels.Add(new RotatableWheel(sprocket, SprocketDiameter, TrackWidth));
			}
			foreach (var idler in IdlerWheels) {
				this.wheels.Add(new RotatableWheel(idler, IdlerDiameter, TrackWidth));
			}
		}

		private void InitSuspension() {
			this.suspensionArms.Clear();

			foreach (var armObject in this.SuspensionArmObjects) {
				var attachedWheel = this.wheels.FirstOrDefault( wheel => wheel.wheelObject.Parent == armObject );

				if (attachedWheel == null) {
					Log.Error(String.Format("Suspension arm object {} has no wheels attached!", armObject));

					continue;
				}

				this.suspensionArms.Add(new SuspensionArm(armObject, attachedWheel));
			}

			// Bones [0] and [1] are the rearmost ones, [^1] and [^2] are the forward most ones
			this.suspensionArms.Sort( (arm1, arm2) => (int) Vector3.Dot(arm1.armBone.WorldPosition - arm2.armBone.WorldPosition, this.WorldTransform.Forward));
		}

		private void CalculateMovement(ref Vector3 position, ref Vector3 forward, out float movementLeft, out float movementRight) {
			float v = this.Engine.GetOutputSpeed() * this.SprocketDiameter * Time.Delta;

			movementLeft = v * (1 - this.brakeForceLeft);
			Vector3 leftTrackPosition = position + this.WorldTransform.Left * (this.Width / 2) + this.WorldTransform.Forward * movementLeft;
			movementRight = v * (1 - this.brakeForceRight);
			Vector3 rightTrackPosition = position + this.WorldTransform.Right * (this.Width / 2) + this.WorldTransform.Forward * movementRight;

			double angle = (movementLeft - movementRight) / this.Width;

			Ray b = new Ray(leftTrackPosition, (rightTrackPosition - leftTrackPosition).Normal);
			Vector3? pivot = new Plane(position, this.WorldTransform.Forward).Trace(b, true);

			if (pivot.HasValue) {
				position = position.RotateAround(pivot.Value, Rotation.FromAxis(this.WorldTransform.Up, MathX.RadianToDegree((float) -angle)));
				forward = forward.RotateAround(Vector3.Zero, Rotation.FromAxis(this.WorldTransform.Up, MathX.RadianToDegree((float) -angle)));
			}
			else {
				position += this.WorldTransform.Forward * movementLeft;
			}
		}

		private void UpdateWheelRotations(float movementLeft, float movementRight) {
			foreach (var wheel in this.wheels) {
				float angle = MathX.RadianToDegree(
					(float) ((Vector3.Dot(wheel.Axis, this.WorldTransform.Right) > 0 ? -movementLeft : movementRight) / (Math.PI * wheel.diameter))
				);

				wheel.Rotation *= Rotation.FromAxis(Vector3.Right, angle);
			}
		}

		private void UpdateSuspension() {
			bool TestCollision(SuspensionArm arm, List<Collider> closeColliders) {
				IntersectableCylinder wheelCylinder = new IntersectableCylinder(arm.wheel.Radius, TrackWidth, new Transform() {
					Position = arm.wheel.WorldPosition,
					Rotation = Rotation.LookAt(-arm.wheel.Axis)
				});

				foreach (Collider collider in closeColliders) {
					if (collider.IntersectsWith(wheelCylinder)) {
						return true;
					};
				}

				return false;
			}

			var traceBox = BBox.FromPositionAndSize(
				Vector3.Zero,
				this.GetComponent<ModelRenderer>().Bounds.Size.WithZ(0)
			);

			var blockers = Scene.Trace.Box(
				traceBox, WorldPosition + Vector3.Up * 20, WorldPosition + Vector3.Down * 20
			).Rotated(
				Rotation.Identity
			).RunAll();

			const int PRECISION = 3;

			Parallel.ForEach(this.suspensionArms, arm => {
				List<Collider> closeColliders = new List<Collider>(blockers.Count());

				double suspensionArmBounds = (
					(arm.armLength + arm.wheel.Radius) * (arm.armLength + arm.wheel.Radius)
					+
					TrackWidth * TrackWidth / 4
				);

				closeColliders.Clear();
				foreach (SceneTraceResult blocker in blockers) {
					if (blocker.Collider.FindClosestPoint(arm.armBone.WorldPosition).DistanceSquared(arm.armBone.WorldPosition) <= suspensionArmBounds) {
						closeColliders.Add(blocker.Collider);
					}
				}

				if (!TestCollision(arm, closeColliders)) {
					arm.Rotation = MathX.Approach(arm.Rotation, 0, TorsionBarReturnSpeed * 90 * Time.Delta);

					return;
				}

				float low_angle = arm.Rotation;
				float high_angle = Math.Min(low_angle + 5, 90);

				arm.Rotation = high_angle;

				if (TestCollision(arm, closeColliders)) {
					return;
				}

				for (int i = 0; i < PRECISION; i++) {
					float temp = (low_angle + high_angle) / 2;

					arm.Rotation = temp;

					if (TestCollision(arm, closeColliders)) {
						low_angle = temp;
					}
					else {
						high_angle = temp;
					}
				}

				arm.Rotation = low_angle;
			});
		}

		// A lot worse than the previous one, but a lot faster
		private void UpdateSuspensionFast() {
			foreach (var arm in this.suspensionArms) {
				Vector3 startPos = arm.armBone.WorldPosition - arm.wheel.CenterPosition;
				float length = arm.armLength + this.WheelDiameter / 2 + this.TrackThickness;
				Vector3 down = this.WorldTransform.Down;
				Vector3 extents = new Vector3(
					length * 2,
					this.TrackWidth,
					0
				);

				var objectsInBounds = Scene.Trace
					.Rotated(this.WorldRotation)
					.Box(extents, new Ray(startPos, down), length)
					.RunAll()
					.ToArray();

				if (objectsInBounds.Length > 0) {
					float deviation = 0;

					foreach (var hit in objectsInBounds) {
						if (hit.Body != null) {
							float dist = hit.Body.FindClosestPoint(arm.wheel.WorldPosition).Distance(arm.wheel.WorldPosition);

							float newDev = this.WheelDiameter / 2 - dist + this.TrackThickness;

							if (dist <= this.WheelDiameter / 2 + this.TrackThickness && newDev > deviation) {
								deviation = newDev;
							}
						}
					}

					if (deviation != 0) {
						if (deviation > 0.01 && deviation < arm.armLength) {
							float angle = MathX.RadianToDegree((float) Math.Asin(deviation / arm.armLength));

							angle *= Vector3.Dot(arm.wheel.Axis, this.WorldTransform.Right);

							arm.Rotation += angle;
						}
					}
					else{ 
						arm.Rotation = MathX.Approach(arm.Rotation, 0, TorsionBarReturnSpeed * 90 * Time.Delta);
					}
				}
				else {
					arm.Rotation = MathX.Approach(arm.Rotation, 0, TorsionBarReturnSpeed * 90 * Time.Delta);
				}
			}
		}

		private Mesh UpdateTrackSingle(IEnumerable<RotatableWheel> sideWheels) {
			BBox trackBounds = new BBox();

			bool first = true;
			foreach (var wheel in sideWheels) {
				var localPos = this.WorldTransform.PointToLocal(wheel.CenterPosition);

				var size = new Vector3(wheel.diameter, this.TrackWidth, wheel.diameter);

				if (first) {
					trackBounds = BBox.FromPositionAndSize(localPos, size);

					first = false;
				}
				else {
					trackBounds = trackBounds.AddBBox(BBox.FromPositionAndSize(localPos, size));
				}
			}

			DebugOverlay.Box(trackBounds, Color.Blue, transform: this.WorldTransform);

			var topWheels = sideWheels
				.Where( wheel => Vector3.Dot(wheel.WorldPosition, this.WorldTransform.Up) > trackBounds.Center.z )
				.OrderBy( wheel => Vector3.Dot(wheel.WorldPosition, this.WorldTransform.Forward));

			var bottomWheels = sideWheels
				.Where( wheel => Vector3.Dot(wheel.WorldPosition, this.WorldTransform.Up) < trackBounds.Center.z )
				.OrderBy( wheel => Vector3.Dot(wheel.WorldPosition, this.WorldTransform.Backward));

			var orderedWheels = topWheels.Concat(bottomWheels).ToArray();

			Mesh result = new Mesh(Material.FromShader("complex.shader"));

			List<Vector3> vertexBuffer = new List<Vector3>();
			List<int> indexBuffer = new List<int>();
			
			const float MaxDeviation = 0.9f;

			vertexBuffer.Add(orderedWheels[0].WorldPosition + this.WorldTransform.Up * (orderedWheels[0].Radius + this.TrackThickness) - orderedWheels[0].Axis * this.TrackWidth);
			vertexBuffer.Add(orderedWheels[0].WorldPosition + this.WorldTransform.Up * (orderedWheels[0].Radius + this.TrackThickness));
			vertexBuffer.Add(orderedWheels[1].WorldPosition + this.WorldTransform.Up * (orderedWheels[1].Radius + this.TrackThickness) - orderedWheels[1].Axis * this.TrackWidth);
			vertexBuffer.Add(orderedWheels[1].WorldPosition + this.WorldTransform.Up * (orderedWheels[1].Radius + this.TrackThickness));

			indexBuffer.AddRange([0, 1, 2, 1, 3, 2]);

			for (int i = 1; i < orderedWheels.Length + 1;) {
				var wheel = orderedWheels[i % orderedWheels.Length];
				var nextWheel = orderedWheels[(i + 1) % orderedWheels.Length];

				float wheelRadius = wheel.Radius + this.TrackThickness;
				float nextWheelRadius = nextWheel.Radius + this.TrackThickness;

				DebugOverlay.Box(wheel.CenterPosition, wheel.diameter, Color.Red);
				DebugOverlay.Text(wheel.WorldPosition - wheel.Axis * this.TrackWidth, i.ToString() + " > " + ((i + 1) % orderedWheels.Length).ToString());

				//CALCULATE TANGENT
				Vector2 secondCenter = new Vector2(
					Vector3.Dot(nextWheel.WorldPosition - wheel.WorldPosition, this.WorldTransform.Forward),
					Vector3.Dot(nextWheel.WorldPosition - wheel.WorldPosition, this.WorldTransform.Up)
				);

				double r = nextWheelRadius - wheelRadius;
				double z = secondCenter.x*secondCenter.x + secondCenter.y*secondCenter.y;
				double d = z - r*r;
				d = Math.Sqrt(Math.Abs(d));

				double a = (secondCenter.x * r + secondCenter.y * d) / z;
				double b = (secondCenter.y * r - secondCenter.x * d) / z;
				double c = wheelRadius;

				double intersectionX = (-2 * a * c) / (2 * (a*a + b*b));
				double intersectionY = (-a * intersectionX - c) / b;
				
				Vector3 pointToAdd = this.WorldTransform.PointToWorld(
					this.WorldTransform.PointToLocal(wheel.WorldPosition) + new Vector3(
						(float) intersectionX,
						0,
						(float) intersectionY
					)
				);

				var lastVertex = vertexBuffer[^1];

				if (Vector3.Dot(
					(lastVertex - wheel.WorldPosition).Normal,
					(pointToAdd - wheel.WorldPosition).Normal
				) < MaxDeviation) { // Need more segments on the wheel
					var offset = lastVertex - wheel.WorldPosition;

					pointToAdd = Rotation.FromAxis(-wheel.Axis, MathX.RadianToDegree((float) Math.Acos(MaxDeviation))) * offset + wheel.WorldPosition;

					vertexBuffer.Add(pointToAdd - wheel.Axis * this.TrackWidth);
					vertexBuffer.Add(pointToAdd);

					int index = vertexBuffer.Count;
					indexBuffer.AddRange([index - 4, index - 3, index - 2, index - 3, index - 1, index - 2]);
				}
				else {
					intersectionX = -((-2 * b*b * secondCenter.x) + (2 * a * c) + (2 * secondCenter.y * a * b)) / (2 * (a*a + b*b));
					intersectionY = (-a * intersectionX - c) / b;

					if (i % orderedWheels.Length == 0) {
						break;
					}

					pointToAdd = this.WorldTransform.PointToWorld(
						this.WorldTransform.PointToLocal(wheel.WorldPosition) + new Vector3(
							(float) intersectionX,
							0,
							(float) intersectionY
						)
					);

					vertexBuffer.Add(pointToAdd - wheel.Axis * this.TrackWidth);
					vertexBuffer.Add(pointToAdd);

					int index = vertexBuffer.Count;
					indexBuffer.AddRange([index - 4, index - 3, index - 2, index - 3, index - 1, index - 2]);

					i++;
				}
			}

			for (int i = 0; i < vertexBuffer.Count; i++) {
				var vertex = vertexBuffer[i];
				var nextVertex = vertexBuffer[(i + 1) % vertexBuffer.Count];

				DebugOverlay.Line(vertex, nextVertex, Color.Black);
			}

			result.CreateVertexBuffer(vertexBuffer.Count, [new VertexAttribute(VertexAttributeType.Position, VertexAttributeFormat.Float32, 3)], vertexBuffer);
			result.CreateIndexBuffer(indexBuffer.Count, indexBuffer);
			result.Bounds = trackBounds;

			return result;
		}

		internal void RegisterTrackController(TrackController track) {
			if (Vector3.Dot(track.WorldPosition - this.WorldPosition, this.WorldTransform.Right) > 0) {
				if (this.rightTrack == null || !this.rightTrack.Active || this.rightTrack == track) {
					this.rightTrack = track;

					RecalculateWheelPositions();

					track.Initialize(
						this.wheels.Where( wheel => Vector3.Dot(wheel.WorldPosition - this.WorldPosition, this.WorldTransform.Right) > 0 ).ToArray(),
						TrackThickness,
						TrackWidth,
						WorldTransform
					);
				}
				else {
					Log.Error("Tank: " + GameObject.Name + " already has a right track");
					Log.Error(this.rightTrack);
				}
			}
			else {
				if (this.leftTrack == null || !this.leftTrack.Active || this.leftTrack == track) {
					this.leftTrack = track;

					RecalculateWheelPositions();

					track.Initialize(
						this.wheels.Where( wheel => Vector3.Dot(wheel.WorldPosition - this.WorldPosition, this.WorldTransform.Right) < 0 ).ToArray(),
						TrackThickness,
						TrackWidth,
						WorldTransform
					);
				}
				else {
					Log.Error("Tank: " + GameObject.Name + " already has a left track");
				}
			}
		}

		private void UpdateTrack() {
			renderList.Reset();

			var leftWheels = this.wheels.Where( wheel => Vector3.Dot(wheel.Axis, this.WorldTransform.Right) > 0 );
			var rightWheels = this.wheels.Where( wheel => Vector3.Dot(wheel.Axis, this.WorldTransform.Right) > 0 );

			Mesh left = UpdateTrackSingle(leftWheels);

			Transform modelTransform = new Transform
			{
				Position = Vector3.Zero,
				Rotation = Rotation.Identity,
				Scale = Vector3.One
			};

			// var attrs = new RenderAttributes();

			renderList.DrawModel(Model.Builder.AddMesh(left).Create(), modelTransform);
		}

		// DEBUG
		private CommandList renderList;

		protected override void OnAwake() {
			RecalculateWheelPositions();

			InitSuspension();

			renderList = new CommandList("Track Render Commands");

			Scene.GetComponentInChildren<CameraComponent>().AddCommandList(renderList, Stage.AfterOpaque);
		}

		protected override void OnUpdate() {
			Vector3 currentPos = WorldTransform.Position + WheelPivot;
			Vector3 currentForward = WorldTransform.Forward;

			CalculateMovement(ref currentPos, ref currentForward, out float moveLeft, out float moveRight);

			UpdateWheelRotations(moveLeft, moveRight);

			WorldPosition = currentPos - WheelPivot;
			WorldRotation = Rotation.LookAt(currentForward, WorldTransform.Up);

			Stopwatch susWatch = Stopwatch.StartNew();

			UpdateSuspension();

			DebugOverlay.ScreenText(Vector2.Left * 10 + Vector2.Up * 30, "Total: " + susWatch.Elapsed.TotalMilliseconds, flags: TextFlag.Left);
			// DebugOverlay.ScreenText(Vector2.Left * 10 + Vector2.Up * 50, "Inter: " + IIntersectable.IntersectionTimer.Elapsed.TotalMilliseconds, flags: TextFlag.Left);
			// DebugOverlay.ScreenText(Vector2.Left * 10 + Vector2.Up * 70, "Ratio: " + IIntersectable.IntersectionTimer.Elapsed.TotalMilliseconds / susWatch.Elapsed.TotalMilliseconds, flags: TextFlag.Left);

			IIntersectable.IntersectionTimer.Reset();
			
			// UpdateTrack();
		}

		protected override void DrawGizmos() {
			if (this.visualizeWheels) {
				foreach (var wheel in this.wheels) {
					Gizmo.Draw.IgnoreDepth = false;
					Gizmo.Draw.Color = Gizmo.Colors.Active;

					Gizmo.Draw.LineCircle(this.WorldTransform.PointToLocal(wheel.CenterPosition), Vector3.Right, wheel.Radius);

					Gizmo.Draw.Color = Gizmo.Colors.Blue;
					Gizmo.Draw.IgnoreDepth = true;

					Gizmo.Draw.Line(
						this.WorldTransform.PointToLocal(wheel.WorldPosition),
						this.WorldTransform.PointToLocal(wheel.WorldPosition - wheel.Axis * 10)
					);
				}
			}
			if (this.visualizeSuspensionBounds) {
				foreach (var arm in this.suspensionArms) {
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.Color = Gizmo.Colors.Red;

					float length = arm.armLength + this.WheelDiameter / 2 + this.TrackThickness;
					Vector3 extents = new Vector3(
						length * 2,
						this.TrackWidth,
						length
					);

					Gizmo.Draw.LineBBox(
						BBox.FromPositionAndSize(
							this.WorldTransform.PointToLocal(arm.armBone.WorldPosition - arm.wheel.CenterPosition + Vector3.Down * length / 2),
							extents
						)
					);
				}
			}

			base.DrawGizmos();
		}
	}
}

#pragma warning restore IDE0044 // Add readonly modifier