[CustomEditor(typeof(FloatRange))]
public class RangeEditor : ControlWidget {
	public override bool SupportsMultiEdit => false;

	public RangeEditor(SerializedProperty property) : base(property) {
		Layout = Layout.Column();
		
		Layout labelRow = Layout.Row();
		labelRow.Margin = 1;

		Layout fieldRow = Layout.Row();

		if (!property.TryGetAsObject(out var serializedObject)) {
			return;
		}

		serializedObject.TryGetProperty("Min", out var minProperty);
		serializedObject.TryGetProperty("Max", out var maxProperty);

		labelRow.Add(new Label("Min") { Alignment = TextFlag.Center,  });
		labelRow.Add(new Label("Max") { Alignment = TextFlag.Center });

		fieldRow.Add(Create(minProperty));
		fieldRow.Add(Create(maxProperty));

		Layout.Add(labelRow);
		Layout.Add(fieldRow);
	}
}