using System;
using System.Diagnostics;

public interface IIntersectable {
	public static Stopwatch IntersectionTimer = Stopwatch.StartNew();

	public struct Tetrahedron {
		public Vector3[] points = new Vector3[4];

		public Tetrahedron(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
			points[0] = a;
			points[1] = b;
			points[2] = c;
			points[3] = d;
		}

		static Vector3 ClosestPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c) {
			Vector3 ab = b - a;
			Vector3 ac = c - a;
			Vector3 ap = p - a;

			const float epsilon = 0f;

			float d1 = Vector3.Dot(ab, ap);
			float d2 = Vector3.Dot(ac, ap);
			if (d1 <= epsilon && d2 <= epsilon) return a; //#1

			Vector3 bp = p - b;
			float d3 = Vector3.Dot(ab, bp);
			float d4 = Vector3.Dot(ac, bp);
			if (d3 >= epsilon && d4 <= d3) return b; //#2

			Vector3 cp = p - c;
			float d5 = Vector3.Dot(ab, cp);
			float d6 = Vector3.Dot(ac, cp);
			if (d6 >= epsilon && d5 <= d6) return c; //#3

			float vc = d1 * d4 - d3 * d2;
			if (vc <= epsilon && d1 >= epsilon && d3 <= epsilon)
			{
				float vv = d1 / (d1 - d3);
				return a + vv * ab; //#4
			}
				
			float vb = d5 * d2 - d1 * d6;
			if (vb <= epsilon && d2 >= epsilon && d6 <= epsilon)
			{
				float vv = d2 / (d2 - d6);
				return a + vv * ac; //#5
			}
				
			float va = d3 * d6 - d5 * d4;
			if (va <= epsilon && (d4 - d3) >= epsilon && (d5 - d6) >= epsilon)
			{
				float vv = (d4 - d3) / ((d4 - d3) + (d5 - d6));
				return b + vv * (c - b); //#6
			}

			float denom = 1f / (va + vb + vc);
			float v = vb * denom;
			float w = vc * denom;
			return a + v * ab + w * ac; //#0
		}

		private bool SameSide(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
			Vector3 normal = Vector3.Cross(b - a, c - a);
			float dotV4 = Vector3.Dot(normal, d - a);
			float dotP = Vector3.Dot(normal, -a);
			return Math.Sign(dotV4) == Math.Sign(dotP);
		}

		public bool ContainsOrigin() {
			return (
				SameSide(points[0], points[1], points[2], points[3])
				&&
				SameSide(points[1], points[2], points[3], points[0])
				&&
				SameSide(points[2], points[3], points[0], points[1])
				&&
				SameSide(points[3], points[0], points[1], points[2])
			);
		}

		// TODO: Make a better name ffs
		public Vector3 GetNormalOfSideClosestToOrigin(out int index) {
			Vector3[] normals = [
				Vector3.Cross(points[2] - points[1], points[3] - points[1]),
				Vector3.Cross(points[2] - points[0], points[3] - points[0]),
				Vector3.Cross(points[3] - points[0], points[1] - points[0]),
				Vector3.Cross(points[2] - points[0], points[1] - points[0])
			];
			
			for (int i = 0; i < 4; i++) {
				if (Vector3.Dot(normals[i], points[i]) > 0) {
					normals[i] = -normals[i];
				}
			}

			float[] distances = new float[4];

			for (int i = 0; i < 4; i++) {
				if (Vector3.Dot(points[(i + 1) % 4], normals[i]) > 0) {
					distances[i] = float.MaxValue;
				}
				else {
					distances[i] = ClosestPointTriangle(Vector3.Zero, points[(i + 1) % 4], points[(i + 2) % 4], points[(i + 3) % 4]).LengthSquared;
				}
			}

			float minDistance = distances.Min();

			for (int i = 0; i < 4; i++) {
				if (distances[i] == minDistance) {
					index = i;

					return normals[i];
				}
			}

			// Realistically it'll never get here
			index = 0;
			return Vector3.Zero;
		}

