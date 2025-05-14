using System;
using System.Threading.Tasks;

namespace MathY {
	static class Interpolation {
		private static float[] DividedDiff(Vector2[] points) {
			int n = points.Length;

			float[,] coef = new float[n, n];

			for (int rowNum = 0; rowNum < n; rowNum++) {
				coef[rowNum, 0] = points[rowNum].y;
			}

			for (int j = 1; j < n; j++) {
				for (int i = 0; i < n - j; i++) {
					coef[i, j] = (coef[i+1, j-1] - coef[i, j-1]) / (points[i+j].x-points[i].x);
				}
			}

			return Enumerable.Range(0, n).Select( i => coef[0, i] ).ToArray();
		}

		public static Func<float, float> NewtonPolynomial(Vector2[] points) {
			var coefficients = DividedDiff(points);
			var pointsOrdinals = points.Select( p => p.x ).ToArray();

			return (x) => {
				float result = coefficients[0];
				float factor = x - pointsOrdinals[0];

				for (int i = 1; i < coefficients.Length; i++) {
					result += coefficients[i] * factor;
					factor *= x - pointsOrdinals[i];
				}

				return result;
			};
		}
	}
}