using System;

/// <summary>
/// Allows for intersection testing between this class and other colliders
/// </summary>
public interface IIntersectable {
	/// <summary>
	/// Returns the furthest point on this body in the given direction
	/// </summary>
	/// <param name="direction"> World space normalized direction vector </param>
	/// <returns> World space position of the point on this body that's the furthest in the given direction </returns>
	Vector3 GetFurthestPoint(Vector3 direction);

	/// <summary>
	/// World position of the body, optional (simply returning 0 does the job)
	/// </summary>
	/// <remarks>
	/// While optional, returning a point in the middle of the body might speed up calculations
	/// </remarks>
	Vector3 WorldPosition { get; }
}

/// <summary>
/// Class for managing intersections between objects
/// </summary>
/// <remarks>
/// This class uses the 3D GJK algorithm to test for intersections
/// </remarks>
public static class Intersections {
	/// <summary>
	/// A 3D simplex, used by GJK
	/// </summary>
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
			if (vc <= epsilon && d1 >= epsilon && d3 <= epsilon) {
				float vv = d1 / (d1 - d3);
				return a + vv * ab; //#4
			}

			float vb = d5 * d2 - d1 * d6;
			if (vb <= epsilon && d2 >= epsilon && d6 <= epsilon) {
				float vv = d2 / (d2 - d6);
				return a + vv * ac; //#5
			}

