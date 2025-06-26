using System;
using Sandbox.Menu;

public struct FloatRange {
	private float _min = float.NegativeInfinity;
	private float _max = float.PositiveInfinity;

	public float Min {
		get => _min;
		set => _min = Math.Min(value, Max);
	}
	public float Max {
		get => _max;
		set => _max = Math.Max(value, Min);
	}

	public FloatRange() {
		this._min = float.NegativeInfinity;
		this._max = float.PositiveInfinity;
	}

	public FloatRange(float min, float max) {
		this._min = Math.Min(min, max);
		this._max = Math.Max(min, max);
	}

	public float Clamp(float val) {
		if (val < Min) {
			return Min;
		}
		if (val > Max) {
			return Max;
		}
		return val;

	}
}
