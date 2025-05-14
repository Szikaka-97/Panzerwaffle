using Sandbox;
using Sandbox.ModelEditor;

namespace Panzerwaffle {
	public sealed class TreeGenerator : Component {
		[Property]
		private BBox bounds;
		[Property]
		private Model prefab;
		[Property]
		private int treeCount;
		[Property]
		private float scale;

		protected override void OnAwake() {
			for (int i = 0; i < treeCount; i++) {
				var pos = bounds.RandomPointInside;

				var treeObject = new GameObject(this.GameObject, true, "tree_" + i);
				treeObject.LocalPosition = pos;
				treeObject.LocalScale = Vector3.One * scale;
				treeObject.AddComponent<ModelRenderer>().Model = this.prefab;
			}
		}
	}
}