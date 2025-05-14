using System;
using Sandbox;
using MathY;

namespace Panzerwaffle.TankControl {
	public class PowerPack : Component {
		public enum TransmissionType {
			Manual,
			Automatic
		}
		
		private class Gear {
			public float ratio;
			public float? lowRPM;
			public float? highRPM;
		}

		private class TorqueCharacteristic {
			
		}

		[Property, Group("Transmission")]
		private TransmissionType transmissionType = TransmissionType.Automatic;

		[Property, InlineEditor, Group("Transmission")]
		private List<Gear> gears;

		[Property, Group("Engine")]
		private float maxRPM;

		private float throttle = 0;
		private float currentRPM;

		public float Throttle {
			get => this.throttle;
			set {
				this.throttle = Math.Clamp(value, 0, 1);
			}
		}

		public float RPM {
			get => this.currentRPM;
		}

		protected override void OnUpdate() {

		}

		/// <summary>
		/// Get the output speed in rad/s
		/// </summary>
		/// <returns></returns>
		public float GetOutputSpeed() {
			return this.throttle * 3;
		}
	}
}