		public bool ContainsVertex(Vector3 vertex) {
			const float epsilon = 0.0001f;

			return (
				points[0].DistanceSquared(vertex) < epsilon
				||
				points[1].DistanceSquared(vertex) < epsilon
				||
				points[2].DistanceSquared(vertex) < epsilon
				||
				points[3].DistanceSquared(vertex) < epsilon
			);
		}
	};

	Vector3 GetFurthestPoint(Vector3 direction);

	Vector3 WorldPosition { get; }

	static bool Intersection(IIntersectable first, IIntersectable second) {
		DebugOverlaySystem debug = null;
		if (second is IntersectableCylinder) {
			debug = (second as IntersectableCylinder).DebugOverlay;
		}

		const bool STRICT_DEBUG = false;
		const float epsilon = 0.0001f;

		Vector3 direction = second.WorldPosition - first.WorldPosition;
		
		// First support point, towards some arbitrary direction
		Vector3 A = first.GetFurthestPoint(direction) - second.GetFurthestPoint(-direction);

		direction = -A.Normal;

		// First support point, towards the origin from the first point
		Vector3 B = first.GetFurthestPoint(direction) - second.GetFurthestPoint(-direction);

		// Sanity Check
		if (Vector3.Dot(direction, B) <= epsilon) {
			return false;
		}

		// New direction, perpendicular to the line between the two points and pointing towards the origin
		direction = Vector3.Cross(B - A, direction).Cross(B - A).Normal;

		// Third point
		Vector3 C = first.GetFurthestPoint(direction) - second.GetFurthestPoint(-direction);

		if (Vector3.Dot(direction, C) <= epsilon) {
			return false;
		}

		direction = Vector3.Cross(B - A, C - A).Normal;

		if (Vector3.Dot(direction, A) > epsilon) {
			direction = -direction;
		}

		Vector3 D = first.GetFurthestPoint(direction) - second.GetFurthestPoint(-direction);

		if (Vector3.Dot(direction, D) <= epsilon) {
			return false;
		}

		Tetrahedron simplex = new Tetrahedron(A, B, C, D);

		// EZ
		if (simplex.ContainsOrigin()) {
			return true;
		}

		int index;

		const int limit = 64;

		for (int i = 0; i < limit; i++) {
			direction = simplex.GetNormalOfSideClosestToOrigin(out index);

			if (STRICT_DEBUG && i == limit - 1) {
				debug?.Line(Vector3.Zero, direction * 10, Color.Magenta, overlay: true);

				debug?.Text(A, "A" + i, overlay: true);

				// debug?.Line(A, B, Color.Cyan, overlay: true);
				// debug?.Line(A, C, Color.Cyan, overlay: true);
				// debug?.Line(A, D, Color.Cyan, overlay: true);
				// debug?.Line(B, C, Color.Cyan, overlay: true);
				// debug?.Line(B, D, Color.Cyan, overlay: true);
				// debug?.Line(C, D, Color.Cyan, overlay: true);

				switch (index) {
					case 0:
						debug?.Line(B, C, Color.Red, overlay: true);
						debug?.Line(B, D, Color.Red, overlay: true);
						debug?.Line(C, D, Color.Red, overlay: true);
						debug?.Line(A, B, Color.Cyan, overlay: true);
						debug?.Line(A, C, Color.Cyan, overlay: true);
						debug?.Line(A, D, Color.Cyan, overlay: true);
						break;
					case 1:
						debug?.Line(A, C, Color.Red, overlay: true);
						debug?.Line(A, D, Color.Red, overlay: true);
						debug?.Line(C, D, Color.Red, overlay: true);
						debug?.Line(A, B, Color.Cyan, overlay: true);
						debug?.Line(B, C, Color.Cyan, overlay: true);
						debug?.Line(B, D, Color.Cyan, overlay: true);
						break;
					case 2:
						debug?.Line(A, B, Color.Red, overlay: true);
						debug?.Line(A, D, Color.Red, overlay: true);
						debug?.Line(B, D, Color.Red, overlay: true);
						debug?.Line(A, C, Color.Cyan, overlay: true);
						debug?.Line(B, C, Color.Cyan, overlay: true);
						debug?.Line(C, D, Color.Cyan, overlay: true);
						break;
					case 3:
						debug?.Line(A, B, Color.Red, overlay: true);
						debug?.Line(A, C, Color.Red, overlay: true);
						debug?.Line(B, C, Color.Red, overlay: true);
						debug?.Line(A, D, Color.Cyan, overlay: true);
						debug?.Line(B, D, Color.Cyan, overlay: true);
						debug?.Line(C, D, Color.Cyan, overlay: true);
						break;
				}
			}

			A = first.GetFurthestPoint(direction) - second.GetFurthestPoint(-direction);

			if (Vector3.Dot(direction, A) <= epsilon || simplex.ContainsVertex(A)) {
				return false;
			}

			simplex.points[index] = A;

			if (simplex.ContainsOrigin()) {
				return true;
			}
		}

		Log.Error("Infinite loop detected");

		return false;
	}
}

