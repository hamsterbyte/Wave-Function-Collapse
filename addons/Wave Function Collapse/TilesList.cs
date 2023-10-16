using Godot;
using Godot.Collections;

[Tool]
public partial class TilesList : ItemList{
    public delegate void OnDropData(NodePath nodePath);

    public OnDropData onDropData;

    public override bool _CanDropData(Vector2 atPosition, Variant data){
        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data){
        Dictionary path = data.AsGodotDictionary();
        Array p = path["nodes"].AsGodotArray();
        NodePath nodePath = p[0].ToString();
        onDropData?.Invoke(nodePath);
    }
}