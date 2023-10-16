using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using hamsterbyte.WFC;

public partial class Test : TileMap{
    private WFCGrid grid;
    [Export] private int width = 50;
    [Export] private int height = 50;
    [Export(PropertyHint.File)] private string rulePath;
    [Export] private bool wrap;
    private TileSetAtlasSource source;

    public override void _Ready(){
        WFCGrid.onComplete += OnGenerationComplete;
        List<WFCRule> rules = WFCRule.FromJSONFile(ProjectSettings.GlobalizePath(rulePath));
        grid = new WFCGrid(width, height, rules);
    }

    public override void _Process(double delta){
        if (Input.IsActionJustPressed("Generate")){
            GenerateGrid();
        }
    }

    private void OnGenerationComplete(WFCResult result){
        if (!result.Success) return;
        StartPopulatingTilemap(result.Grid);
    }

    private void GenerateGrid(){
        if (grid.Busy) return;
        ClearTilemap();
        grid.TryCollapse(wrap);
    }

    private async Task StartPopulatingTilemap(WFCGrid _grid){
        source = TileSet.GetSource(0) as TileSetAtlasSource;
        bool complete = await Task.Run(() => PopulateTilemapAsync(_grid));
        GD.Print(complete);
    }

    private async Task<bool> PopulateTilemapAsync(WFCGrid _grid){
        while (_grid.AnimationCoordinates.Count > 0){
            CallDeferred("SetNextCell", _grid.AnimationCoordinates.Dequeue().AsVector2I);
            await Task.Delay(5);
        }

        return true;
    }

    private void SetNextCell(Vector2I c){
        EraseCell(0, c);
        if (grid[c.X, c.Y].TileIndex == -1) return;
        SetCell(0, c, 0,
            source.GetTileId(grid[c.X, c.Y].TileIndex));
    }

    private void ClearTilemap(){
        foreach (Vector2I v in GetUsedCells(0)){
            EraseCell(0, v);
        }
    }
}