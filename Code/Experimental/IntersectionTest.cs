class IntersectionTest : Component {
	[Property]
	private Collider first;
	[Property]
	private Collider second;

	protected override void OnUpdate() {
		if (first == null || second == null) {
			this.GetComponent<ModelRenderer>().Tint = Color.White;

			return;
		}

		bool collides = first.IntersectsWith(second);

		this.GetComponent<ModelRenderer>().Tint = collides ? Color.Green : Color.Red;
	}
}