public class IntersectableCollider : IIntersectable {
	public Collider Collider { get; init; }

	public IntersectableCollider(Collider collider) {
		Collider = collider;
	}

	public Vector3 WorldPosition {
		get => Collider.WorldPosition;
	}

	public Vector3 GetFurthestPoint(Vector3 direction) {
		var temp = Collider.FurthestPoint(direction);

		return temp;
	}
}

// Cylinder pointing forward (+x)
public class IntersectableCylinder : IIntersectable {
	public Transform WorldTransform { get; init; }
	public float Radius { get; init; }
	public float Height { get; init; }

	public DebugOverlaySystem DebugOverlay { get; init; } = null;

	public IntersectableCylinder(float radius, float height) {
		Radius = radius;
		Height = height;
		WorldTransform = Transform.Zero;
	}

	public IntersectableCylinder(float radius, float height, Transform worldTransform) {
		Radius = radius;
		Height = height;
		WorldTransform = worldTransform;
	}

	public IntersectableCylinder(float radius, float height, Transform worldTransform, DebugOverlaySystem debugOverlay) {
		Radius = radius;
		Height = height;
		WorldTransform = worldTransform;
		DebugOverlay = debugOverlay;
	}

	public Vector3 WorldPosition {
		get => WorldTransform.Position;
	}

	public Vector3 GetFurthestPoint(Vector3 direction) {
		var localDirection = WorldTransform.NormalToLocal(direction);

		Vector3 furthestPoint;

		if (localDirection.WithX(0).LengthSquared <= 0.0001) {
			if (localDirection.x < 0) {
				furthestPoint = WorldTransform.Position;
				
			}
			else {
				furthestPoint = WorldTransform.PointToWorld(Vector3.Forward * Height);
			}
		}
		else {
			float x = localDirection.x;
			localDirection = localDirection.WithX(0).Normal * Radius;

			if (x <= 0.0001) {
				furthestPoint = WorldTransform.PointToWorld(localDirection);
			}
			else {
				furthestPoint = WorldTransform.PointToWorld(localDirection.WithX(Height));
			}
		}

		return furthestPoint;
	}
}

static class Extensions {
	public static bool IntersectsWith(this Collider self, Collider other) {
		IIntersectable.IntersectionTimer.Start();

		var result = IIntersectable.Intersection(new IntersectableCollider(self), new IntersectableCollider(other));

		IIntersectable.IntersectionTimer.Stop();

		return result;
	}

	public static bool IntersectsWith(this Collider self, IIntersectable other) {
		IIntersectable.IntersectionTimer.Start();

		var result = IIntersectable.Intersection(new IntersectableCollider(self), other);

		IIntersectable.IntersectionTimer.Stop();

		return result;
	}

	public static Vector3 FurthestPoint(this BoxCollider self, Vector3 direction) {
		Vector3 localDir = self.WorldTransform.NormalToLocal(direction);

		Vector3 furthestPoint;

		localDir.x = Math.Sign(localDir.x);
		localDir.y = Math.Sign(localDir.y);
		localDir.z = Math.Sign(localDir.z);

		furthestPoint = self.WorldTransform.PointToWorld(self.Center + localDir * self.Scale / 2);

		return furthestPoint;
	}

	public static Vector3 FurthestPoint(this CapsuleCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this HullCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this MapCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this MeshComponent self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this ModelCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this PlaneCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this SphereCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this Terrain self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	public static Vector3 FurthestPoint(this Collider self, Vector3 direction) {
		switch (self) {
			case BoxCollider box:
				return box.FurthestPoint(direction);
			case CapsuleCollider capsule:
				return capsule.FurthestPoint(direction);
			case HullCollider hull:
				return hull.FurthestPoint(direction);
			case MapCollider map:
				return map.FurthestPoint(direction);
			case MeshComponent mesh:
				return mesh.FurthestPoint(direction);
			case ModelCollider model:
				return model.FurthestPoint(direction);
			case PlaneCollider plane:
				return plane.FurthestPoint(direction);
			case SphereCollider sphere:
				return sphere.FurthestPoint(direction);
			case Terrain terrain:
				return terrain.FurthestPoint(direction);
			default:
				throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
		}
	}
}