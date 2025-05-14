
using System;

namespace Panzerwaffle.Experimental {
	public class RayTraceTest : Component {
		[Property]
		private float size = 10;
		[Property]
		private float rotation = 0;

		protected override void DrawGizmos() {
			var startPos = this.WorldPosition + Vector3.Up * 50;
			var endPos = this.WorldPosition + Vector3.Down * 50;

			var hit = Scene.Trace.Rotated(Rotation.FromYaw(this.rotation)).Box(BBox.FromPositionAndSize(startPos, size), startPos, endPos).Run();

			if (hit.Hit) {
				Gizmo.Draw.Color = Gizmo.Colors.Green;
			}
			else {
				Gizmo.Draw.Color = Gizmo.Colors.Red;
			}

			Gizmo.Draw.SolidSphere(startPos, 25, 16, 16);
		}
	}
}