			float va = d3 * d6 - d5 * d4;
			if (va <= epsilon && (d4 - d3) >= epsilon && (d5 - d6) >= epsilon) {
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

	/// <summary>
	/// Test if the collider type supports intersection testing. Some types might not be implemented
	/// </summary>
	/// <typeparam name="T">
	/// Type of the collider
	/// </typeparam>
	/// <returns>
	/// True if the collider supports intersections, false otherwise
	/// </returns>
	/// <remarks>
	/// To support intersections the class must fulfill one of two requirements: <para />
	/// - For builtin colliders: A FurthestPoint extension method must be defined <para />
	/// - For custom colliders: The collider must implement the <see cref="IIntersectable"/> interface
	/// </remarks>
	public static bool Supported<T>() where T : Collider, new() {
		try {
			T c = new T();

			c.FurthestPoint(Vector3.Forward);

			return true;
		} catch (NotImplementedException) {
			return false;
		} catch {
			return true;
		}
	}

	/// <summary>
	/// Test for intersection between 2 colliders
	/// </summary>
	/// <param name="first"> First of the colliders, must not be null and satisfy <see cref="Intersections.Supported{T}"/>> </param>
	/// <param name="second"> Second of the colliders, must not be null and satisfy <see cref="Intersections.Supported{T}"/>> </param>
	/// <returns> True if the colliders intersect, false otherwise </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the 2 colliders is null </exception>
	/// <remarks>
	/// In the event that the method fails to conclude if an intersection occurs, it will assume false and print an error message to the console. <para />
	/// The order of the operands doesn't matter, that is <c> Test(first, second) == Test(second, first) </c>
	/// </remarks>
	public static bool Test(Collider first, Collider second) {
		ArgumentNullException.ThrowIfNull(first, nameof(first));
		ArgumentNullException.ThrowIfNull(second, nameof(second));

		return TestInternal(new IntersectableCollider(first), new IntersectableCollider(second));
	}

	/// <summary>
	/// Test for intersection between an <see cref="IIntersectable"/> object and a collider
	/// </summary>
	/// <param name="first"> <see cref="IIntersectable"/> object, must not be null </param>
	/// <param name="second"> The collider, must not be null and satisfy <see cref="Intersections.Supported{T}"/>> </param>
	/// <returns> True if the intersection occurs, false otherwise </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the 2 arguments is null </exception>
	/// <remarks>
	/// In the event that the method fails to conclude if an intersection occurs, it will assume false and print an error message to the console. <para />
	/// The order of the operands doesn't matter, that is <c> Test(first, second) == Test(second, first) </c>
	/// </remarks>
	public static bool Test(IIntersectable first, Collider second) {
		ArgumentNullException.ThrowIfNull(first, nameof(first));
		ArgumentNullException.ThrowIfNull(second, nameof(second));

		return TestInternal(first, new IntersectableCollider(second));
	}

	/// <summary>
	/// Test for intersection between a collider and an <see cref="IIntersectable"/> object
	/// </summary>
	/// <param name="first"> The collider, must not be null and satisfy <see cref="Intersections.Supported{T}"/>> </param>
	/// <param name="second"> <see cref="IIntersectable"/> object, must not be null </param>
	/// <returns> True if the intersection occurs, false otherwise </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the 2 arguments is null </exception>
	/// <remarks>
	/// In the event that the method fails to conclude if an intersection occurs, it will assume false and print an error message to the console. <para />
	/// The order of the operands doesn't matter, that is <c> Test(first, second) == Test(second, first) </c>
	/// </remarks>
	public static bool Test(Collider first, IIntersectable second) {
		ArgumentNullException.ThrowIfNull(first, nameof(first));
		ArgumentNullException.ThrowIfNull(second, nameof(second));

		return TestInternal(new IntersectableCollider(first), second);
	}

	/// <summary>
	/// Test for intersection between two <see cref="IIntersectable"/> objects
	/// </summary>
	/// <param name="first"> The first <see cref="IIntersectable"/> object, must not be null </param>
	/// <param name="second"> The second <see cref="IIntersectable"/> object, must not be null </param>
	/// <returns> True if the intersection occurs, false otherwise </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the 2 arguments is null </exception>
	/// <remarks>
	/// In the event that the method fails to conclude if an intersection occurs, it will assume false and print an error message to the console. <para />
	/// The order of the operands doesn't matter, that is <c> Test(first, second) == Test(second, first) </c>
	/// </remarks>
	public static bool Test(IIntersectable first, IIntersectable second) {
		ArgumentNullException.ThrowIfNull(first, nameof(first));
		ArgumentNullException.ThrowIfNull(second, nameof(second));

		return TestInternal(first, second);
	}

	private static bool TestInternal(IIntersectable first, IIntersectable second) {
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

/// <summary>
/// Container class for colliders that allows for intersection tests
/// </summary>
public class IntersectableCollider : IIntersectable {
	/// <summary>
	/// <see cref="Sandbox.Collider"/> wrapped in this object, should satisfy <see cref="Intersections.Supported{T}"/> to work correctly
	/// </summary>
	public Collider Collider { get; init; }

	/// <summary>
	/// Constructs a new <see cref="IntersectableCollider"/>
	/// </summary>
	/// <param name="collider"> Collider to wrap into this object. Checking if it satisfies <see cref="Intersections.Supported{T}"/> is left to the user </param>
	/// <exception cref="ArgumentNullException"> Thrown if the collider is null </exception>
	public IntersectableCollider(Collider collider) {
		ArgumentNullException.ThrowIfNull(collider, nameof(collider));

		Collider = collider;
	}

	/// <summary>
	/// World position of the underlying collider
	/// </summary>
	public Vector3 WorldPosition {
		get => Collider.WorldPosition;
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction
	/// </summary>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Thrown if the wrapped collider doesn't satisfy <see cref="Intersections.Supported{T}"/> </exception>
	public Vector3 GetFurthestPoint(Vector3 direction) {
		return Collider.FurthestPoint(direction);;
	}
}

/// <summary>
/// An intersectable cylinder, with it's base at WorldPosition and facing towards <see cref="WorldTransform"/>.Forward
/// </summary>
public class IntersectableCylinder : IIntersectable {
	/// <summary>
	/// Transform used by this cylinder, affects the position, rotation and scale of the cylinder
	/// </summary>
	public Transform WorldTransform { get; init; }
	/// <summary>
	/// Radius of the cylinder
	/// </summary>
	public float Radius { get; init; }
	/// <summary>
	/// Height of the cylinder, stretching towards <see cref="WorldTransform"/>.Forward
	/// </summary>
	public float Height { get; init; }

	/// <summary>
	/// Create a cylinder located at the world origin, pointing towards world's positive X
	/// </summary>
	/// <param name="radius"> Radius of the cylinder </param>
	/// <param name="height"> Height of the cylinder </param>
	public IntersectableCylinder(float radius, float height) {
		Radius = radius;
		Height = height;
		WorldTransform = Transform.Zero;
	}

	/// <summary>
	/// Create a cylinder located at the <see cref="WorldTransform"/>.Position, pointing towards <see cref="WorldTransform"/>.Forward
	/// </summary>
	/// <param name="radius"> Radius of the cylinder </param>
	/// <param name="height"> Height of the cylinder </param>
	/// <param name="worldTransform"> The transform used by this cylinder </param>
	public IntersectableCylinder(float radius, float height, Transform worldTransform) {
		Radius = radius;
		Height = height;
		WorldTransform = worldTransform;
	}

	/// <summary>
	/// Create a cylinder centered at worldTransform.Position, pointing towards <see cref="WorldTransform"/>.Forward
	/// </summary>
	/// <param name="radius"> Radius of the cylinder </param>
	/// <param name="height"> Height of the cylinder </param>
	/// <param name="worldTransform"> The transform used by this cylinder </param>
	public static IntersectableCylinder CenteredAt(float radius, float height, Transform worldTransform) {
		Transform t = worldTransform.WithPosition(worldTransform.Position + worldTransform.Backward * (height / 2));

		return new IntersectableCylinder(radius, height, t);
	}

	/// <summary>
	/// The point at the center of the base of this cylinder
	/// </summary>
	public Vector3 WorldPosition {
		get => WorldTransform.Position;
	}

	/// <summary>
	/// Returns the furthest point on this cylinder in the given direction
	/// </summary>
	/// <param name="direction"> World space normalized direction vector </param>
	/// <returns> World space position of the point on this cylinder that's the furthest in the given direction </returns>
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
	/// <summary>
	/// The furthest point on the wrapped collider in the given direction
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	public static Vector3 FurthestPoint(this BoxCollider self, Vector3 direction) {
		Vector3 localDir = self.WorldTransform.NormalToLocal(direction);

		Vector3 furthestPoint;

		localDir.x = Math.Sign(localDir.x);
		localDir.y = Math.Sign(localDir.y);
		localDir.z = Math.Sign(localDir.z);

		furthestPoint = self.WorldTransform.PointToWorld(self.Center + localDir * self.Scale / 2);

		return furthestPoint;
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this CapsuleCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this HullCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this MapCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this MeshComponent self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this ModelCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this PlaneCollider self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	public static Vector3 FurthestPoint(this SphereCollider self, Vector3 direction) {
		return self.WorldPosition + self.WorldTransform.PointToWorld(self.Center) + direction * self.Radius;
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction. Not supported, will always throw <see cref="NotSupportedException"/>
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Always thrown </exception>
	public static Vector3 FurthestPoint(this Terrain self, Vector3 direction) {
		throw new NotImplementedException("Unsupported type: " + self.GetType().ToString());
	}

	/// <summary>
	/// The furthest point on the wrapped collider in the given direction
	/// </summary>
	/// <param name="self"> The tested collider </param>
	/// <param name="direction"> World space normalized direction </param>
	/// <returns> World space position of the point on the collider that's the furthest in the given direction </returns>
	/// <exception cref="NotSupportedException"> Thrown if <c> self </c> collider doesn't satisfy <see cref="Intersections.Supported{T}"/> </exception>
	public static Vector3 FurthestPoint(this Collider self, Vector3 direction) {
		if (self is IIntersectable) {
			return (self as IIntersectable).GetFurthestPoint(direction);
		}
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